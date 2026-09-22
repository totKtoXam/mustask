namespace Mustask.Application.Abstractions;

/// <summary>Абстракция текущего времени — тестируемость сценариев с истечением/грейс-окном.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
