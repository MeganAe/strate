using Strate.Native;

namespace Strate.ViewModels;

public partial class AnalysisViewModel : PageViewModel
{
    private readonly AppSettings _settings;
    private CancellationTokenSource? _cts;

    public AnalysisViewModel(AppSettings settings)
    {
        _settings = settings;
        FolderPath = settings.AnalysisPath is { Length: > 0 } path && Directory.Exists(path)
            ? path
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        QuickRoots = new ObservableCollection<string>(CommonRoots.All());
    }

    public override string Title => "Analyse";
    public override string Subtitle => "Taille des éléments d'un dossier. Les liens symboliques sont ignorés. Entrez dans un dossier pour descendre d'un niveau.";
    public override PackIconKind Icon => PackIconKind.ChartBar;

    public ObservableCollection<string> QuickRoots { get; }
    public ObservableCollection<Crumb> Crumbs { get; } = [];
    public ObservableCollection<FolderEntry> Entries { get; } = [];

    [ObservableProperty]
    private string _folderPath;

    [ObservableProperty]
    private string? _quickRoot;

    [ObservableProperty]
    private string _totalText = "Aucune analyse.";

    [ObservableProperty]
    private FolderEntry? _selectedEntry;

    [ObservableProperty]
    private bool _hasEntries;

    partial void OnQuickRootChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            FolderPath = value;
    }

    partial void OnSelectedEntryChanged(FolderEntry? value) => OpenLocationCommand.NotifyCanExecuteChanged();

    protected override void OnBusyChanged()
    {
        AnalyzeCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        BrowseCommand.NotifyCanExecuteChanged();
        GoParentCommand.NotifyCanExecuteChanged();
    }

    private bool CanStart() => !IsBusy;
    private bool CanCancel() => IsBusy;
    private bool CanGoParent()
    {
        if (IsBusy || string.IsNullOrWhiteSpace(FolderPath))
            return false;
        try
        {
            return Directory.GetParent(FolderPath.TrimEnd('\\')) is not null;
        }
        catch
        {
            return false;
        }
    }
    private bool HasSelection() => SelectedEntry is not null;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Browse()
    {
        var picked = FolderBrowser.Pick("Dossier à analyser", FolderPath);
        if (!string.IsNullOrWhiteSpace(picked))
            FolderPath = picked;
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task AnalyzeAsync() => await RunAsync(FolderPath);

    [RelayCommand]
    private async Task EnterAsync(FolderEntry? entry)
    {
        if (entry is not { IsDirectory: true } || IsBusy)
            return;
        FolderPath = entry.FullPath;
        await RunAsync(entry.FullPath);
    }

    [RelayCommand]
    private async Task OpenCrumbAsync(Crumb? crumb)
    {
        if (crumb is null || IsBusy)
            return;
        FolderPath = crumb.FullPath;
        await RunAsync(crumb.FullPath);
    }

    [RelayCommand(CanExecute = nameof(CanGoParent))]
    private async Task GoParentAsync()
    {
        var parent = Directory.GetParent(FolderPath.TrimEnd('\\'));
        if (parent is null)
            return;
        FolderPath = parent.FullName;
        await RunAsync(parent.FullName);
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void OpenLocation()
    {
        if (SelectedEntry is not null)
            ShellInterop.Reveal(SelectedEntry.FullPath);
    }

    private async Task RunAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            StatusText = "Choisissez un dossier existant.";
            return;
        }

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        IsBusy = true;
        StatusText = "Analyse en cours…";
        try
        {
            var progress = new Progress<ScanTick>(tick => StatusText = $"{tick.Files:N0} fichiers lus");
            var entries = await Task.Run(() => SpaceAnalyzer.MeasureChildren(path, progress, ct), ct);
            Entries.Clear();
            foreach (var entry in entries)
                Entries.Add(entry);
            HasEntries = Entries.Count > 0;
            var total = entries.Sum(entry => entry.Bytes);
            TotalText = $"{ByteFormat.Format(total)} · {entries.Count.ToString("N0", CultureInfo.GetCultureInfo("fr-FR"))} éléments";
            RebuildCrumbs(path);
            _settings.AnalysisPath = path;
            SettingsStore.Save(_settings);
            AppState.AnalysisPath = path;
            AppState.AnalysisBytes = total;
            AppState.AnalysisEntries = entries.Count;
            StatusText = "Analyse terminée.";
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

    private void RebuildCrumbs(string path)
    {
        Crumbs.Clear();
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full);
        if (string.IsNullOrEmpty(root))
            return;
        Crumbs.Add(new Crumb(root, root));
        if (full.TrimEnd('\\').Equals(root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            return;
        var current = root;
        foreach (var part in full[root.Length..].Split('\\', StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            Crumbs.Add(new Crumb(part, current));
        }
    }
}
