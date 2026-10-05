namespace Mustask.Domain.Organizations;

/// <summary>An organization's lifecycle (ADR-0026 rule 1). Stored as text.</summary>
public enum OrganizationStatus
{
    Active,
    Deactivated,
}
