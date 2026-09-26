using System.Management;

namespace Strate.Services;

public static class DiskHealthService
{
    public static (IReadOnlyList<DiskRow> Disks, string Smart) Query()
    {
        var disks = new List<DiskRow>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Model, InterfaceType, Size, Status, MediaType, SerialNumber FROM Win32_DiskDrive");
            foreach (var item in searcher.Get())
            {
                disks.Add(new DiskRow
                {
                    Model = Text(item, "Model", "Disque inconnu"),
                    InterfaceName = Text(item, "InterfaceType", "Interface inconnue"),
                    Media = Text(item, "MediaType", "Support inconnu"),
                    Status = Text(item, "Status", "Inconnu"),
                    Serial = item["SerialNumber"]?.ToString()?.Trim(),
                    Size = Long(item, "Size")
                });
            }
        }
        catch (Exception ex)
        {
            Log.Write(ex);
        }

        return (disks, QuerySmart());
    }

    private static string QuerySmart()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT PredictFailure, Reason FROM MSStorageDriver_FailurePredictStatus");
            var any = false;
            foreach (var item in searcher.Get())
            {
                any = true;
                var predict = item["PredictFailure"] is bool flag && flag;
                if (predict)
                    return "Défaillance prédite par SMART. Sauvegardez vos données.";
            }

            return any ? "SMART ne signale pas de défaillance prédite." : "SMART indisponible.";
        }
        catch
        {
            return "SMART indisponible sans élévation, ou non exposé par le pilote.";
        }
    }

    private static string Text(ManagementBaseObject item, string name, string fallback)
    {
        var value = item[name]?.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static long Long(ManagementBaseObject item, string name)
    {
        try
        {
            return Convert.ToInt64(item[name] ?? 0, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }
}
