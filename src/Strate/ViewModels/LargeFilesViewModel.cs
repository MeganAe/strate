using System.Windows.Data;
using Strate.Native;

namespace Strate.ViewModels;

public partial class LargeFilesViewModel : PageViewModel
{
    private readonly AppSettings _settings;
    private CancellationTokenSource? _cts;

    public LargeFilesViewModel(AppSettings settings)
    {
        _settings = settings;
        FolderPath = settings.LargeFilesPath is { Length: > 0 } path && Directory.Exists(path)
            ? path
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        QuickRoots = new ObservableCollection<string>(CommonRoots.All());
        Thresholds =
        [
            new SizeOption("50 Mo", 50L * 1024 * 1024, settings.LargeFileThreshold == 50L * 1024 * 1024),
            new SizeOption("100 Mo", 100L * 1024 * 1024, settings.LargeFileThreshold == 100L * 1024 * 1024),
            new SizeOption("500 Mo", 500L * 1024 * 1024, settings.LargeFileThreshold == 500L * 1024 * 1024),
            new SizeOption("1 Go", 1024L * 1024 * 1024, settings.LargeFileThreshold == 1024L * 1024 * 1024)
        ];
        if (Thresholds.All(option => !option.IsActive))
            Thresholds[1].IsActive = true;
        ThresholdBytes = Thresholds.First(option => option.IsActive).Bytes;
        foreach (var option in Thresholds)
        {
            option.OnChosen = chosen =>
            {
                ThresholdBytes = chosen.Bytes;
                foreach (var other in Thresholds)
                {
                    if (!ReferenceEquals(other, chosen) && other.IsActive)
                        other.IsActive = false;
                }
            };
        }

        RowsView = CollectionViewSource.GetDefaultView(Rows);
        RowsView.Filter = Filter;
    }

    public override string Title => "Gros fichiers";
    public override string Subtitle => "Fichiers au-dessus d'un seuil. L'envoi à la corbeille ne détruit rien tant que la corbeille n'est pas vidée. Tailles binaires, comme l'Explorateur.";
    public override PackIconKind Icon => PackIconKind.FileFind;

    public ObservableCollection<string> QuickRoots { get; }
    public SizeOption[] Thresholds { get; }
    public ObservableCollection<FileRow> Rows { get; } = [];
    public ICollectionView RowsView { get; }
    public long ThresholdBytes { get; private set; }

    [ObservableProperty]
    private string _folderPath;

    [ObservableProperty]
    private string? _quickRoot;

    [ObservableProperty]
    private string _query = "";

    [ObservableProperty]
    private string _summary = "Aucune recherche.";

    [ObservableProperty]
    private FileRow? _selectedRow;

    [ObservableProperty]
    private bool _hasRows;

    partial void OnQuickRootChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            FolderPath = value;
    }

    partial void OnQueryChanged(string value) => RowsView.Refresh();
    partial void OnSelectedRowChanged(FileRow? value) => OpenLocationCommand.NotifyCanExecuteChanged();

    protected override void OnBusyChanged()
    {
        ScanCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        BrowseCommand.NotifyCanExecuteChanged();
    }

    private bool CanStart() => !IsBusy;
    private bool CanCancel() => IsBusy;
    private bool HasSelection() => SelectedRow is not null;

    private bool Filter(object item)
    {
        if (item is not FileRow row || string.IsNullOrWhiteSpace(Query))
            return item is FileRow;
        return row.Name.Contains(Query, StringComparison.OrdinalIgnoreCase)
            || row.FullPath.Contains(Query, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Browse()
    {
        var picked = FolderBrowser.Pick("Dossier à parcourir", FolderPath);
        if (!string.IsNullOrWhiteSpace(picked))
            FolderPath = picked;
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task ScanAsync()
    {
        if (!Directory.Exists(FolderPath))
        {
            StatusText = "Choisissez un dossier existant.";
            return;
        }

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        IsBusy = true;
        StatusText = "Recherche des gros fichiers…";
        try
        {
            var progress = new Progress<ScanTick>(tick => StatusText = $"{tick.Files:N0} fichiers examinés");
            var threshold = ThresholdBytes;
            var result = await Task.Run(() =>
            {
                var rows = LargeFileFinder.Find(FolderPath, threshold, progress, ct, out var capped);
                return (rows, capped);
            }, ct);

            Rows.Clear();
            foreach (var row in result.rows)
                Rows.Add(row);
            HasRows = Rows.Count > 0;
            var bytes = Rows.Sum(row => row.Length);
            Summary = result.capped
                ? $"{Rows.Count:N0} plus gros fichiers affichés, liste plafonnée. {ByteFormat.Format(bytes)}."
                : $"{Rows.Count:N0} fichiers, {ByteFormat.Format(bytes)}.";
            _settings.LargeFilesPath = FolderPath;
            _settings.LargeFileThreshold = threshold;
            SettingsStore.Save(_settings);
            AppState.LargeFilePath = FolderPath;
            AppState.LargeFileCount = Rows.Count;
            AppState.LargeFileBytes = bytes;
            StatusText = "Recherche terminée.";
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CheckVisible()
    {
        foreach (FileRow row in RowsView)
            row.IsChecked = true;
    }

    [RelayCommand]
    private void UncheckAll()
    {
        foreach (var row in Rows)
            row.IsChecked = false;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void OpenLocation()
    {
        if (SelectedRow is not null)
            ShellInterop.Reveal(SelectedRow.FullPath);
    }

    [RelayCommand]
    private async Task RecycleCheckedAsync()
    {
        var selected = Rows.Where(row => row.IsChecked).ToList();
        if (selected.Count == 0)
        {
            AppMessenger.Say("Cochez au moins un fichier.");
            return;
        }

        var bytes = selected.Sum(row => row.Length);
        var ok = await Strate.Views.Dialogs.ConfirmAsync(
            "Envoyer à la corbeille ?",
            $"{selected.Count:N0} fichiers, {ByteFormat.Format(bytes)}. Vous pourrez les restaurer depuis la corbeille.",
            "Envoyer",
            true);
        if (!ok)
            return;

        IsBusy = true;
        StatusText = "Envoi à la corbeille…";
        try
        {
            var outcome = await Task.Run(() => CleanupService.Delete(selected.Select(row => row.FullPath).ToList(), recycle: true, applyGrace: false, CancellationToken.None));
            foreach (var row in selected.Where(row => !File.Exists(row.FullPath)).ToList())
                Rows.Remove(row);
            HasRows = Rows.Count > 0;
            var message = $"{outcome.Deleted:N0} fichiers envoyés à la corbeille ({ByteFormat.Format(outcome.Bytes)}).";
            if (outcome.SkippedLocked > 0)
                message += $" {outcome.SkippedLocked:N0} ignorés, encore utilisés.";
            StatusText = message;
            AppMessenger.Say(message);
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
