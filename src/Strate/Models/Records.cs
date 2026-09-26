using MaterialDesignThemes.Wpf;

namespace Strate.Models;

public sealed class VolumeRow
{
    public required string Root { get; init; }
    public required string Title { get; init; }
    public required string FormatName { get; init; }
    public required string Kind { get; init; }
    public long Total { get; init; }
    public long Free { get; init; }
    public bool IsSystem { get; init; }
    public long Used => Math.Max(0, Total - Free);
    public double UsedPercent => Total <= 0 ? 0 : Used * 100.0 / Total;
    public bool IsLow => UsedPercent >= 90;
    public string PercentText => UsedPercent.ToString("0.0", Fr) + " %";
    public string Summary => $"{Services.ByteFormat.Format(Used)} utilisés sur {Services.ByteFormat.Format(Total)}";
    public string FreeText => Services.ByteFormat.Format(Free) + " libres";
    public string Meta => $"{FormatName} · {Kind}" + (IsSystem ? " · système" : "");
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
}

public sealed class FolderEntry
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public bool IsDirectory { get; init; }
    public long Bytes { get; init; }
    public double Fraction { get; init; }
    public string SizeText => Services.ByteFormat.Format(Bytes);
    public string PercentText => (Fraction * 100).ToString("0.0", CultureInfo.GetCultureInfo("fr-FR")) + " %";
    public double BarWidth => Math.Clamp(Fraction, 0, 1) * 140;
    public string KindText => IsDirectory ? "Dossier" : "Fichier";
    public PackIconKind Icon => IsDirectory ? PackIconKind.Folder : PackIconKind.File;
}

public sealed record Crumb(string Name, string FullPath);

public partial class FileRow : ObservableObject
{
    public required string FullPath { get; init; }
    public required string Name { get; init; }
    public long Length { get; init; }
    public DateTime ModifiedUtc { get; init; }
    public string GroupKey { get; init; } = "";
    public bool IsSuggested { get; init; }
    public int GroupCount { get; init; }
    public string SizeText => Services.ByteFormat.Format(Length);
    public string ModifiedText => ModifiedUtc.ToLocalTime().ToString("g", CultureInfo.GetCultureInfo("fr-FR"));
    public string SuggestionText => string.IsNullOrEmpty(GroupKey) ? "" : IsSuggested ? "Doublon" : "À conserver";
    public string DirectoryText => Path.GetDirectoryName(FullPath) ?? FullPath;

    [ObservableProperty]
    private bool _isChecked;
}

public partial class SizeOption : ObservableObject
{
    public SizeOption(string label, long bytes, bool active)
    {
        Label = label;
        Bytes = bytes;
        _isActive = active;
    }

    public string Label { get; }
    public long Bytes { get; }
    public Action<SizeOption>? OnChosen { get; set; }

    [ObservableProperty]
    private bool _isActive;

    partial void OnIsActiveChanged(bool value)
    {
        if (value)
            OnChosen?.Invoke(this);
    }
}

public partial class ChoiceOption : ObservableObject
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public Action<ChoiceOption>? OnChosen { get; set; }

    [ObservableProperty]
    private bool _isActive;

    partial void OnIsActiveChanged(bool value)
    {
        if (value)
            OnChosen?.Invoke(this);
    }
}

public partial class StartupEntry : ObservableObject
{
    public required string Name { get; init; }
    public required string Command { get; init; }
    public required string Location { get; init; }
    public required string Scope { get; init; }
    public bool CanToggle { get; init; }
    public string StoreKey { get; init; } = "";

    [ObservableProperty]
    private bool _enabled;

    public string StateText => Enabled ? "Actif" : "Désactivé";
    public string ToggleText => CanToggle ? (Enabled ? "Désactiver" : "Activer") : "Lecture seule";

    partial void OnEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(ToggleText));
    }
}

public sealed class DiskRow
{
    public required string Model { get; init; }
    public required string InterfaceName { get; init; }
    public required string Media { get; init; }
    public required string Status { get; init; }
    public string? Serial { get; init; }
    public long Size { get; init; }
    public string SizeText => Services.ByteFormat.Format(Size);
    public bool IsOk => Status.Equals("OK", StringComparison.OrdinalIgnoreCase);
}

public sealed record ScanTick(int Files, string Detail);

public sealed record MeasureResult(long Bytes, int Count, string Status, IReadOnlyList<string> Files);

public sealed record CleanupOutcome(int Deleted, int SkippedLocked, int SkippedRecent, int Failed, long Bytes, IReadOnlyList<string> Errors);

public partial class CleanupCategory : ObservableObject
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Group { get; init; }
    public bool PermanentByDefault { get; init; }
    public bool ApplyGrace { get; init; }
    public bool IsRecycleBin { get; init; }
    public Func<IEnumerable<string>>? Roots { get; init; }
    public string? Pattern { get; init; }
    public bool Recursive { get; init; } = true;

    [ObservableProperty]
    private bool _isChecked;

    [ObservableProperty]
    private long _bytes;

    [ObservableProperty]
    private int _fileCount;

    [ObservableProperty]
    private string _status = "Non mesuré";

    public string SizeText => Services.ByteFormat.Format(Bytes);
    public string CountText => FileCount <= 1
        ? $"{FileCount.ToString("N0", CultureInfo.GetCultureInfo("fr-FR"))} élément"
        : $"{FileCount.ToString("N0", CultureInfo.GetCultureInfo("fr-FR"))} éléments";

    partial void OnBytesChanged(long value) => OnPropertyChanged(nameof(SizeText));
    partial void OnFileCountChanged(int value) => OnPropertyChanged(nameof(CountText));
}
