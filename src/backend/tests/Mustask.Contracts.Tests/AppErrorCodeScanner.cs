using System.Reflection;
using Mustask.Shared.Errors;

namespace Mustask.Contracts.Tests;

/// <summary>Один код приложения, найденный рефлексией — код плюс где он объявлен (для сообщений об ошибке).</summary>
internal sealed record AppErrorCode(string Code, string DeclaringType);

/// <summary>
/// Находит «коды приложения» — <c>public const string</c> в классах,
/// помеченных <see cref="ErrorCodesAttribute"/> (ADR-0005, п. 5). Чистая функция над переданными сборками — не привязана к
/// тому, что сегодня в <c>src/</c> нет ни одного слайса, поэтому
/// проверяется отдельно на синтетической сборке
/// (<see cref="RulesAreNotVacuous"/>), а не только на реальном (сегодня
/// пустом) множестве.
/// </summary>
internal static class AppErrorCodeScanner
{
    public static IReadOnlyList<AppErrorCode> Scan(params IEnumerable<Assembly> assemblies)
    {
        var result = new List<AppErrorCode>();

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (!type.IsClass || type.GetCustomAttribute<ErrorCodesAttribute>() is null)
                {
                    continue;
                }

                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                {
                    if (field.FieldType != typeof(string) || !field.IsLiteral)
                    {
                        continue;
                    }

                    var value = (string?)field.GetRawConstantValue();

                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    result.Add(new AppErrorCode(value, $"{type.FullName}.{field.Name}"));
                }
            }
        }

        return result;
    }
}
