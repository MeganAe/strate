using Strate.Native;

namespace Strate.ViewModels;

public partial class SettingsViewModel : PageViewModel
{
    private readonly AppSettings _settings;

    public SettingsViewModel(AppSettings settings)
    {
        _settings = settings;
        Themes =
        [
            new ChoiceOption { Id = "Light", Label = "Clair" },
            new ChoiceOption { Id = "Dark", Label = "Sombre" },
            new ChoiceOption { Id = "System", Label = "Système" }
        ];
        var match = Themes.FirstOrDefault(choice => choice.Id == settings.Theme) ?? Themes[2];
        match.IsActive = true;
        foreach (var choice in Themes)
            choice.OnChosen = OnThemeChosen;
        PermanentTempDelete = settings.PermanentTempDelete;
    }

    public override string Title => "Réglages";
    public override string Subtitle => "Le thème suit les rôles de couleur Material Design 3. Le nettoyage des fichiers temporaires peut être définitif, ou passer par la corbeille.";
    public override PackIconKind Icon => PackIconKind.Cog;

    public ChoiceOption[] Themes { get; }
    public string VersionText => BrandInfo.Name + " " + BrandInfo.Version;
    public string AboutText => BrandInfo.Summary;

    [ObservableProperty]
    private bool _permanentTempDelete;

    partial void OnPermanentTempDeleteChanged(bool value)
    {
        _settings.PermanentTempDelete = value;
        SettingsStore.Save(_settings);
    }

    public void SyncTheme(string id)
    {
        foreach (var choice in Themes)
        {
            if (choice.IsActive != (choice.Id == id))
                choice.IsActive = choice.Id == id;
        }
    }

    private void OnThemeChosen(ChoiceOption choice)
    {
        foreach (var other in Themes)
        {
            if (!ReferenceEquals(other, choice) && other.IsActive)
                other.IsActive = false;
        }

        _settings.Theme = choice.Id;
        SettingsStore.Save(_settings);
        ThemeController.Apply(choice.Id);
        StatusText = choice.Id == "System" ? "Thème suivi sur Windows." : "Thème appliqué.";
    }

    [RelayCommand]
    private void OpenLogs()
    {
        Directory.CreateDirectory(SettingsStore.DirectoryPath);
        if (!File.Exists(SettingsStore.LogPath))
            File.WriteAllText(SettingsStore.LogPath, "");
        ShellInterop.Reveal(SettingsStore.LogPath);
    }

    [RelayCommand]
    private void OpenAppData() => ShellInterop.Open(SettingsStore.DirectoryPath);
}
