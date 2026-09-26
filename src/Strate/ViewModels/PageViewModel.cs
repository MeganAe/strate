using MaterialDesignThemes.Wpf;

namespace Strate.ViewModels;

public interface IPageHost
{
    void NotifySelected(PageViewModel page);
    void GoTo<T>() where T : PageViewModel;
}

public abstract partial class PageViewModel : ObservableObject
{
    public IPageHost? Host { get; set; }
    public abstract string Title { get; }
    public abstract string Subtitle { get; }
    public abstract PackIconKind Icon { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Prêt";

    partial void OnIsSelectedChanged(bool value)
    {
        if (!value)
            return;
        Host?.NotifySelected(this);
        OnActivated();
    }

    partial void OnIsBusyChanged(bool value) => OnBusyChanged();

    protected virtual void OnActivated()
    {
    }

    protected virtual void OnBusyChanged()
    {
    }

    protected void Fail(Exception ex)
    {
        if (Errors.IsCancel(ex))
        {
            StatusText = "Opération annulée.";
            return;
        }

        Log.Write(ex);
        StatusText = "Échec : " + ex.Message;
        Services.AppMessenger.Say(StatusText);
    }
}
