using Strate.Native;

namespace Strate.ViewModels;

public partial class StartupViewModel : PageViewModel
{
    private bool _started;

    public override string Title => "Démarrage";
    public override string Subtitle => "Programmes lancés avec la session. Strate peut désactiver ceux de votre compte. Les entrées de l'ordinateur sont affichées, pas modifiées.";
    public override PackIconKind Icon => PackIconKind.RocketLaunch;

    public ObservableCollection<StartupEntry> Entries { get; } = [];

    [ObservableProperty]
    private bool _hasEntries;

    protected override void OnActivated()
    {
        if (_started)
            return;
        _started = true;
        Refresh();
    }

    [RelayCommand]
    private void Refresh()
    {
        IsBusy = true;
        try
        {
            var entries = StartupService.Load();
            Entries.Clear();
            foreach (var entry in entries)
                Entries.Add(entry);
            HasEntries = Entries.Count > 0;
            AppState.StartupCount = Entries.Count;
            StatusText = $"{Entries.Count:N0} entrées.";
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
    private void Toggle(StartupEntry? entry)
    {
        if (entry is null)
            return;
        if (!entry.CanToggle)
        {
            AppMessenger.Say("Cette entrée demande une élévation. Strate ne la modifie pas.");
            return;
        }

        var enable = !entry.Enabled;
        if (!StartupService.SetEnabled(entry, enable))
        {
            AppMessenger.Say("Impossible de modifier « " + entry.Name + " ».");
            return;
        }

        Refresh();
        AppMessenger.Say(enable ? "Entrée réactivée." : "Entrée désactivée. Elle n'est pas supprimée.");
    }

    [RelayCommand]
    private void OpenUserStartup()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        ShellInterop.Open(folder);
    }
}
