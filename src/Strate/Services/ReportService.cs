using Strate.Brand;

namespace Strate.Services;

public static class ReportService
{
    public static string Export()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Strate", "Rapports");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"strate-{DateTime.Now:yyyyMMdd-HHmm}.html");
        File.WriteAllText(path, Build());
        return path;
    }

    public static string Folder()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Strate", "Rapports");
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static string Build()
    {
        var volumes = string.Join("", AppState.Volumes.Select(volume =>
            $"<tr><td>{E(volume.Title)}</td><td>{E(volume.Summary)}</td><td>{E(volume.FreeText)}</td><td>{E(volume.PercentText)}</td></tr>"));
        if (string.IsNullOrEmpty(volumes))
            volumes = "<tr><td colspan=\"4\">Aucun volume lu.</td></tr>";

        return $$"""
            <!DOCTYPE html>
            <html lang="fr">
            <head>
            <meta charset="utf-8">
            <title>Rapport Strate</title>
            <style>
              body { margin: 0; background: #F5FAFD; color: #171C1F; font: 16px/24px "Segoe UI", sans-serif; }
              main { max-width: 840px; margin: 0 auto; padding: 40px 24px 64px; }
              h1 { font-weight: 500; font-size: 32px; line-height: 40px; margin: 16px 0 8px; }
              h2 { font-weight: 500; font-size: 22px; margin: 32px 0 8px; }
              p, td, th { font-size: 14px; line-height: 20px; }
              .muted { color: #40484C; }
              table { width: 100%; border-collapse: collapse; }
              th, td { text-align: left; padding: 10px 8px; border-bottom: 1px solid #BFC8CC; }
              th { color: #40484C; font-weight: 500; }
              .mark { width: 32px; height: 32px; background: #06677F; border-radius: 7px; }
            </style>
            </head>
            <body>
            <main>
              <div class="mark"></div>
              <h1>Rapport {{BrandInfo.Name}}</h1>
              <p class="muted">{{DateTime.Now:dddd d MMMM yyyy, HH:mm}} · {{BrandInfo.Tagline}}</p>
              <h2>Volumes</h2>
              <table>
                <thead><tr><th>Volume</th><th>Occupation</th><th>Libre</th><th>Part</th></tr></thead>
                <tbody>{{volumes}}</tbody>
              </table>
              <h2>Dernières lectures</h2>
              <p>Analyse : {{E(AppState.AnalysisPath ?? "aucune")}} · {{ByteFormat.Format(AppState.AnalysisBytes)}} sur {{AppState.AnalysisEntries}} éléments.</p>
              <p>Gros fichiers : {{E(AppState.LargeFilePath ?? "aucun")}} · {{AppState.LargeFileCount}} fichiers, {{ByteFormat.Format(AppState.LargeFileBytes)}}.</p>
              <p>Doublons : {{E(AppState.DuplicatePath ?? "aucun")}} · {{AppState.DuplicateGroups}} groupes, {{ByteFormat.Format(AppState.DuplicateWaste)}} récupérables.</p>
              <p>Nettoyage : {{E(AppState.LastCleanup ?? "aucun nettoyage dans cette session.")}}</p>
              <p>Démarrage : {{AppState.StartupCount}} entrées lues.</p>
              <p>Disques : {{E(AppState.DiskSummary ?? "non lu.")}}</p>
              <p class="muted">Strate ne supprime rien sans confirmation. Les tailles sont en octets binaires, comme dans l'Explorateur.</p>
            </main>
            </body>
            </html>
            """;
    }

    private static string E(string value) => System.Net.WebUtility.HtmlEncode(value);
}
