using Mustask.Shared.Errors;

namespace Mustask.Contracts.Tests;

/// <summary>
/// Живёт только для <see cref="RulesAreNotVacuous"/>: сегодня в
/// <c>src/</c> нет ни одного <c>[ErrorCodes]</c>-класса (ноль слайсов), а
/// значит <see cref="AppErrorCodeScanner"/> на реальных сборках отдаёт
/// пустой список — зелёный тест на пустом множестве неотличим от
/// сломанного сканера. Эта заглушка доказывает, что механизм сам по себе
/// работает, независимо от того, объявлен ли уже настоящий слайс.
/// </summary>
[ErrorCodes]
internal static class ScannerSelfTestFixture
{
    public const string SampleCode = "contracts_tests.self_check";

    // Не строка — не должна попасть в результат сканера.
    public static readonly string NotAConst = "contracts_tests.not_a_const";
}
