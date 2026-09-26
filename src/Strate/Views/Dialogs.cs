namespace Strate.Views;

public static class Dialogs
{
    public static async Task<bool> ConfirmAsync(string title, string body, string confirm, bool destructive)
    {
        var dialog = new ConfirmDialog(title, body, confirm, destructive);
        var result = await DialogHost.Show(dialog, "RootDialog");
        return result is bool value && value
            || result is string text && text.Equals("True", StringComparison.OrdinalIgnoreCase);
    }
}
