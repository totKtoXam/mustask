namespace Mustask.Testing;

/// <summary>
/// Where the repository's files are, for the tests that read them (the error catalog, the realm import, the sources). The root
/// is the directory with <c>Mustask.sln</c> above the test binaries. Compiled into every test project by
/// <c>tests/Directory.Build.props</c> (tech-035); the older per-project copies stay until phase 2.
/// </summary>
internal static class RepositoryLayout
{
    private static readonly Lazy<DirectoryInfo> RootLazy = new(FindRoot);

    public static DirectoryInfo Root => RootLazy.Value;

    public static DirectoryInfo Source => new(Path.Combine(Root.FullName, "src", "backend"));

    public static DirectoryInfo Frontend => new(Path.Combine(Root.FullName, "src", "frontend"));

    /// <summary>The error catalog's source files: <c>contracts/errors/</c>.</summary>
    public static DirectoryInfo ErrorCatalog => new(Path.Combine(Root.FullName, "contracts", "errors"));

    /// <summary>A path under the repository root, from segments.</summary>
    public static string PathUnderRoot(params string[] segments) => Path.Combine([Root.FullName, .. segments]);

    private static DirectoryInfo FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Mustask.sln")))
        {
            dir = dir.Parent;
        }

        return dir ?? throw new InvalidOperationException($"The repository root (Mustask.sln) is not above {AppContext.BaseDirectory}.");
    }
}
