using Mustask.Api.IntegrationTests.Features.Organizations;
using System.Net;
using System.Text.Json;
using Mustask.Testing;
using Xunit.Sdk;

namespace Mustask.Api.IntegrationTests.Kit;

/// <summary>The kit's own logic (tech-035): the assertions fail when they should, not only pass. No Docker, no host.</summary>
[Trait("Category", "Integration")]
public sealed class KitAssertionsTests
{
    private static Answer AnswerOf(HttpStatusCode status, string json) =>
        new(status, null, "application/json", JsonDocument.Parse(json).RootElement.Clone());

    private static string Quoted(string text) => JsonSerializer.Serialize(text);

    private static Answer NameTaken(string detail, string paramType = "string") => AnswerOf(
        HttpStatusCode.Conflict,
        $$"""{ "errorCode": "organizations.organization.name_taken", "detail": {{Quoted(detail)}}, "params": { "name": { "value": "Альфа", "type": "{{paramType}}" } } }""");

    // The detail is the catalog's template with the parameter substituted, read from contracts/errors, not from the test.
    [Fact]
    public void ExpectedText_ReadsTheCatalogAndSubstitutesParameters()
    {
        Assert.Equal("Название «Альфа» уже занято", ProblemAssert.ExpectedText("organizations.organization.name_taken", ("name", "Альфа")));
        Assert.Equal("Название должно содержать от 1 до 100 символов", ProblemAssert.ExpectedText("organizations.name_length"));
    }

    [Fact]
    public void ExpectedText_UnknownCode_Throws() =>
        Assert.Throws<KeyNotFoundException>(() => ProblemAssert.ExpectedText("organizations.no_such_code"));

    [Fact]
    public void ExpectedText_MissingParameter_Fails() =>
        Assert.ThrowsAny<XunitException>(() => ProblemAssert.ExpectedText("organizations.organization.name_taken"));

    [Fact]
    public void Problem_MatchingAnswer_Passes() =>
        ProblemAssert.Problem(
            NameTaken("Название «Альфа» уже занято"), HttpStatusCode.Conflict, "organizations.organization.name_taken", ("name", "Альфа"));

    [Fact]
    public void Problem_DetailNotFromTheCatalog_Fails() =>
        Assert.ThrowsAny<XunitException>(() => ProblemAssert.Problem(
            NameTaken("Название «Альфа» занято"), HttpStatusCode.Conflict, "organizations.organization.name_taken", ("name", "Альфа")));

    [Fact]
    public void Problem_WrongStatusOrCode_Fails()
    {
        var answer = NameTaken("Название «Альфа» уже занято");

        Assert.ThrowsAny<XunitException>(() => ProblemAssert.Problem(answer, HttpStatusCode.BadRequest, "organizations.organization.name_taken", ("name", "Альфа")));
        Assert.ThrowsAny<XunitException>(() => ProblemAssert.Problem(answer, HttpStatusCode.Conflict, "organizations.project.name_taken"));
    }

    [Fact]
    public void Problem_ParameterOfAnotherType_Fails() =>
        Assert.ThrowsAny<XunitException>(() => ProblemAssert.Problem(
            NameTaken("Название «Альфа» уже занято", "number"), HttpStatusCode.Conflict, "organizations.organization.name_taken", ("name", "Альфа")));

    [Fact]
    public void Problem_UnexpectedParams_Fails() =>
        Assert.ThrowsAny<XunitException>(() => ProblemAssert.Problem(
            AnswerOf(
                HttpStatusCode.BadRequest,
                """{ "errorCode": "organizations.validation_failed", "detail": "Проверьте правильность заполнения полей.", "params": { "x": { "value": "y", "type": "string" } } }"""),
            HttpStatusCode.BadRequest, "organizations.validation_failed"));

    [Fact]
    public void Problem_NumberOfAnotherValue_Fails()
    {
        var answer = AnswerOf(
            HttpStatusCode.BadRequest,
            """{ "errorCode": "organizations.validation_failed", "detail": "Проверьте правильность заполнения полей.", "params": { "limit": { "value": 5, "type": "number" } } }""");

        Assert.ThrowsAny<XunitException>(() => ProblemAssert.Problem(
            answer, HttpStatusCode.BadRequest, "organizations.validation_failed", ("limit", 6)));
    }

    private static Answer LengthRefusal(string reasonCode, string detail) => AnswerOf(
        HttpStatusCode.BadRequest,
        $$"""{ "errorCode": "organizations.validation_failed", "detail": "Проверьте правильность заполнения полей.", "errors": { "name": [ { "code": {{Quoted(reasonCode)}}, "detail": {{Quoted(detail)}} } ] } }""");

    [Fact]
    public void Validation_MatchingAnswer_Passes() =>
        ProblemAssert.Validation(
            LengthRefusal("organizations.name_length", "Название должно содержать от 1 до 100 символов"),
            "organizations.validation_failed", "name", "organizations.name_length");

    [Fact]
    public void Validation_ReasonTextNotFromTheCatalog_Fails() =>
        Assert.ThrowsAny<XunitException>(() => ProblemAssert.Validation(
            LengthRefusal("organizations.name_length", "Слишком длинное название"),
            "organizations.validation_failed", "name", "organizations.name_length"));

    [Fact]
    public void Validation_OtherField_Fails() =>
        Assert.ThrowsAny<Exception>(() => ProblemAssert.Validation(
            LengthRefusal("organizations.name_length", "Название должно содержать от 1 до 100 символов"),
            "organizations.validation_failed", "description", "organizations.name_length"));

    [Fact]
    public void ResponseFields_SameNamesInAnyOrder_Passes() =>
        ResponseFields.AssertExactly(AnswerOf(HttpStatusCode.OK, """{ "name": "a", "id": "b" }"""), "id", "name");

    [Fact]
    public void ResponseFields_ExtraField_FailsNamingIt()
    {
        var failure = Assert.ThrowsAny<XunitException>(() =>
            ResponseFields.AssertExactly(AnswerOf(HttpStatusCode.OK, """{ "id": "b", "name": "a", "secret": 1 }"""), "id", "name"));

        Assert.Contains("unexpected: [secret]", failure.Message);
    }

    [Fact]
    public void ResponseFields_MissingField_FailsNamingIt()
    {
        var failure = Assert.ThrowsAny<XunitException>(() =>
            ResponseFields.AssertExactly(AnswerOf(HttpStatusCode.OK, """{ "id": "b" }"""), "id", "name"));

        Assert.Contains("Missing: [name]", failure.Message);
    }

    [Fact]
    public void ResponseFields_BodyThatIsNotAnObject_Fails() =>
        Assert.ThrowsAny<XunitException>(() => ResponseFields.AssertExactly(AnswerOf(HttpStatusCode.OK, "[]"), "id"));

    [Fact]
    public void TestData_UniqueName_IsUniqueAndAValidName()
    {
        var first = TestData.UniqueName("Проект");

        Assert.NotEqual(first, TestData.UniqueName("Проект"));
        Assert.Equal(first, TestData.Name(first).Value);
    }
}
