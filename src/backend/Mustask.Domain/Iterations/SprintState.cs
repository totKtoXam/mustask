namespace Mustask.Domain.Iterations;

/// <summary>The lifecycle of a sprint, never backwards (BR-3): Planned, then Active, then Closed. Stored and served by name.</summary>
public enum SprintState
{
    Planned,
    Active,
    Closed,
}
