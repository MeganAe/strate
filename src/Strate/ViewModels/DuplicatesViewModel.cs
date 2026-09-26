using System.Windows.Data;
using Strate.Native;

namespace Strate.ViewModels;

public partial class DuplicatesViewModel : PageViewModel
{
    private readonly AppSettings _settings;
    private CancellationTokenSource? _cts;

    public DuplicatesViewModel(AppSettings settings)
    {
        _settings = settings;
        FolderPath = settings.DuplicatesPath is { Length: > 0 } path && Directory.Exists(path)
            ? path
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        QuickRoots = new ObservableCollection<string>(CommonRoots.All());
        RowsView = CollectionViewSource.GetDefaultView(Rows);
        RowsView.Filter = Filter;
    }

    public override string Title => "Doublons";
    public override string Subtitle => "Même taille, puis même empreinte. Une copie est marquée « À conserver » : la plus récente, à chemin égal le plus court. Rien n'est coché tout seul.";
    public override PackIconKind Icon => PackIconKind.ContentDuplicate;

    public ObservableCollection<string> QuickRoots { get; }
    public ObservableCollection<FileRow> Rows { get; } = [];
    public ICollectionView RowsView { get; }

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
            || row.FullPath.Contains(Query, StringComparison.OrdinalIgnoreCase)
            || row.SuggestionText.Contains(Query, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Browse()
    {
        var picked = FolderBrowser.Pick("Dossier à comparer", FolderPath);
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
        StatusText = "Recherche des doublons…";
        try
        {
            var progress = new Progress<ScanTick>(tick => StatusText = $"{tick.Files:N0} fichiers · {tick.Detail}");
            var result = await Task.Run(() =>
            {
                var rows = DuplicateFinder.Find(FolderPath, progress, ct, out var truncated, out var seen);
                return (rows, truncated, seen);
            }, ct);

            Rows.Clear();
            foreach (var row in result.rows)
                Rows.Add(row);
            HasRows = Rows.Count > 0;
            var groups = Rows.Select(row => row.GroupKey).Distinct().Count();
            var waste = Rows.Where(row => row.IsSuggested).Sum(row => row.Length);
            Summary = result.truncated
                ? $"Analyse limitée à {DuplicateFinder.FileCap:N0} fichiers. {groups:N0} groupes, {ByteFormat.Format(waste)} en copies."
                : $"{groups:N0} groupes, {ByteFormat.Format(waste)} en copies suggérées.";
            _settings.DuplicatesPath = FolderPath;
            SettingsStore.Save(_settings);
            AppState.DuplicatePath = FolderPath;
            AppState.DuplicateGroups = groups;
            AppState.DuplicateWaste = waste;
            StatusText = "Comparaison terminée.";
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
    private void CheckSuggested()
    {
        foreach (var row in Rows)
            row.IsChecked = row.IsSuggested;
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
            AppMessenger.Say("Cochez les copies à retirer.");
            return;
        }

        var wiped = Rows
            .GroupBy(row => row.GroupKey)
            .Where(group => group.Any(row => row.IsChecked) && group.All(row => row.IsChecked))
            .Select(group => group.First().Name)
            .Take(3)
            .ToList();
        if (wiped.Count > 0)
        {
            AppMessenger.Say("Au moins une copie doit rester dans chaque groupe. Utilisez « Cocher les doublons suggérés ».");
            return;
        }

        var bytes = selected.Sum(row => row.Length);
        var ok = await Strate.Views.Dialogs.ConfirmAsync(
            "Envoyer les copies à la corbeille ?",
            $"{selected.Count:N0} fichiers, {ByteFormat.Format(bytes)}. Une copie de chaque groupe reste en place.",
            "Envoyer",
            true);
        if (!ok)
            return;

        IsBusy = true;
        try
        {
            var outcome = await Task.Run(() => CleanupService.Delete(selected.Select(row => row.FullPath).ToList(), true, false, CancellationToken.None));
            foreach (var row in selected.Where(row => !File.Exists(row.FullPath)).ToList())
                Rows.Remove(row);
            HasRows = Rows.Count > 0;
            var message = $"{outcome.Deleted:N0} copies envoyées à la corbeille ({ByteFormat.Format(outcome.Bytes)}).";
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
