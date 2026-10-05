namespace Mustask.Domain.Organizations;

/// <summary>A project's lifecycle (ADR-0026 rule 1). Stored as text.</summary>
public enum ProjectStatus
{
    Active,
    Deactivated,
}
