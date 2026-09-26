namespace Strate.Services;

public static class SpaceAnalyzer
{
    public static IReadOnlyList<FolderEntry> MeasureChildren(string root, IProgress<ScanTick>? progress, CancellationToken ct)
    {
        root = Path.GetFullPath(root);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("Dossier introuvable.");

        var children = new List<(string Path, bool Directory)>();
        foreach (var entry in Directory.EnumerateFileSystemEntries(root))
        {
            ct.ThrowIfCancellationRequested();
            if (!TryAttributes(entry, out var attr) || attr.HasFlag(FileAttributes.ReparsePoint))
                continue;
            children.Add((entry, attr.HasFlag(FileAttributes.Directory)));
        }

        var sizes = new long[children.Count];
        var files = 0;
        var gate = new object();
        var lastReport = Environment.TickCount64;
        var options = new ParallelOptions
        {
            CancellationToken = ct,
            MaxDegreeOfParallelism = Math.Max(1, Math.Min(4, Environment.ProcessorCount))
        };

        Parallel.For(0, children.Count, options, index =>
        {
            var (path, directory) = children[index];
            long size;
            int localFiles;
            if (directory)
                (size, localFiles) = MeasureTree(path, ct);
            else
            {
                size = TryLength(path);
                localFiles = 1;
            }

            sizes[index] = size;
            if (progress is null)
                return;

            lock (gate)
            {
                files += localFiles;
                var now = Environment.TickCount64;
                if (now - lastReport > 250)
                {
                    lastReport = now;
                    progress.Report(new ScanTick(files, path));
                }
            }
        });

        long total = 0;
        foreach (var size in sizes)
            total += size;

        var list = new List<FolderEntry>(children.Count);
        for (var i = 0; i < children.Count; i++)
        {
            var (path, directory) = children[i];
            var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
            list.Add(new FolderEntry
            {
                Name = string.IsNullOrEmpty(name) ? path : name,
                FullPath = path,
                IsDirectory = directory,
                Bytes = sizes[i],
                Fraction = total <= 0 ? 0 : (double)sizes[i] / total
            });
        }

        list.Sort((a, b) => b.Bytes.CompareTo(a.Bytes));
        progress?.Report(new ScanTick(files, root));
        return list;
    }

    private static (long Bytes, int Files) MeasureTree(string root, CancellationToken ct)
    {
        long bytes = 0;
        var files = 0;
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
                if (!TryAttributes(entry, out var attr) || attr.HasFlag(FileAttributes.ReparsePoint))
                    continue;
                if (attr.HasFlag(FileAttributes.Directory))
                {
                    stack.Push(entry);
                    continue;
                }

                bytes += TryLength(entry);
                files++;
            }
        }

        return (bytes, files);
    }

    internal static bool TryAttributes(string path, out FileAttributes attributes)
    {
        try
        {
            attributes = File.GetAttributes(path);
            return true;
        }
        catch
        {
            attributes = 0;
            return false;
        }
    }

    internal static long TryLength(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch
        {
            return 0;
        }
    }
}
