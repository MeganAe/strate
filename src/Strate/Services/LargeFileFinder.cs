namespace Strate.Services;

public static class LargeFileFinder
{
    public const int ResultCap = 2000;

    public static IReadOnlyList<FileRow> Find(string root, long minBytes, IProgress<ScanTick>? progress, CancellationToken ct, out bool capped)
    {
        root = Path.GetFullPath(root);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("Dossier introuvable.");

        var found = new List<FileRow>();
        var seen = 0;
        var lastReport = Environment.TickCount64;
        capped = false;
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var dir = stack.Pop();
            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(dir);
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
                    stack.Push(entry);
                    continue;
                }

                seen++;
                var length = SpaceAnalyzer.TryLength(entry);
                if (length >= minBytes)
                {
                    DateTime modified;
                    try { modified = File.GetLastWriteTimeUtc(entry); }
                    catch { modified = DateTime.MinValue; }
                    found.Add(new FileRow
                    {
                        FullPath = entry,
                        Name = Path.GetFileName(entry),
                        Length = length,
                        ModifiedUtc = modified
                    });
                    if (found.Count > ResultCap * 4)
                    {
                        found.Sort((a, b) => b.Length.CompareTo(a.Length));
                        found.RemoveRange(ResultCap, found.Count - ResultCap);
                        capped = true;
                    }
                }

                var now = Environment.TickCount64;
                if (progress is not null && now - lastReport > 250)
                {
                    lastReport = now;
                    progress.Report(new ScanTick(seen, entry));
                }
            }
        }

        found.Sort((a, b) => b.Length.CompareTo(a.Length));
        if (found.Count > ResultCap)
        {
            found.RemoveRange(ResultCap, found.Count - ResultCap);
            capped = true;
        }

        progress?.Report(new ScanTick(seen, root));
        return found;
    }
}
