using System.Runtime.InteropServices;

namespace Strate.Native;

internal static class ShellInterop
{
    public static string? DownloadsFolder()
    {
        var id = new Guid("374DE290-123F-4565-9164-39C4925E467B");
        if (SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out var ptr) != 0 || ptr == IntPtr.Zero)
            return null;
        try
        {
            return Marshal.PtrToStringUni(ptr);
        }
        finally
        {
            Marshal.FreeCoTaskMem(ptr);
        }
    }

    public static (long Bytes, long Items)? QueryRecycleBin()
    {
        var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
        var hr = SHQueryRecycleBin(null, ref info);
        if (hr != 0)
            return null;
        return (Math.Max(0, info.i64Size), Math.Max(0, info.i64NumItems));
    }

    public static bool EmptyRecycleBin()
    {
        const uint noConfirmation = 0x00000001;
        const uint noSound = 0x00000004;
        var hr = SHEmptyRecycleBin(IntPtr.Zero, null, noConfirmation | noSound);
        return hr == 0 || hr == unchecked((int)0x8000FFFF) /* E_UNEXPECTED when already empty on some builds */;
    }

    public static void Reveal(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        var args = File.Exists(path) ? $"/select,\"{path}\"" : $"\"{path}\"";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", args)
        {
            UseShellExecute = true
        });
    }

    public static void Open(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path) && !File.Exists(path))
            return;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
    }

    public static void SendToRecycleBin(string path)
    {
        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
            path,
            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(ref Guid rfid, int dwFlags, IntPtr hToken, out IntPtr ppszPath);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    private struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }
}
