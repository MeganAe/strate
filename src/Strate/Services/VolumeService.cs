namespace Strate.Services;

public static class VolumeService
{
    public static IReadOnlyList<VolumeRow> GetVolumes()
    {
        var system = Path.GetPathRoot(Environment.SystemDirectory) ?? "";
        var rows = new List<VolumeRow>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady)
                continue;
            if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable))
                continue;

            try
            {
                var label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "Disque local" : drive.VolumeLabel;
                rows.Add(new VolumeRow
                {
                    Root = drive.Name,
                    Title = $"{label} ({drive.Name.TrimEnd('\\')})",
                    FormatName = string.IsNullOrWhiteSpace(drive.DriveFormat) ? "Inconnu" : drive.DriveFormat,
                    Kind = drive.DriveType == DriveType.Removable ? "Amovible" : "Fixe",
                    Total = drive.TotalSize,
                    Free = drive.AvailableFreeSpace,
                    IsSystem = drive.Name.Equals(system, StringComparison.OrdinalIgnoreCase)
                });
            }
            catch
            {
                // Volume retiré entre l'énumération et la lecture.
            }
        }

        return rows
            .OrderByDescending(row => row.IsSystem)
            .ThenBy(row => row.Root, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
