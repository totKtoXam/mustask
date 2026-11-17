namespace Mustask.Architecture.Tests;

/// <summary>ADR-0045 decision 2, plan-backend § 5: only the Processing folder of the image intake names the image library.</summary>
public sealed class ImageLibraryBoundaryRules
{
    private const string Allowed = "Mustask.Infrastructure/Images/Intake/Processing/";

    private static readonly string[] Layers = ["Mustask.Shared", "Mustask.Domain", "Mustask.Application", "Mustask.Infrastructure", "Mustask.Api"];

    [Fact]
    public void NetVips_is_named_only_in_the_intake_processing_folder()
    {
        var violations = new List<string>();

        foreach (var file in Layers.SelectMany(RepositoryLayout.SourceFilesOf))
        {
            var relative = Path.GetRelativePath(RepositoryLayout.Source.FullName, file.FullName).Replace('\\', '/');

            if (!relative.StartsWith(Allowed, StringComparison.Ordinal)
                && File.ReadAllText(file.FullName).Contains("NetVips", StringComparison.Ordinal))
            {
                violations.Add(relative);
            }
        }

        Assert.True(violations.Count == 0, "NetVips is named outside " + Allowed + ": " + string.Join(", ", violations));
    }

    [Fact]
    public void NetVips_packages_are_referenced_only_by_Infrastructure()
    {
        foreach (var layer in Layers.Where(layer => layer != "Mustask.Infrastructure"))
        {
            Assert.DoesNotContain(RepositoryLayout.PackageReferencesOf(layer), name => name.StartsWith("NetVips", StringComparison.Ordinal));
        }
    }
}
