using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;

namespace Mustask.Api.Logging;

/// <summary>
/// Читаемый в консоли формат для Development. Продовый дефолт (см.
/// <c>appsettings.json</c>, секция <c>Serilog</c>) — структурный JSON
/// (<c>CompactJsonFormatter</c>); <c>appsettings.Development.json</c>
/// переопределяет один лист-ключ <c>Serilog:WriteTo:0:Args:formatter</c> на
/// эту реализацию по её assembly-qualified имени.
/// </summary>
/// <remarks>
/// Serilog-конфигурация ASP.NET Core (<c>ReadFrom.Configuration</c>)
/// сливает <c>appsettings.json</c> и <c>appsettings.{Environment}.json</c>
/// по отдельным листовым ключам, а не заменяет JSON-массив (<c>WriteTo</c>)
/// целиком — известное ограничение конфигурации на базе JSON-провайдеров.
/// Если бы Development-файл задавал другой набор аргументов (например,
/// <c>outputTemplate</c> вместо <c>formatter</c>), оба набора аргументов
/// слились бы в один вызов <c>WriteTo.Console(...)</c>, для которого нет
/// перегрузки с обоими параметрами сразу — старт упал бы с "Unable to find
/// methods called Console for supplied arguments". Отдельный класс с
/// собственным именем сборки и типа как раз позволяет переопределить один и
/// тот же ключ (<c>formatter</c>) без коллизии — <see cref="MessageTemplateTextFormatter"/>
/// сам по себе для этого не годится: у него нет публичного конструктора без
/// параметров (шаблон вывода обязателен), поэтому строкой типа его не
/// сконфигурировать.
/// </remarks>
public sealed class DevelopmentConsoleFormatter : ITextFormatter
{
    private readonly MessageTemplateTextFormatter _inner = new(
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

    public void Format(LogEvent logEvent, TextWriter output) => _inner.Format(logEvent, output);
}
