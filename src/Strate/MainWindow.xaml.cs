using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Strate;

public partial class MainWindow : Window
{
    private IntPtr _hwnd;

    public MainWindow()
    {
        InitializeComponent();
        StateChanged += (_, _) => UpdateMaxIcon();
    }

    public void CenterOnCurrentMonitor()
    {
        if (WindowState == WindowState.Maximized)
            return;

        var workArea = SystemParameters.WorkArea;
        var width = Width > 0 ? Width : ActualWidth;
        var height = Height > 0 ? Height : ActualHeight;
        if (width <= 0 || height <= 0)
            return;

        Left = workArea.Left + (workArea.Width - width) / 2;
        Top = workArea.Top + (workArea.Height - height) / 2;
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        // UserPreferenceChanged ne couvre pas tous les changements de thème Windows 11.
        if (ThemeController.Preference == "System"
            && (Theme.GetSystemTheme() == BaseTheme.Dark) != ThemeController.IsDark)
        {
            ThemeController.Apply("System");
        }

        if (_hwnd != IntPtr.Zero)
            ApplyDwm(ThemeController.IsDark);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
        ApplyDwm(ThemeController.IsDark);
        ThemeController.Applied += OnThemeApplied;
    }

    protected override void OnClosed(EventArgs e)
    {
        ThemeController.Applied -= OnThemeApplied;
        base.OnClosed(e);
    }

    private void OnThemeApplied(bool dark)
    {
        if (_hwnd == IntPtr.Zero)
            return;
        // Apply est déjà sur le thread UI : Invoke y bloquerait le dispatcher.
        if (Dispatcher.CheckAccess())
            ApplyDwm(dark);
        else
            Dispatcher.BeginInvoke(() => ApplyDwm(dark));
    }

    private void ApplyDwm(bool dark)
    {
        var useDark = dark ? 1 : 0;
        DwmSetWindowAttribute(_hwnd, 20, ref useDark, sizeof(int));
        var corner = 2;
        DwmSetWindowAttribute(_hwnd, 33, ref corner, sizeof(int));
    }

    private void UpdateMaxIcon()
    {
        MaxIcon.Kind = WindowState == WindowState.Maximized ? PackIconKind.WindowRestore : PackIconKind.WindowMaximize;
    }

    private void MinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int wmGetMinMaxInfo = 0x0024;
        if (msg == wmGetMinMaxInfo)
        {
            WmGetMinMaxInfo(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var monitor = MonitorFromWindow(hwnd, 2);
        if (monitor == IntPtr.Zero)
            return;
        var info = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info))
            return;

        var work = info.rcWork;
        var bounds = info.rcMonitor;
        mmi.ptMaxPosition.x = work.left - bounds.left;
        mmi.ptMaxPosition.y = work.top - bounds.top;
        mmi.ptMaxSize.x = work.right - work.left;
        mmi.ptMaxSize.y = work.bottom - work.top;
        Marshal.StructureToPtr(mmi, lParam, true);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point ptReserved;
        public Point ptMaxSize;
        public Point ptMaxPosition;
        public Point ptMinTrackSize;
        public Point ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public int dwFlags;
    }
}
