namespace Mustask.Architecture.Tests;

/// <summary>
/// ADR-0022 Decision 6: <c>Mustask.Api/Features/Session/</c>
/// depends on nothing in <c>Mustask.Domain</c>/<c>Mustask.Application</c>, and nothing
/// outside it depends on it — the whole contour can move into a gateway
/// later by moving the folder, and every resource-server route keeps
/// validating Bearer tokens exactly as it does today. Checked by source
/// (not by assembly), the same technique as
/// <see cref="OnionDependencyRules"/>/<see cref="VerticalSliceRules"/>.
/// </summary>
public sealed class SessionIsolationRules
{
    private const string SessionFolder = "Features/Session/";

    /// <summary>
    /// (a) of the isolation rule. Referencing <c>Mustask.Api.Authentication</c>
    /// (<c>KeycloakOptions</c>, the shared <c>IConfigurationManager&lt;OpenIdConnectConfiguration&gt;</c>)
    /// or <c>Mustask.Api.Authorization</c> (<c>.Public(...)</c>) is not a
    /// violation — both live inside <c>Mustask.Api</c>, not in
    /// <c>Domain</c>/<c>Application</c>: the session
    /// contour would take these two small files with it (or a copy of them)
    /// on the day it moves out, which is a concern for that future move, not
    /// for this test.
    /// </summary>
    [Fact]
    public void Session_module_does_not_reference_domain_or_application()
    {
        string[] forbiddenNamespaces = ["Mustask.Domain", "Mustask.Application"];

        var violations = new List<string>();

        foreach (var file in SessionFiles())
        {
            foreach (var line in File.ReadLines(file.FullName))
            {
                var trimmed = line.TrimStart();

                if (!trimmed.StartsWith("using ", StringComparison.Ordinal))
                {
                    continue;
                }

                if (forbiddenNamespaces.Any(ns =>
                        trimmed.Contains(ns, StringComparison.Ordinal)))
                {
                    violations.Add($"{RelativePath(file)}: {trimmed.TrimEnd()}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Features/Session/ ссылается на Domain/Application:{Environment.NewLine}" +
            string.Join(Environment.NewLine, violations) + Environment.NewLine +
            "ADR-0022 Decision 6: модуль сессии не знает о домене вовсе.");
    }

    /// <summary>
    /// (б) of the isolation rule. The composition root (<c>Program.cs</c>) is
    /// deliberately excluded: it is the one file whose entire job is wiring
    /// every feature together (it already does the same for
    /// <c>Mustask.Api.Features.Dictionaries</c>)
    /// — the same exception <see cref="VerticalSliceRules"/> makes implicitly
    /// by only scanning inside <c>Mustask.Application/Features</c>, never
    /// <c>Program.cs</c>, for cross-slice references. What this guards
    /// against is a DIFFERENT resource-server slice reaching into the
    /// session module — that is the coupling ADR-0022 Decision 6 rules out.
    /// </summary>
    [Fact]
    public void Nothing_outside_the_session_module_references_it()
    {
        const string SessionNamespaceUsing = "using Mustask.Api.Features.Session";

        var violations = new List<string>();

        foreach (var file in RepositoryLayout.SourceFilesOf("Mustask.Api"))
        {
            var relative = RelativePath(file);

            if (relative.StartsWith(SessionFolder, StringComparison.Ordinal)
                || string.Equals(file.Name, "Program.cs", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var line in File.ReadLines(file.FullName))
            {
                if (line.TrimStart().StartsWith(SessionNamespaceUsing, StringComparison.Ordinal))
                {
                    violations.Add(relative);
                    break;
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Файлы вне Features/Session/ ссылаются на модуль сессии: {string.Join(", ", violations)}. " +
            "ADR-0022 Decision 6: ничто в resource-server половине не знает о существовании cookie.");
    }

    private static IEnumerable<FileInfo> SessionFiles() =>
        RepositoryLayout.SourceFilesOf("Mustask.Api")
            .Where(file => RelativePath(file).StartsWith(SessionFolder, StringComparison.Ordinal));

    private static string RelativePath(FileInfo file) =>
        Path.GetRelativePath(Path.Combine(RepositoryLayout.Source.FullName, "Mustask.Api"), file.FullName)
            .Replace('\\', '/');
}
