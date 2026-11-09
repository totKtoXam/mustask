using Mustask.Api.IntegrationTests.Features.Organizations;
using System.Globalization;
using System.Net;
using Mustask.Shared.Errors;

namespace Mustask.Api.IntegrationTests.Kit;

/// <summary>
/// Assertions on an error answer that read the expected text from the error catalog (<c>contracts/errors/*.json</c>, the same
/// source the server renders from) instead of repeating the Russian string in the test (tech-035). When a wording is edited
/// in the catalog the tests follow; when a code is retired or renamed the test fails on the unknown code, which is the point.
/// </summary>
public static class ProblemAssert
{
    /// <summary>
    /// The answer is a problem: the status, the <c>errorCode</c>, the <c>detail</c> the catalog renders for the code with the given
    /// parameters, and exactly those parameters in <c>params</c>, each with the type the value implies. A parameter value is a
    /// string, a number, a <see cref="DateOnly"/> or a <see cref="DateTimeOffset"/>.
    /// </summary>
    public static void Problem(Answer answer, HttpStatusCode status, string code, params (string Name, object Value)[] parameters)
    {
        Assert.Equal(status, answer.Status);
        Assert.Equal(code, answer.ErrorCode);
        Assert.Equal(ExpectedText(code, parameters), answer.Detail);

        if (parameters.Length == 0)
        {
            return;
        }

        var sent = answer.Body.GetProperty("params");

        foreach (var (name, value) in parameters)
        {
            var expected = WireType(ToErrorParam(value).Type);
            var parameter = sent.GetProperty(name);
            Assert.Equal(expected, parameter.GetProperty("type").GetString());

            if (value is string text)
            {
                Assert.Equal(text, parameter.GetProperty("value").GetString());
            }
        }

        Assert.Equal(parameters.Length, sent.EnumerateObject().Count());
    }

    /// <summary>
    /// A 400 with <paramref name="validationCode"/> (the module's <c>*.validation_failed</c>) whose <c>errors[field]</c> holds
    /// exactly one reason, <paramref name="reasonCode"/>, with the catalog's text for it. The problem's own <c>detail</c> is the
    /// catalog's text for <paramref name="validationCode"/>.
    /// </summary>
    public static void Validation(Answer answer, string validationCode, string field, string reasonCode)
    {
        Problem(answer, HttpStatusCode.BadRequest, validationCode);

        var reason = Assert.Single(answer.Body.GetProperty("errors").GetProperty(field).EnumerateArray());
        Assert.Equal(reasonCode, reason.GetProperty("code").GetString());
        Assert.Equal(ExpectedText(reasonCode), reason.GetProperty("detail").GetString());
    }

    /// <summary>The catalog's Russian text for a code, the parameters substituted.</summary>
    public static string ExpectedText(string code, params (string Name, object Value)[] parameters)
    {
        var errorParams = new ErrorParams();

        foreach (var (name, value) in parameters)
        {
            errorParams[name] = ToErrorParam(value);
        }

        var rendered = ErrorCatalogTestData.RealProductCatalog.RenderRu(code, errorParams);
        Assert.False(rendered.HasMissingParams, $"The test gave no value for [{string.Join(", ", rendered.MissingParamNames)}] of «{code}».");

        return rendered.Text;
    }

    // The "type" hint the server writes next to each value (BusinessExceptionHandler.TypeHint).
    private static string WireType(ErrorParamType type) => type switch
    {
        ErrorParamType.Text => "string",
        ErrorParamType.Number => "number",
        ErrorParamType.Currency => "currency",
        ErrorParamType.Date => "date",
        ErrorParamType.DateTime => "datetime",
        _ => throw new NotSupportedException($"Unknown parameter type {type}."),
    };

    private static ErrorParam ToErrorParam(object value) => value switch
    {
        string text => ErrorParam.Text(text),
        DateOnly date => ErrorParam.Date(date),
        DateTimeOffset moment => ErrorParam.Moment(moment),
        int or long or short or byte or decimal or double or float =>
            ErrorParam.Number(Convert.ToDecimal(value, CultureInfo.InvariantCulture)),
        _ => throw new ArgumentException($"A problem parameter is a string, a number, a date or a moment, not {value.GetType().Name}.", nameof(value)),
    };
}
