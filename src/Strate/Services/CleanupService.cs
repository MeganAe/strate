using Strate.Native;

namespace Strate.Services;

public static class CleanupService
{
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(15);

    public static List<CleanupCategory> CreateCatalog()
    {
        var list = new List<CleanupCategory>();
        var userTemp = Path.GetFullPath(Path.GetTempPath());
        var windowsTemp = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"));
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        list.Add(Category("user-temp", "Fichiers temporaires", "Dossier Temp de votre session. Les fichiers modifiés dans les 15 dernières minutes sont conservés.", "Recommandé", true, true, true, () => [userTemp]));
        if (!userTemp.Equals(windowsTemp, StringComparison.OrdinalIgnoreCase) && Directory.Exists(windowsTemp))
        {
            list.Add(Category("windows-temp", "Temp Windows", "C:\\Windows\\Temp, si le dossier est accessible. Même délai de grâce de 15 minutes.", "Recommandé", true, true, true, () => [windowsTemp]));
        }

        list.Add(new CleanupCategory
        {
            Id = "recycle",
            Title = "Corbeille",
            Description = "Vider la corbeille libère réellement l'espace. Cette action est définitive.",
            Group = "Recommandé",
            IsChecked = true,
            IsRecycleBin = true,
            PermanentByDefault = true
        });

        list.Add(Category("edge", "Cache Microsoft Edge", "Caches des profils Edge. Les mots de passe, cookies et historique ne sont pas touchés.", "Caches", false, true, true, () => BrowserCaches(Path.Combine(local, @"Microsoft\Edge\User Data"))));
        list.Add(Category("chrome", "Cache Google Chrome", "Caches des profils Chrome. Le profil lui-même est conservé.", "Caches", false, true, true, () => BrowserCaches(Path.Combine(local, @"Google\Chrome\User Data"))));
        list.Add(Category("firefox", "Cache Firefox", "Dossiers cache2 des profils Firefox.", "Caches", false, true, true, () => FirefoxCaches(local)));
        list.Add(Category("d3d", "Cache de shaders DirectX", "Se régénère à l'usage. Peut ralentir le premier lancement de certains jeux.", "Caches", false, true, true, () => [Path.Combine(local, "D3DSCache")]));
        list.Add(new CleanupCategory
        {
            Id = "thumbs",
            Title = "Miniatures de l'Explorateur",
            Description = "Fichiers thumbcache et iconcache. Souvent verrouillés tant que l'Explorateur est ouvert.",
            Group = "Caches",
            IsChecked = false,
            PermanentByDefault = true,
            ApplyGrace = false,
            Recursive = false,
            Pattern = "*.db",
            Roots = () => [Path.Combine(local, @"Microsoft\Windows\Explorer")]
        });

        list.Add(Category("wer", "Rapports d'erreurs Windows", "Archives WER locales. Utile seulement si vous n'en avez plus besoin.", "Caches", false, true, false, () =>
        [
            Path.Combine(local, @"Microsoft\Windows\WER\ReportArchive"),
            Path.Combine(local, @"Microsoft\Windows\WER\ReportQueue")
        ]));
        list.Add(new CleanupCategory
        {
            Id = "recent",
            Title = "Raccourcis récents",
            Description = "Uniquement les raccourcis .lnk. Les fichiers pointés ne sont pas supprimés. Envoi à la corbeille.",
            Group = "Caches",
            IsChecked = false,
            PermanentByDefault = false,
            ApplyGrace = false,
            Recursive = false,
            Pattern = "*.lnk",
            Roots = () => [Path.Combine(roaming, @"Microsoft\Windows\Recent")]
        });
        list.Add(Category("dumps", "Journaux de plantage", "Dossier CrashDumps. Envoyé à la corbeille, pas détruit.", "Caches", false, false, false, () => [Path.Combine(local, "CrashDumps")]));

        list.Add(Category("nuget", "Cache HTTP NuGet", "Cache de téléchargement NuGet, pas les paquets déjà restaurés.", "Développeur", false, true, false, () => [Path.Combine(local, @"NuGet\v3-cache")]));
        list.Add(Category("npm", "Cache npm", "Cache de paquets npm. Les projets installés ne sont pas modifiés.", "Développeur", false, true, false, () => [Path.Combine(local, "npm-cache")]));
        list.Add(Category("pip", "Cache pip", "Cache de téléchargement pip.", "Développeur", false, true, false, () => [Path.Combine(local, @"pip\Cache")]));
        return list;
    }

    public static MeasureResult Measure(CleanupCategory category, CancellationToken ct)
    {
        if (category.IsRecycleBin)
        {
            var bin = ShellInterop.QueryRecycleBin();
            if (bin is null)
                return new MeasureResult(0, 0, "Indisponible", []);
            return new MeasureResult(bin.Value.Bytes, (int)Math.Min(int.MaxValue, bin.Value.Items), "Mesuré", []);
        }

        var files = new List<string>();
        var skippedRecent = 0;
        foreach (var root in ExistingRoots(category))
            Collect(root, category, files, ref skippedRecent, ct);

        long bytes = 0;
        foreach (var file in files)
            bytes += SpaceAnalyzer.TryLength(file);

        var status = files.Count == 0
            ? skippedRecent > 0 ? "Rien d'assez ancien" : "Vide ou absent"
            : "Mesuré";
        return new MeasureResult(bytes, files.Count, status, files);
    }

    public static CleanupOutcome Delete(IReadOnlyList<string> files, bool recycle, bool applyGrace, CancellationToken ct)
    {
        var deleted = 0;
        var locked = 0;
        var recent = 0;
        var failed = 0;
        long bytes = 0;
        var errors = new List<string>();
        var cutoff = DateTime.UtcNow - Grace;

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (!File.Exists(file))
                    continue;
                if (applyGrace && File.GetLastWriteTimeUtc(file) > cutoff)
                {
                    recent++;
                    continue;
                }

                var length = SpaceAnalyzer.TryLength(file);
                if (recycle)
                    ShellInterop.SendToRecycleBin(file);
                else
                    File.Delete(file);
                deleted++;
                bytes += length;
            }
            catch (IOException)
            {
                locked++;
            }
            catch (UnauthorizedAccessException)
            {
                locked++;
            }
            catch (Exception ex)
            {
                failed++;
                if (errors.Count < 6)
                    errors.Add(Path.GetFileName(file) + " : " + ex.Message);
            }
        }

        return new CleanupOutcome(deleted, locked, recent, failed, bytes, errors);
    }

    public static bool EmptyRecycleBin() => ShellInterop.EmptyRecycleBin();

    private static CleanupCategory Category(
        string id,
        string title,
        string description,
        string group,
        bool checkedByDefault,
        bool permanent,
        bool grace,
        Func<IEnumerable<string>> roots)
    {
        return new CleanupCategory
        {
            Id = id,
            Title = title,
            Description = description,
            Group = group,
            IsChecked = checkedByDefault,
            PermanentByDefault = permanent,
            ApplyGrace = grace,
            Recursive = true,
            Roots = roots
        };
    }

    private static IEnumerable<string> ExistingRoots(CleanupCategory category)
    {
        if (category.Roots is null)
            yield break;
        foreach (var root in category.Roots())
        {
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
                yield return root;
        }
    }

    private static void Collect(string root, CleanupCategory category, List<string> files, ref int skippedRecent, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow - Grace;
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var dir = stack.Pop();
            IEnumerable<string> entries;
            try
            {
                entries = string.IsNullOrEmpty(category.Pattern)
                    ? Directory.EnumerateFileSystemEntries(dir)
                    : Directory.EnumerateFiles(dir, category.Pattern);
            }
            catch
            {
                continue;
            }

            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();
                if (!SpaceAnalyzer.TryAttributes(entry, out var attr) || attr.HasFlag(FileAttributes.ReparsePoint))
                    continue;
                if (attr.HasFlag(FileAttributes.Directory))
                {
                    if (category.Recursive && string.IsNullOrEmpty(category.Pattern))
                        stack.Push(entry);
                    continue;
                }

                if (category.Id == "thumbs")
                {
                    var name = Path.GetFileName(entry);
                    if (!name.StartsWith("thumbcache_", StringComparison.OrdinalIgnoreCase)
                        && !name.StartsWith("iconcache_", StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                if (category.ApplyGrace)
                {
                    try
                    {
                        if (File.GetLastWriteTimeUtc(entry) > cutoff)
                        {
                            skippedRecent++;
                            continue;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                files.Add(entry);
            }

            if (!category.Recursive)
                break;
        }
    }

    private static IEnumerable<string> BrowserCaches(string userData)
    {
        if (!Directory.Exists(userData))
            yield break;
        IEnumerable<string> profiles;
        try { profiles = Directory.EnumerateDirectories(userData); }
        catch { yield break; }

        foreach (var profile in profiles)
        {
            var name = Path.GetFileName(profile);
            if (!name.Equals("Default", StringComparison.OrdinalIgnoreCase)
                && !name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase))
                continue;
            yield return Path.Combine(profile, "Cache");
            yield return Path.Combine(profile, "Code Cache");
        }
    }

    private static IEnumerable<string> FirefoxCaches(string local)
    {
        var profiles = Path.Combine(local, @"Mozilla\Firefox\Profiles");
        if (!Directory.Exists(profiles))
            yield break;
        IEnumerable<string> dirs;
        try { dirs = Directory.EnumerateDirectories(profiles); }
        catch { yield break; }
        foreach (var dir in dirs)
            yield return Path.Combine(dir, "cache2");
    }
}
