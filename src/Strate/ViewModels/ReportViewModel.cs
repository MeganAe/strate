using Strate.Native;

namespace Strate.ViewModels;

public partial class ReportViewModel : PageViewModel
{
    public override string Title => "Rapport";
    public override string Subtitle => "Synthèse de la session en cours. L'export est un fichier HTML dans Documents\\Strate\\Rapports.";
    public override PackIconKind Icon => PackIconKind.FileChart;

    [ObservableProperty]
    private string _volumesText = "Aucun volume lu.";

    [ObservableProperty]
    private string _analysisText = "Analyse non lancée.";

    [ObservableProperty]
    private string _largeText = "Recherche non lancée.";

    [ObservableProperty]
    private string _duplicateText = "Comparaison non lancée.";

    [ObservableProperty]
    private string _cleanupText = "Aucun nettoyage.";

    [ObservableProperty]
    private string _startupText = "Démarrage non lu.";

    [ObservableProperty]
    private string _diskText = "Disques non lus.";

    protected override void OnActivated() => Refresh();

    [RelayCommand]
    private void Refresh()
    {
        VolumesText = AppState.Volumes.Count == 0
            ? "Aucun volume lu."
            : string.Join("\n", AppState.Volumes.Select(volume => volume.Title + " — " + volume.Summary + " — " + volume.FreeText));
        AnalysisText = AppState.AnalysisPath is null
            ? "Analyse non lancée."
            : $"{AppState.AnalysisPath}\n{ByteFormat.Format(AppState.AnalysisBytes)} sur {AppState.AnalysisEntries:N0} éléments.";
        LargeText = AppState.LargeFilePath is null
            ? "Recherche non lancée."
            : $"{AppState.LargeFilePath}\n{AppState.LargeFileCount:N0} fichiers, {ByteFormat.Format(AppState.LargeFileBytes)}.";
        DuplicateText = AppState.DuplicatePath is null
            ? "Comparaison non lancée."
            : $"{AppState.DuplicatePath}\n{AppState.DuplicateGroups:N0} groupes, {ByteFormat.Format(AppState.DuplicateWaste)} en copies.";
        CleanupText = AppState.LastCleanup ?? "Aucun nettoyage dans cette session.";
        StartupText = AppState.StartupCount == 0 ? "Démarrage non lu." : $"{AppState.StartupCount:N0} entrées lues.";
        DiskText = AppState.DiskSummary ?? "Disques non lus.";
        StatusText = "Synthèse à jour.";
    }

    [RelayCommand]
    private void Export()
    {
        try
        {
            Refresh();
            var path = ReportService.Export();
            StatusText = "Rapport enregistré.";
            AppMessenger.Say("Rapport enregistré.");
            ShellInterop.Reveal(path);
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    [RelayCommand]
    private void OpenFolder() => ShellInterop.Open(ReportService.Folder());
}
