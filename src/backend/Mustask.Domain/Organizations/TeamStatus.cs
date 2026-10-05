namespace Mustask.Domain.Organizations;

/// <summary>A team's lifecycle (ADR-0026 rule 1): <see cref="Deleted"/> is terminal. Stored as text.</summary>
public enum TeamStatus
{
    Active,
    Deleted,
}
