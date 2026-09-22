using Mustask.Infrastructure.Conventions;

namespace Mustask.Infrastructure.Tests.Conventions;

// Сам конвертер ToSnakeCase до этого
// проверялся только косвенно — через имена в модели EF. Здесь он зафиксирован
// напрямую, чтобы любое изменение регулярного выражения было заметно сразу.
// Поведение на аббревиатурах (HTTPClient -> httpclient) намеренно НЕ
// фиксируется тестом: это открытый дефект, тест не должен
// цементировать его.
public sealed class SnakeCaseConverterTests
{
    [Theory]
    [InlineData("TaskComment", "task_comment")]
    [InlineData("TaskId", "task_id")]
    [InlineData("Id", "id")]
    [InlineData("Orders", "orders")]
    public void ToSnakeCase_PascalCaseIdentifier_SplitsOnWordBoundaries(string input, string expected) =>
        Assert.Equal(expected, input.ToSnakeCase());

    [Theory]
    [InlineData("Address2", "address2")]
    [InlineData("Sprint2024", "sprint2024")]
    [InlineData("Iso3Code", "iso3_code")]
    public void ToSnakeCase_IdentifierWithDigits_DoesNotSplitInsideToken(string input, string expected) =>
        Assert.Equal(expected, input.ToSnakeCase());

    [Theory]
    [InlineData("task_comment")]
    [InlineData("id")]
    [InlineData("_")]
    [InlineData("")]
    public void ToSnakeCase_AlreadySnakeCase_IsIdempotent(string input)
    {
        var once = input.ToSnakeCase();

        Assert.Equal(input, once);
        Assert.Equal(once, once.ToSnakeCase());
    }
}
