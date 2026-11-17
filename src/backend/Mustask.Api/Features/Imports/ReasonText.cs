using Mustask.Api.ErrorHandling.Catalog;
using Mustask.Application.Abstractions.Imports;
using Mustask.Shared.Errors;

namespace Mustask.Api.Features.Imports;

/// <summary>The Russian text of a dry-run reason or note from the error catalog (ADR-0006); a code the catalog lacks falls back to the code.</summary>
public sealed class ReasonText(ErrorCatalog catalog) : IReasonText
{
    public string Render(string code, IReadOnlyDictionary<string, string> parameters)
    {
        if (!catalog.TryGet(code, out _))
        {
            return code;
        }

        var values = new ErrorParams();

        foreach (var (name, value) in parameters)
        {
            values[name] = ErrorParam.Text(value);
        }

        return catalog.RenderRu(code, values).Text;
    }
}
