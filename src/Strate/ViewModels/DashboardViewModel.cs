namespace Strate.ViewModels;

public partial class DashboardViewModel : PageViewModel
{
    private bool _started;

    public override string Title => "Tableau de bord";
    public override string Subtitle => "Occupation des volumes fixes et estimation de ce qui peut être libéré sans toucher à vos documents.";
    public override PackIconKind Icon => PackIconKind.ViewDashboard;

    public ObservableCollection<VolumeRow> Volumes { get; } = [];

    [ObservableProperty]
    private string _systemPercent = "—";

    [ObservableProperty]
    private string _systemSummary = "Lecture des volumes…";

    [ObservableProperty]
    private bool _systemIsLow;

    [ObservableProperty]
    private double _systemPercentValue;

    [ObservableProperty]
    private string _recoverableText = "—";

    [ObservableProperty]
    private string _recoverableDetail = "Mesure des fichiers temporaires et de la corbeille.";

    [ObservableProperty]
    private string _freeText = "—";

    [ObservableProperty]
    private string _volumeCountText = "—";

    [ObservableProperty]
    private bool _hasVolumes;

    protected override void OnActivated()
    {
        if (_started)
            return;
        _started = true;
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        StatusText = "Lecture des volumes…";
        try
        {
            var volumes = await Task.Run(VolumeService.GetVolumes);
            Volumes.Clear();
            foreach (var volume in volumes)
                Volumes.Add(volume);
            HasVolumes = Volumes.Count > 0;
            AppState.Volumes = volumes;

            var system = volumes.FirstOrDefault(volume => volume.IsSystem) ?? volumes.FirstOrDefault();
            if (system is null)
            {
                SystemPercent = "—";
                SystemSummary = "Aucun volume fixe prêt.";
                SystemPercentValue = 0;
                SystemIsLow = false;
            }
            else
            {
                SystemPercent = system.PercentText;
                SystemSummary = system.Title + " · " + system.Summary;
                SystemPercentValue = system.UsedPercent;
                SystemIsLow = system.IsLow;
            }

            VolumeCountText = volumes.Count.ToString(CultureInfo.GetCultureInfo("fr-FR"));
            FreeText = ByteFormat.Format(volumes.Sum(volume => volume.Free));
            StatusText = HasVolumes ? "Volumes à jour." : "Aucun volume fixe prêt.";
            _ = MeasureRecoverableAsync();
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

    private async Task MeasureRecoverableAsync()
    {
        try
        {
            StatusText = "Mesure des éléments récupérables…";
            var temp = await Task.Run(() => CleanupService.Measure(CleanupService.CreateCatalog().First(c => c.Id == "user-temp"), CancellationToken.None));
            var bin = await Task.Run(() => CleanupService.Measure(CleanupService.CreateCatalog().First(c => c.Id == "recycle"), CancellationToken.None));
            var total = temp.Bytes + bin.Bytes;
            RecoverableText = ByteFormat.Format(total);
            RecoverableDetail = $"{ByteFormat.Format(temp.Bytes)} temporaires · {ByteFormat.Format(bin.Bytes)} dans la corbeille";
            StatusText = "Tableau de bord à jour.";
        }
        catch (Exception ex)
        {
            Log.Write(ex);
            RecoverableDetail = "Mesure impossible pour le moment.";
        }
    }

    [RelayCommand]
    private void OpenCleanup() => Host?.GoTo<CleanupViewModel>();
}
