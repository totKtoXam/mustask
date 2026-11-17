using Mustask.Api.Features.Imports;
using Mustask.Domain.Imports;

namespace Mustask.Contracts.Tests.Imports;

/// <summary>ADR-0006: a dry-run reason is rendered in Russian from the error catalog; a code the catalog lacks stays the code.</summary>
public sealed class ReasonTextTests
{
    private static readonly ReasonText Reasons = new(CatalogFixture.Catalog);

    [Fact]
    public void A_catalogued_code_is_rendered_in_russian()
    {
        var text = Reasons.Render(ImportErrorCodes.FileTooLarge, new Dictionary<string, string>());

        Assert.NotEqual(ImportErrorCodes.FileTooLarge, text);
        Assert.Matches("[А-Яа-я]", text);
    }

    [Fact]
    public void A_code_the_catalog_lacks_falls_back_to_the_code()
    {
        Assert.Equal("imports.no.such.code", Reasons.Render("imports.no.such.code", new Dictionary<string, string> { ["x"] = "1" }));
    }
}
