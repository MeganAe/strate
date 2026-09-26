using System.Windows.Data;

namespace Strate.ViewModels;

public partial class CleanupViewModel : PageViewModel
{
    private readonly AppSettings _settings;
    private CancellationTokenSource? _cts;
    private bool _started;

    public CleanupViewModel(AppSettings settings)
    {
        _settings = settings;
        Categories = new ObservableCollection<CleanupCategory>(CleanupService.CreateCatalog());
        CategoriesView = CollectionViewSource.GetDefaultView(Categories);
        CategoriesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(CleanupCategory.Group)));
    }

    public override string Title => "Nettoyage";
    public override string Subtitle => "Seules les cases cochées sont traitées, après une nouvelle mesure et une confirmation. Les documents, le dossier Windows et les profils de navigateur ne font pas partie des cibles.";
    public override PackIconKind Icon => PackIconKind.Broom;

    public ObservableCollection<CleanupCategory> Categories { get; }
    public ICollectionView CategoriesView { get; }

    protected override void OnActivated()
    {
        if (_started)
            return;
        _started = true;
        _ = MeasureAsync();
    }

    protected override void OnBusyChanged()
    {
        MeasureCommand.NotifyCanExecuteChanged();
        CleanCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    private static bool ShouldRecycle(CleanupCategory category, bool permanentTemp)
    {
        if (category.Id is "recent" or "dumps")
            return true;
        if (category.Id is "user-temp" or "windows-temp")
            return !permanentTemp;
        return !category.PermanentByDefault;
    }

    private bool CanStart() => !IsBusy;
    private bool CanCancel() => IsBusy;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task MeasureAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        IsBusy = true;
        StatusText = "Mesure des cibles…";
        try
        {
            await Task.Run(() =>
            {
                var snapshot = Categories.ToList();
                Parallel.ForEach(snapshot, new ParallelOptions { MaxDegreeOfParallelism = 3, CancellationToken = ct }, category =>
                {
                    var result = CleanupService.Measure(category, ct);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        category.Bytes = result.Bytes;
                        category.FileCount = result.Count;
                        category.Status = result.Status;
                    });
                });
            }, ct);
            var total = Categories.Where(category => category.IsChecked).Sum(category => category.Bytes);
            StatusText = "Mesure terminée. Sélection : " + ByteFormat.Format(total) + ".";
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CheckRecommended()
    {
        foreach (var category in Categories)
            category.IsChecked = category.Group == "Recommandé";
    }

    [RelayCommand]
    private void UncheckAll()
    {
        foreach (var category in Categories)
            category.IsChecked = false;
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task CleanAsync()
    {
        var selected = Categories.Where(category => category.IsChecked).ToList();
        if (selected.Count == 0)
        {
            AppMessenger.Say("Cochez au moins une cible.");
            return;
        }

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        IsBusy = true;
        StatusText = "Vérification avant nettoyage…";
        try
        {
            var plans = new List<(CleanupCategory Category, MeasureResult Result)>();
            foreach (var category in selected)
            {
                ct.ThrowIfCancellationRequested();
                var result = await Task.Run(() => CleanupService.Measure(category, ct), ct);
                category.Bytes = result.Bytes;
                category.FileCount = result.Count;
                category.Status = result.Status;
                plans.Add((category, result));
            }

            var actionable = plans.Where(plan => plan.Result.Bytes > 0 || plan.Category.IsRecycleBin && plan.Result.Count > 0).ToList();
            if (actionable.Count == 0)
            {
                StatusText = "Rien à nettoyer dans la sélection.";
                AppMessenger.Say(StatusText);
                return;
            }

            var bytes = actionable.Sum(plan => plan.Result.Bytes);
            var lines = string.Join("\n", actionable.Select(plan => "· " + plan.Category.Title + " — " + ByteFormat.Format(plan.Result.Bytes)));
            var body = lines + "\n\nTotal : " + ByteFormat.Format(bytes) + ".";
            body += _settings.PermanentTempDelete
                ? "\n\nLes fichiers temporaires et les caches sont supprimés définitivement. La corbeille, si elle est cochée, est vidée."
                : "\n\nLes fichiers temporaires seront envoyés à la corbeille. Les caches cochés restent supprimés définitivement.";
            body += "\nLes fichiers encore ouverts, et ceux de moins de 15 minutes dans les dossiers temporaires, sont ignorés.";

            var ok = await Strate.Views.Dialogs.ConfirmAsync("Nettoyer la sélection ?", body, "Nettoyer", true);
            if (!ok)
            {
                StatusText = "Nettoyage annulé.";
                return;
            }

            long freed = 0;
            var deleted = 0;
            var locked = 0;
            foreach (var plan in actionable)
            {
                ct.ThrowIfCancellationRequested();
                StatusText = "Nettoyage · " + plan.Category.Title;
                if (plan.Category.IsRecycleBin)
                {
                    var before = plan.Result.Bytes;
                    var emptied = await Task.Run(CleanupService.EmptyRecycleBin, ct);
                    if (emptied)
                    {
                        freed += before;
                        deleted += plan.Result.Count;
                        plan.Category.Bytes = 0;
                        plan.Category.FileCount = 0;
                        plan.Category.Status = "Vidée";
                    }
                    else
                    {
                        locked++;
                        plan.Category.Status = "Échec";
                    }
                    continue;
                }

                var recycle = ShouldRecycle(plan.Category, _settings.PermanentTempDelete);
                var outcome = await Task.Run(() => CleanupService.Delete(plan.Result.Files, recycle, plan.Category.ApplyGrace, ct), ct);
                freed += outcome.Bytes;
                deleted += outcome.Deleted;
                locked += outcome.SkippedLocked;
                plan.Category.Status = outcome.Deleted > 0 ? "Nettoyé" : "Inchangé";
                var fresh = CleanupService.Measure(plan.Category, ct);
                plan.Category.Bytes = fresh.Bytes;
                plan.Category.FileCount = fresh.Count;
            }

            var message = $"{ByteFormat.Format(freed)} traités, {deleted:N0} éléments.";
            if (locked > 0)
                message += $" {locked:N0} ignorés car utilisés ou inaccessibles.";
            StatusText = message;
            AppState.LastCleanup = message + " · " + DateTime.Now.ToString("g", CultureInfo.GetCultureInfo("fr-FR"));
            AppMessenger.Say(message);
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
