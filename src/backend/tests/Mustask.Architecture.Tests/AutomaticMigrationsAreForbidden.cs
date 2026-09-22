namespace Mustask.Architecture.Tests;

// ADR-0002 requires that migrations are never applied
// automatically -- no Database.Migrate()/MigrateAsync()/EnsureCreated() call
// anywhere in production src/backend/ (see
// docs/adr/0002-explicit-database-migrations.md). That was previously
// verified by reading the code once, with nothing to catch a future
// regression, and the first version of this guard only scanned Program.cs --
// a call from AddInfrastructure, an IHostedService, or EnsureCreated()
// anywhere else in src/backend/ would have slipped through unnoticed.
// A source-text guard over the whole tree is the simplest
// thing that actually runs in CI: no Docker, no DbContext, just the source
// files' own text.
//
// The scan is scoped to production sources, excluding the
// top-level `tests/` tree in addition to the pre-existing `bin`/`obj`
// exclusions. Test fixtures are legitimately allowed to call
// Migrate()/EnsureCreated() -- MustaskApiFactory brings up the test database,
// MigrationsTests exercises the migrations themselves -- and this guard's own file necessarily quotes the
// forbidden literals in ForbiddenCalls below. Excluding the top-level
// `tests/` tree (docs/conventions/backend.md, "Solution structure" -- every
// test project lives under `src/backend/tests/`, nowhere else) keeps the
// guard aimed at what ADR-0002 actually forbids: production code migrating
// itself, not tests that must set the schema up to run at all. The exclusion
// is anchored to that one, known directory rather than to any path segment
// literally named `tests` -- a future `Mustask.Infrastructure/tests/` fixture
// folder stays in scope and the guard would still catch a forbidden call
// inside it.
//
// Moved here from
// `Mustask.Api.IntegrationTests`, the one project `scripts/verify.sh` skips
// entirely when Docker is down -- exactly the configuration in which the
// original, narrower guard's blind spot went unnoticed. This is a pure
// source-text rule with no Docker/HTTP dependency, so `Mustask.Architecture.Tests`
// (docs/conventions/git.md, "Architecture tests") is its home, alongside
// `RulesAreNotVacuous`, which this file leans on for the same reason: a
// filesystem scan that quietly stops reading anything must fail loudly, not
// pass quietly. `FindOffenders` is extracted over an arbitrary root so both
// directions -- a forbidden call in a production path is caught, the same
// call under a `tests/` path is not -- are pinned by a hermetic test against
// a throwaway directory, not by a manual injection that does not survive the
// session.
public sealed class AutomaticMigrationsAreForbidden
{
    private static readonly string[] ForbiddenCalls = ["Migrate(", "MigrateAsync(", "EnsureCreated(", "EnsureCreatedAsync("];

    [Fact]
    public void ProductionSources_AnyCsFile_DoesNotCallDatabaseMigrateOrEnsureCreated()
    {
        var root = RepositoryLayout.Source.FullName;
        var scannedFiles = ProductionSourceFiles(root);

        // An empty enumeration would pass the assertion below exactly
        // like a clean tree -- this line is what tells the two apart.
        Assert.True(
            scannedFiles.Count > 0,
            $"The scan found zero production .cs files under {root} -- it is not reading the repository.");

        // A non-empty total does not catch a *partial* narrowing -- an
        // exclusion that drops one whole project (e.g. Mustask.Api, 35 files)
        // still leaves hundreds of files from the rest, and the check above
        // stays green. One load-bearing, unlikely-to-be-renamed file per
        // Onion layer (docs/conventions/backend.md, "Solution structure")
        // pins that each layer specifically is still being read, not just
        // that some files somewhere are.
        var expectedLayerFiles = new[]
        {
            Path.Combine(root, "Mustask.Shared", "Exceptions", "MustaskException.cs"),
            // A long-lived domain type: an anchor deleted by an unrelated
            // feature change would fail this guard for a reason that has
            // nothing to do with migrations.
            Path.Combine(root, "Mustask.Domain", "Dictionaries", "Language.cs"),
            Path.Combine(root, "Mustask.Application", "DependencyInjection.cs"),
            Path.Combine(root, "Mustask.Infrastructure", "MustaskDbContext.cs"),
            Path.Combine(root, "Mustask.Api", "Program.cs"),
        };

        foreach (var expected in expectedLayerFiles)
        {
            Assert.True(
                scannedFiles.Contains(expected),
                $"{expected} is not in the scanned set -- its layer dropped out of the scan (renamed, moved, or newly excluded).");
        }

        var offenders = FindOffenders(root);

        Assert.True(
            offenders.Count == 0,
            "Found forbidden migration/EnsureCreated call(s) in: " + string.Join(", ", offenders));
    }

    // Hermetic proof of both directions in one sandbox: a throwaway
    // directory, not the
    // repository and not a manual edit that has to be remembered and
    // reverted. Both files carry the identical forbidden call, one under a
    // production path and one under the top-level `tests/` tree, and the
    // assertion is that the result is exactly the production file --
    // `Assert.Empty` over a sandbox holding only the test-shaped file would
    // have passed just as well if `FindOffenders` returned empty for any
    // reason at all (the vacuity guarded against above, in the small).
    [Fact]
    public void FindOffenders_ProductionFileIsReported_SameCallUnderTopLevelTestsTreeIsNot()
    {
        using var sandbox = new TemporarySourceTree();
        var offendingFile = sandbox.WriteFile(
            Path.Combine("Mustask.FakeProduction", "Worker.cs"),
            "await db.Database.MigrateAsync();");
        sandbox.WriteFile(
            Path.Combine("tests", "Mustask.FakeProduction.Tests", "Worker.cs"),
            "await db.Database.MigrateAsync();");

        var offenders = FindOffenders(sandbox.Root);

        Assert.Equal([offendingFile], offenders);
    }

    // Exercises the classifying logic directly instead of relying on the
    // filesystem scan above to happen to catch or miss a case -- this also
    // removes the need for a special case around the guard's own file: a
    // path under the top-level `tests/` tree is excluded because it is a
    // test path, not because of whose file it is.
    //
    // Includes the near-miss cases the classifier's own name promises to
    // handle: `tests-support` and `Protests` do not
    // match the anchored `tests` segment, and -- now that the exclusion is
    // anchored to the top-level tree rather than to any segment named
    // `tests` anywhere -- a nested `tests/` folder inside a production
    // project is no longer exempt either.
    [Theory]
    [InlineData(@"C:\repo\src\backend\Mustask.Api\Program.cs", true)]
    [InlineData(@"C:\repo\src\backend\Mustask.Infrastructure\MustaskDbContext.cs", true)]
    [InlineData(@"C:\repo\src\backend\Mustask.Application\Handlers\SomeHandler.cs", true)]
    [InlineData(@"C:\repo\src\backend\Mustask.Domain\Entities\SomeEntity.cs", true)]
    [InlineData(@"C:\repo\src\backend\tests\Mustask.Api.IntegrationTests\MustaskApiFactory.cs", false)]
    [InlineData(@"C:\repo\src\backend\tests\Mustask.Architecture.Tests\AutomaticMigrationsAreForbidden.cs", false)]
    [InlineData(@"C:\repo\src\backend\tests\Mustask.Infrastructure.Tests\MigrationsTests.cs", false)]
    [InlineData(@"C:\repo\src\backend\tests\Mustask.Infrastructure.Tests\Dictionaries\DictionarySeedFilesTests.cs", false)]
    [InlineData(@"C:\repo\src\backend\Mustask.Api\obj\Debug\net10.0\Program.cs", false)]
    [InlineData(@"C:\repo\src\backend\Mustask.Api\bin\Debug\net10.0\Program.cs", false)]
    [InlineData(@"C:\repo\src\backend\Mustask.Api\tests-support\Fixture.cs", true)]
    [InlineData(@"C:\repo\src\backend\Mustask.Api\Features\Protests\ProtestsHandler.cs", true)]
    [InlineData(@"C:\repo\src\backend\Mustask.Infrastructure\tests\SeedData.cs", true)]
    public void IsProductionSourcePath_ExcludesTopLevelTestsTreeAndBinObjOnly(string rawPath, bool expectedIsProduction)
    {
        var root = @"C:\repo\src\backend".Replace('\\', Path.DirectorySeparatorChar);
        var path = rawPath.Replace('\\', Path.DirectorySeparatorChar);

        Assert.Equal(expectedIsProduction, IsProductionSourcePath(root, path));
    }

    [Theory]
    [InlineData("db.Database.Migrate();")]
    [InlineData("await db.Database.MigrateAsync();")]
    [InlineData("db.Database.EnsureCreated();")]
    [InlineData("await db.Database.EnsureCreatedAsync();")]
    public void ContainsForbiddenCall_FlagsEachForbiddenLiteral(string source)
    {
        Assert.True(ContainsForbiddenCall(source));
    }

    [Fact]
    public void ContainsForbiddenCall_DoesNotFlagUnrelatedDatabaseCalls()
    {
        Assert.False(ContainsForbiddenCall("await db.Database.CanConnectAsync();"));
    }

    private static IReadOnlyList<string> ProductionSourceFiles(string root) =>
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => IsProductionSourcePath(root, path))
            .ToList();

    private static IReadOnlyList<string> FindOffenders(string root) =>
        ProductionSourceFiles(root)
            .Where(path => ContainsForbiddenCall(File.ReadAllText(path)))
            .ToList();

    private static bool IsProductionSourcePath(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');

        // All three exclusions are matched against the scan-root-relative
        // path, not the absolute one -- an absolute match would silently
        // empty the scan if the repository (or, for the hermetic tests, the
        // OS temp directory) happened to sit under a directory literally
        // named `bin` or `obj`. Bounding with a leading/trailing slash makes
        // "/obj/" and "/bin/" match a leading segment too (a project root
        // directly named `obj`), not only a nested one.
        var bounded = $"/{relative}/";

        return !bounded.Contains("/obj/", StringComparison.Ordinal)
            && !bounded.Contains("/bin/", StringComparison.Ordinal)
            && !relative.StartsWith("tests/", StringComparison.Ordinal);
    }

    private static bool ContainsForbiddenCall(string source) =>
        ForbiddenCalls.Any(call => source.Contains(call, StringComparison.Ordinal));

    /// <summary>
    /// A throwaway directory tree under the OS temp folder, deleted on
    /// dispose. Used only to give `FindOffenders_...` something real to read
    /// off disk without touching the repository itself.
    /// </summary>
    private sealed class TemporarySourceTree : IDisposable
    {
        public string Root { get; } = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "mustask-automigrate-guard-" + Guid.NewGuid().ToString("N"))).FullName;

        public string WriteFile(string relativePath, string contents)
        {
            var fullPath = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, contents);
            return fullPath;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
