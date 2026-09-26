using System.Buffers;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Strate.Services;

public static class DuplicateFinder
{
    public const int FileCap = 300_000;

    public static IReadOnlyList<FileRow> Find(string root, IProgress<ScanTick>? progress, CancellationToken ct, out bool truncated, out int seen)
    {
        root = Path.GetFullPath(root);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("Dossier introuvable.");

        truncated = false;
        seen = 0;
        var bySize = new Dictionary<long, List<string>>();
        var stack = new Stack<string>();
        stack.Push(root);
        var lastReport = Environment.TickCount64;

        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var dir = stack.Pop();
            IEnumerable<string> entries;
            try { entries = Directory.EnumerateFileSystemEntries(dir); }
            catch { continue; }

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
                if (seen > FileCap)
                {
                    truncated = true;
                    break;
                }

                var length = SpaceAnalyzer.TryLength(entry);
                if (length > 0)
                {
                    if (!bySize.TryGetValue(length, out var group))
                    {
                        group = new List<string>(1);
                        bySize[length] = group;
                    }
                    group.Add(entry);
                }

                var now = Environment.TickCount64;
                if (progress is not null && now - lastReport > 250)
                {
                    lastReport = now;
                    progress.Report(new ScanTick(seen, "Inventaire · " + entry));
                }
            }

            if (truncated)
                break;
        }

        var candidates = bySize.Where(pair => pair.Value.Count > 1).ToList();
        var partial = new ConcurrentDictionary<string, ConcurrentBag<string>>();
        var options = new ParallelOptions
        {
            CancellationToken = ct,
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount)
        };

        Parallel.ForEach(candidates, options, pair =>
        {
            foreach (var file in pair.Value)
            {
                ct.ThrowIfCancellationRequested();
                if (!TryHash(file, pair.Key, partial: true, out var hash))
                    continue;
                partial.GetOrAdd(pair.Key + ":" + hash, _ => new ConcurrentBag<string>()).Add(file);
            }
        });

        var fullGroups = new ConcurrentDictionary<string, ConcurrentBag<string>>();
        Parallel.ForEach(partial.Where(pair => pair.Value.Count > 1), options, pair =>
        {
            foreach (var file in pair.Value)
            {
                ct.ThrowIfCancellationRequested();
                var length = SpaceAnalyzer.TryLength(file);
                if (!TryHash(file, length, partial: false, out var hash))
                    continue;
                fullGroups.GetOrAdd(length + ":" + hash, _ => new ConcurrentBag<string>()).Add(file);
            }
        });

        var rows = new List<FileRow>();
        foreach (var pair in fullGroups.Where(p => p.Value.Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1))
        {
            var files = pair.Value.Distinct(StringComparer.OrdinalIgnoreCase).Select(path =>
            {
                DateTime modified;
                try { modified = File.GetLastWriteTimeUtc(path); }
                catch { modified = DateTime.MinValue; }
                return (Path: path, Modified: modified, Length: SpaceAnalyzer.TryLength(path));
            }).ToList();

            var keeper = files
                .OrderByDescending(file => file.Modified)
                .ThenBy(file => file.Path.Length)
                .ThenBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
                .First();

            foreach (var file in files)
            {
                rows.Add(new FileRow
                {
                    FullPath = file.Path,
                    Name = Path.GetFileName(file.Path),
                    Length = file.Length,
                    ModifiedUtc = file.Modified,
                    GroupKey = pair.Key,
                    GroupCount = files.Count,
                    IsSuggested = !file.Path.Equals(keeper.Path, StringComparison.OrdinalIgnoreCase)
                });
            }
        }

        rows.Sort((a, b) =>
        {
            var group = string.Compare(a.GroupKey, b.GroupKey, StringComparison.Ordinal);
            if (group != 0)
                return group;
            return a.IsSuggested.CompareTo(b.IsSuggested);
        });

        progress?.Report(new ScanTick(seen, root));
        return rows;
    }

    private static bool TryHash(string path, long length, bool partial, out string hash)
    {
        hash = "";
        var rented = ArrayPool<byte>.Shared.Rent(partial ? 65536 : 1024 * 1024);
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536, FileOptions.SequentialScan);
            using var incremental = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            if (partial)
            {
                var read = stream.Read(rented, 0, 65536);
                incremental.AppendData(rented, 0, read);
                if (length > 65536)
                {
                    stream.Seek(Math.Max(0, length - 65536), SeekOrigin.Begin);
                    read = stream.Read(rented, 0, 65536);
                    incremental.AppendData(rented, 0, read);
                }
            }
            else
            {
                int read;
                while ((read = stream.Read(rented, 0, rented.Length)) > 0)
                    incremental.AppendData(rented, 0, read);
            }

            incremental.AppendData(BitConverter.GetBytes(length));
            hash = Convert.ToHexString(incremental.GetHashAndReset());
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
