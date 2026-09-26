using System.Text.Json;
using Microsoft.Win32;

namespace Strate.Services;

public static class StartupService
{
    private const string DisabledRun = @"Software\Strate\DisabledRun";
    private static string StorePath => Path.Combine(SettingsStore.DirectoryPath, "startup-disabled.json");

    public static IReadOnlyList<StartupEntry> Load()
    {
        var list = new List<StartupEntry>();
        ReadRun(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "Session utilisateur", "HKCU", true, list);
        ReadRun(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "Tous les utilisateurs", "HKLM", false, list);
        ReadRun(Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "Tous les utilisateurs, 32 bits", "HKLM32", false, list);
        ReadDisabledRun(list);
        ReadFolder(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Dossier Démarrage", true, list);
        ReadFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Dossier Démarrage commun", false, list);
        ReadDisabledLinks(list);
        return list.OrderBy(entry => entry.Location, StringComparer.CurrentCultureIgnoreCase).ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static bool SetEnabled(StartupEntry entry, bool enable)
    {
        if (!entry.CanToggle)
            return false;

        try
        {
            if (entry.Scope is "HKCU" or "HKCU-OFF")
                return SetRunEnabled(entry, enable);
            if (entry.Scope is "LNK" or "LNK-OFF")
                return SetLinkEnabled(entry, enable);
            return false;
        }
        catch (Exception ex)
        {
            Log.Write(ex);
            return false;
        }
    }

    private static bool SetRunEnabled(StartupEntry entry, bool enable)
    {
        using var live = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        using var disabled = Registry.CurrentUser.CreateSubKey(DisabledRun, true);
        if (live is null || disabled is null)
            return false;

        if (enable)
        {
            var value = disabled.GetValue(entry.Name) as string ?? entry.Command;
            live.SetValue(entry.Name, value);
            disabled.DeleteValue(entry.Name, false);
            return true;
        }

        var current = live.GetValue(entry.Name) as string ?? entry.Command;
        disabled.SetValue(entry.Name, current);
        live.DeleteValue(entry.Name, false);
        return true;
    }

    private static bool SetLinkEnabled(StartupEntry entry, bool enable)
    {
        var map = LoadMap();
        if (enable)
        {
            var stored = map.FirstOrDefault(item => item.Name.Equals(entry.Name, StringComparison.OrdinalIgnoreCase));
            if (stored is null || !File.Exists(stored.StoredPath))
                return false;
            Directory.CreateDirectory(Path.GetDirectoryName(stored.OriginalPath)!);
            if (File.Exists(stored.OriginalPath))
                return false;
            File.Move(stored.StoredPath, stored.OriginalPath);
            map.Remove(stored);
            SaveMap(map);
            return true;
        }

        if (!File.Exists(entry.Command))
            return false;
        var storeDir = Path.Combine(SettingsStore.DirectoryPath, "DisabledStartup");
        Directory.CreateDirectory(storeDir);
        var storedPath = Path.Combine(storeDir, Path.GetFileName(entry.Command));
        if (File.Exists(storedPath))
            storedPath = Path.Combine(storeDir, Guid.NewGuid().ToString("N") + "-" + Path.GetFileName(entry.Command));
        File.Move(entry.Command, storedPath);
        map.Add(new StoredLink(entry.Name, entry.Command, storedPath));
        SaveMap(map);
        return true;
    }

    private static void ReadRun(RegistryKey hive, string subkey, string location, string scope, bool canToggle, List<StartupEntry> list)
    {
        try
        {
            using var key = hive.OpenSubKey(subkey, false);
            if (key is null)
                return;
            foreach (var name in key.GetValueNames())
            {
                var command = key.GetValue(name)?.ToString() ?? "";
                list.Add(new StartupEntry
                {
                    Name = name,
                    Command = command,
                    Location = location,
                    Scope = scope,
                    CanToggle = canToggle,
                    Enabled = true
                });
            }
        }
        catch
        {
            // HKLM peut être illisible sans élévation.
        }
    }

    private static void ReadDisabledRun(List<StartupEntry> list)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(DisabledRun, false);
            if (key is null)
                return;
            foreach (var name in key.GetValueNames())
            {
                list.Add(new StartupEntry
                {
                    Name = name,
                    Command = key.GetValue(name)?.ToString() ?? "",
                    Location = "Désactivé par Strate",
                    Scope = "HKCU-OFF",
                    CanToggle = true,
                    Enabled = false
                });
            }
        }
        catch (Exception ex)
        {
            Log.Write(ex);
        }
    }

    private static void ReadFolder(string folder, string location, bool canToggle, List<StartupEntry> list)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return;
        try
        {
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                if (Path.GetExtension(file).Equals(".ini", StringComparison.OrdinalIgnoreCase))
                    continue;
                list.Add(new StartupEntry
                {
                    Name = Path.GetFileName(file),
                    Command = file,
                    Location = location,
                    Scope = canToggle ? "LNK" : "LNK-RO",
                    CanToggle = canToggle,
                    Enabled = true
                });
            }
        }
        catch (Exception ex)
        {
            Log.Write(ex);
        }
    }

    private static void ReadDisabledLinks(List<StartupEntry> list)
    {
        foreach (var item in LoadMap())
        {
            if (!File.Exists(item.StoredPath))
                continue;
            list.Add(new StartupEntry
            {
                Name = item.Name,
                Command = item.OriginalPath,
                Location = "Désactivé par Strate",
                Scope = "LNK-OFF",
                CanToggle = true,
                Enabled = false,
                StoreKey = item.StoredPath
            });
        }
    }

    private static List<StoredLink> LoadMap()
    {
        try
        {
            if (!File.Exists(StorePath))
                return [];
            return JsonSerializer.Deserialize<List<StoredLink>>(File.ReadAllText(StorePath)) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static void SaveMap(List<StoredLink> map)
    {
        Directory.CreateDirectory(SettingsStore.DirectoryPath);
        File.WriteAllText(StorePath, JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed record StoredLink(string Name, string OriginalPath, string StoredPath);
}
