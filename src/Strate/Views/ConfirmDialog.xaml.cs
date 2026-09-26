namespace Strate.Views;

public partial class ConfirmDialog : UserControl
{
    public ConfirmDialog(string title, string body, string confirm, bool destructive)
    {
        InitializeComponent();
        TitleBlock.Text = title;
        BodyBlock.Text = body;
        ConfirmButton.Content = confirm;
        if (destructive)
            ConfirmButton.SetResourceReference(ForegroundProperty, "Brush.Error");
    }
}
