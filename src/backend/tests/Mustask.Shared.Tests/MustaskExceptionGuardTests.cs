using System.Reflection;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Shared.Tests;

/// <summary>
/// Guard-тест рефлексией: страхует от будущего наследника
/// <see cref="MustaskException"/>, который приедет со статусом вне диапазона
/// HTTP-ошибок (0, 200 и т. п.) и превратится в битый ProblemDetails-ответ.
/// Обработчики в Mustask.Api берут статус из <see cref="MustaskException.StatusCode"/>
/// без switch по типам — этот тест единственная защита от такой опечатки.
/// </summary>
public sealed class MustaskExceptionGuardTests
{
    [Fact]
    public void MustaskExceptionSubclasses_AllHaveStatusCodeInHttpErrorRange()
    {
        var subclasses = typeof(MustaskException).Assembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(MustaskException).IsAssignableFrom(type))
            .ToList();

        Assert.NotEmpty(subclasses);

        var violations = new List<string>();

        foreach (var type in subclasses)
        {
            var instance = (MustaskException)CreateInstance(type);

            if (instance.StatusCode is < 400 or > 599)
            {
                violations.Add($"{type.Name}: StatusCode={instance.StatusCode}");
            }
        }

        Assert.True(
            violations.Count == 0,
            "Наследники MustaskException со статусом вне диапазона 400..599: " +
            string.Join(", ", violations));
    }

    /// <summary>Создаёт экземпляр наследника, подставляя заглушки под параметры конструктора.</summary>
    private static object CreateInstance(Type type)
    {
        var constructor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        var arguments = constructor.GetParameters()
            .Select(parameter => DummyValueFor(parameter.ParameterType))
            .ToArray();

        return constructor.Invoke(arguments)
            ?? throw new InvalidOperationException($"Не удалось создать экземпляр {type.Name}.");
    }

    private static object? DummyValueFor(Type parameterType)
    {
        if (parameterType == typeof(string))
        {
            return "test";
        }

        if (parameterType == typeof(Exception))
        {
            return null;
        }

        if (parameterType == typeof(ErrorParams))
        {
            // Необязательный параметр — MustaskException сам подставляет
            // пустой набор, когда получает null (см. ErrorParams.None).
            return null;
        }

        if (typeof(System.Collections.IDictionary).IsAssignableFrom(parameterType) ||
            parameterType.Name.Contains("IReadOnlyDictionary", StringComparison.Ordinal))
        {
            return new Dictionary<string, IReadOnlyList<FieldError>>();
        }

        return parameterType.IsValueType ? Activator.CreateInstance(parameterType) : null;
    }
}
