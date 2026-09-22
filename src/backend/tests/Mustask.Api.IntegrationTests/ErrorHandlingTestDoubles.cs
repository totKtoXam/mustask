using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mustask.Api.IntegrationTests;

/// <summary>
/// Тестовые двойники для unit-уровневых тестов над отдельными
/// <c>IExceptionHandler</c> (без поднятия полного хоста/Testcontainers):
/// перехватывают уровень логирования и итоговый <c>ProblemDetails</c>,
/// которые через <c>WebApplicationFactory</c> напрямую не видны иначе, чем
/// через реальный <c>ILoggerProvider</c> (см.
/// <see cref="CapturingLoggerProvider"/>).
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, Exception? Exception, string Message)> Entries { get; } = [];

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add((logLevel, exception, formatter(state, exception)));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}

internal sealed class FakeProblemDetailsService : IProblemDetailsService
{
    public ProblemDetailsContext? LastContext { get; private set; }

    /// <summary>
    /// Результат, который вернёт <see cref="TryWriteAsync"/> — управляемый,
    /// а не захардкоженный <c>true</c>: реальный
    /// <c>DefaultProblemDetailsWriter</c> возвращает <c>false</c>, если не
    /// может согласовать content type с заголовком <c>Accept</c> запроса,
    /// и обработчики обязаны вести себя корректно
    /// в этом случае тоже.
    /// </summary>
    public bool WriteResult { get; init; } = true;

    public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
    {
        LastContext = context;
        return ValueTask.FromResult(WriteResult);
    }

    public ValueTask WriteAsync(ProblemDetailsContext context)
    {
        LastContext = context;
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Настоящий <see cref="ILoggerProvider"/>, подключаемый в тестовый хост
/// через <c>ConfigureTestServices</c> — в отличие от <see cref="CapturingLogger{T}"/>
/// (используется только в юнит-тестах над отдельным обработчиком, в обход
/// DI), он подтверждает, что события реально доходят от
/// <c>ILogger&lt;T&gt;</c> обработчика через Serilog (<c>writeToProviders: true</c>,
/// см. Program.cs) до провайдера — то есть ловит именно регрессию в этой цепочке, а не
/// только правильный вызов метода логгера в коде.
/// </summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<(string Category, LogLevel Level, string Message)> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new ProviderLogger(categoryName, this);

    public void Dispose()
    {
    }

    private sealed class ProviderLogger : ILogger
    {
        private readonly string _category;
        private readonly CapturingLoggerProvider _owner;

        public ProviderLogger(string category, CapturingLoggerProvider owner)
        {
            _category = category;
            _owner = owner;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _owner.Entries.Enqueue((_category, logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}

internal sealed class FakeHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Production;

    public string ApplicationName { get; set; } = "Mustask.Api.IntegrationTests";

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
