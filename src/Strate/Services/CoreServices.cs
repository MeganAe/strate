using System.Text.Json;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using Strate.Brand;

namespace Strate.Services;

public static class ByteFormat
{
    private static readonly string[] Units = ["o", "Ko", "Mo", "Go", "To", "Po"];
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    public static string Format(long bytes)
    {
        if (bytes < 0)
            bytes = 0;
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        var pattern = unit == 0 ? "0" : value >= 10 ? "0.0" : "0.00";
        return string.Concat(value.ToString(pattern, Fr), "\u00A0", Units[unit]);
    }
}

public static class Errors
{
    public static bool IsCancel(Exception ex)
    {
        if (ex is OperationCanceledException)
            return true;
        if (ex is AggregateException aggregate)
            return aggregate.Flatten().InnerExceptions.All(inner => inner is OperationCanceledException);
        return false;
    }
}

public sealed class AppSettings
{
    public string Theme { get; set; } = "System";
    public string? AnalysisPath { get; set; }
    public string? LargeFilesPath { get; set; }
    public string? DuplicatesPath { get; set; }
    public long LargeFileThreshold { get; set; } = 100L * 1024 * 1024;
    public bool PermanentTempDelete { get; set; } = true;
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string DirectoryPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Strate");

    public static string FilePath => Path.Combine(DirectoryPath, "settings.json");
    public static string LogPath => Path.Combine(DirectoryPath, "strate.log");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new AppSettings();
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Json));
    }
}

public static class Log
{
    public static void Write(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.DirectoryPath);
            File.AppendAllText(SettingsStore.LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Le journal ne doit jamais interrompre l'application.
        }
    }
}

public static class AppMessenger
{
    public static SnackbarMessageQueue Queue { get; private set; } = null!;

    public static void Initialize() => Queue = new SnackbarMessageQueue(TimeSpan.FromSeconds(5));

    public static void Say(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || Queue is null)
            return;
        Queue.Enqueue(message);
    }
}

public static class AppState
{
    public static IReadOnlyList<VolumeRow> Volumes { get; set; } = [];
    public static string? AnalysisPath { get; set; }
    public static long AnalysisBytes { get; set; }
    public static int AnalysisEntries { get; set; }
    public static string? LargeFilePath { get; set; }
    public static int LargeFileCount { get; set; }
    public static long LargeFileBytes { get; set; }
    public static string? DuplicatePath { get; set; }
    public static int DuplicateGroups { get; set; }
    public static long DuplicateWaste { get; set; }
    public static string? LastCleanup { get; set; }
    public static int StartupCount { get; set; }
    public static string? DiskSummary { get; set; }
}

public static class ThemeController
{
    public static event Action<bool>? Applied;
    public static bool IsDark { get; private set; }
    public static string Preference { get; set; } = "System";

    public static void Apply(string preference)
    {
        Preference = preference;
        var dark = preference switch
        {
            "Dark" => true,
            "Light" => false,
            _ => Theme.GetSystemTheme() == BaseTheme.Dark
        };
        ApplyResolved(dark);
    }

    private static void ApplyResolved(bool dark)
    {
        IsDark = dark;
        var colors = dark ? StrateColors.Dark : StrateColors.Light;
        var theme = Theme.Create(dark ? BaseTheme.Dark : BaseTheme.Light, colors.Primary, colors.Secondary);
        theme.Background = colors.Surface;
        theme.Foreground = colors.OnSurface;
        theme.ForegroundLight = colors.OnSurfaceVariant;
        theme.ValidationError = colors.Error;
        theme.Cards.Background = colors.SurfaceContainerLow;
        theme.Cards.Border = colors.OutlineVariant;
        theme.SnackBars.Background = colors.InverseSurface;
        theme.TextBoxes.FilledBackground = colors.SurfaceContainerHighest;
        theme.TextBoxes.HoverBackground = colors.SurfaceContainerHigh;
        theme.TextBoxes.OutlineBorder = colors.Outline;
        theme.TextBoxes.OutlineInactiveBorder = colors.Outline;
        theme.ComboBoxes.FilledBackground = colors.SurfaceContainerHighest;
        theme.ComboBoxes.OutlineBorder = colors.Outline;
        theme.Chips.OutlineBorder = colors.Outline;
        new PaletteHelper().SetTheme(theme);
        SwapTokens(dark);
        Applied?.Invoke(dark);
    }

    private static void SwapTokens(bool dark)
    {
        var app = Application.Current;
        if (app is null)
            return;

        var merged = app.Resources.MergedDictionaries;
        var uri = new Uri(dark
            ? "pack://application:,,,/Themes/StrateDark.xaml"
            : "pack://application:,,,/Themes/StrateLight.xaml", UriKind.Absolute);
        var next = new ResourceDictionary { Source = uri };
        _ = next["Brush.Surface"];

        for (var i = 0; i < merged.Count; i++)
        {
            if (merged[i].Contains("Strate.TokenSet"))
            {
                merged[i] = next;
                return;
            }
        }

        merged.Add(next);
    }

    public static void WatchSystem(Action reapply)
    {
        try
        {
            SystemEvents.UserPreferenceChanged += (_, args) =>
            {
                if (args.Category != UserPreferenceCategory.General || Preference != "System")
                    return;
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher is null)
                    return;
                dispatcher.Invoke(reapply);
            };
        }
        catch (Exception ex)
        {
            Log.Write(ex);
        }
    }
}

public static class CommonRoots
{
    public static IReadOnlyList<string> All()
    {
        var list = new List<string>();
        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            try
            {
                var full = Path.GetFullPath(path);
                if (Directory.Exists(full) && !list.Contains(full, StringComparer.OrdinalIgnoreCase))
                    list.Add(full);
            }
            catch
            {
                // Un dossier spécial inaccessible n'est pas proposé.
            }
        }

        Add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        Add(Native.ShellInterop.DownloadsFolder());
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                    Add(drive.RootDirectory.FullName);
            }
        }
        catch
        {
            // DriveInfo peut échouer sur un volume en cours de montage.
        }

        return list;
    }
}
