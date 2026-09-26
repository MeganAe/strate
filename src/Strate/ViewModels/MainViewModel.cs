using Strate.Brand;

namespace Strate.ViewModels;

public partial class MainViewModel : ObservableObject, IPageHost
{
    private readonly List<PageViewModel> _pages;

    public MainViewModel(AppSettings settings)
    {
        Settings = settings;
        Dashboard = new DashboardViewModel();
        Analysis = new AnalysisViewModel(settings);
        LargeFiles = new LargeFilesViewModel(settings);
        Duplicates = new DuplicatesViewModel(settings);
        Cleanup = new CleanupViewModel(settings);
        Startup = new StartupViewModel();
        Disks = new DisksViewModel();
        Report = new ReportViewModel();
        SettingsPage = new SettingsViewModel(settings);

        StoragePages = [Dashboard, Analysis, LargeFiles, Duplicates, Cleanup];
        SystemPages = [Startup, Disks, Report, SettingsPage];
        _pages = [..StoragePages, ..SystemPages];
        foreach (var page in _pages)
            page.Host = this;

        Dashboard.IsSelected = true;
        Current = Dashboard;
    }

    public AppSettings Settings { get; }
    public DashboardViewModel Dashboard { get; }
    public AnalysisViewModel Analysis { get; }
    public LargeFilesViewModel LargeFiles { get; }
    public DuplicatesViewModel Duplicates { get; }
    public CleanupViewModel Cleanup { get; }
    public StartupViewModel Startup { get; }
    public DisksViewModel Disks { get; }
    public ReportViewModel Report { get; }
    public SettingsViewModel SettingsPage { get; }
    public IReadOnlyList<PageViewModel> StoragePages { get; }
    public IReadOnlyList<PageViewModel> SystemPages { get; }
    public SnackbarMessageQueue Queue => AppMessenger.Queue;
    public string ProductName => BrandInfo.Name;
    public string Tagline => BrandInfo.Tagline;
    public string VersionText => "Version " + BrandInfo.Version;

    [ObservableProperty]
    private PageViewModel _current = null!;

    public string WindowTitle => BrandInfo.Name + " — " + Current.Title;

    partial void OnCurrentChanged(PageViewModel value) => OnPropertyChanged(nameof(WindowTitle));

    public void NotifySelected(PageViewModel page)
    {
        foreach (var other in _pages)
        {
            if (!ReferenceEquals(other, page) && other.IsSelected)
                other.IsSelected = false;
        }

        Current = page;
    }

    public void GoTo<T>() where T : PageViewModel
    {
        var page = _pages.OfType<T>().First();
        page.IsSelected = true;
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        var next = ThemeController.IsDark ? "Light" : "Dark";
        Settings.Theme = next;
        SettingsStore.Save(Settings);
        ThemeController.Apply(next);
        SettingsPage.SyncTheme(next);
    }
}
