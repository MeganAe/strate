using System.Windows.Threading;

namespace Strate;

public partial class App : Application
{
    private Mutex? _mutex;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                Log.Write(ex);
        };

        _mutex = new Mutex(true, @"Local\Strate.SingleInstance", out _ownsMutex);
        if (!_ownsMutex)
        {
            MessageBox.Show("Strate est déjà en cours d'exécution.", BrandInfo.Name, MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        AppMessenger.Initialize();
        var settings = SettingsStore.Load();
        ThemeController.Apply(settings.Theme);
        ThemeController.WatchSystem(() =>
        {
            if (ThemeController.Preference == "System")
                ThemeController.Apply("System");
        });

        var window = new MainWindow
        {
            DataContext = new ViewModels.MainViewModel(settings)
        };
        MainWindow = window;
        window.Show();
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsMutex)
        {
            try { _mutex?.ReleaseMutex(); }
            catch { /* déjà relâché */ }
        }
        _mutex?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs args)
    {
        Log.Write(args.Exception);
        MessageBox.Show(
            "Une erreur inattendue s'est produite. Le détail est dans le journal Strate.\n\n" + args.Exception.Message,
            BrandInfo.Name,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        args.Handled = true;
    }
}
