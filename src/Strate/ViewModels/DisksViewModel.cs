namespace Strate.ViewModels;

public partial class DisksViewModel : PageViewModel
{
    private bool _started;

    public override string Title => "Disques";
    public override string Subtitle => "Disques physiques vus par Windows, et état SMART lorsqu'il est exposé. Ce n'est pas un diagnostic matériel complet.";
    public override PackIconKind Icon => PackIconKind.Harddisk;

    public ObservableCollection<DiskRow> Disks { get; } = [];
    public ObservableCollection<VolumeRow> Volumes { get; } = [];

    [ObservableProperty]
    private string _smartText = "Non lu.";

    [ObservableProperty]
    private bool _hasDisks;

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
        StatusText = "Interrogation des disques…";
        try
        {
            var snapshot = await Task.Run(DiskHealthService.Query);
            var volumes = await Task.Run(VolumeService.GetVolumes);
            Disks.Clear();
            foreach (var disk in snapshot.Disks)
                Disks.Add(disk);
            Volumes.Clear();
            foreach (var volume in volumes)
                Volumes.Add(volume);
            HasDisks = Disks.Count > 0;
            SmartText = snapshot.Smart;
            AppState.DiskSummary = HasDisks
                ? $"{Disks.Count} disque(s). {snapshot.Smart}"
                : snapshot.Smart;
            AppState.Volumes = volumes;
            StatusText = HasDisks ? "Disques lus." : "Aucun disque exposé par WMI.";
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
