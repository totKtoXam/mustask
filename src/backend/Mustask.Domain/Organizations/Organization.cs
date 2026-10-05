using Mustask.Domain.Process;

namespace Mustask.Domain.Organizations;

/// <summary>
/// An organization (US-1, US-2): the top container of M1's structure. Its name
/// is unique instance-wide among organizations of any status (BR-M1-1), it is
/// never visible without its process (BR-M1-3), and it is removed softly:
/// deactivated and restored exactly as it was, never purged (ADR-0026).
/// </summary>
public sealed class Organization
{
    private Organization()
    {
        // EF materialization.
    }

    private Organization(Guid id, EntityName name, ProcessId processId, DateTimeOffset createdAt, string createdBy)
    {
        Id = id;
        Name = name.Value;
        NormalizedName = name.Normalized;
        Status = OrganizationStatus.Active;
        ProcessId = processId;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string NormalizedName { get; private set; } = null!;

    public OrganizationStatus Status { get; private set; }

    /// <summary>mod-001's process of this organization, seeded in the same transaction as the organization (BR-M1-3).</summary>
    public ProcessId ProcessId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The creator's Keycloak subject (<c>sub</c>, BR-M1-21).</summary>
    public string CreatedBy { get; private set; } = null!;

    public bool IsActive => Status == OrganizationStatus.Active;

    /// <summary>
    /// A new, active organization. The caller seeds the process first, with
    /// <paramref name="id"/>, and passes the identity the seed returned (mod-001
    /// api-contract § 2).
    /// </summary>
    public static Organization Create(Guid id, EntityName name, ProcessId processId, DateTimeOffset createdAt, string createdBy)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("An organization's id is never all zeros.", nameof(id));
        }

        EnsureCreator(createdBy);

        return new Organization(id, name, processId, createdAt, createdBy);
    }

    /// <summary>Renames the organization. Projects, teams and areas do not change (US-2).</summary>
    public void Rename(EntityName name)
    {
        Name = name.Value;
        NormalizedName = name.Normalized;
    }

    /// <summary>Writes this one row: nothing beneath changes status (ADR-0026 rule 2). A deactivated one stays so.</summary>
    public void Deactivate() => Status = OrganizationStatus.Deactivated;

    /// <summary>Back to active, exactly as it was (BR-M1-20). Restoring an active organization changes nothing.</summary>
    public void Restore() => Status = OrganizationStatus.Active;

    internal static void EnsureCreator(string createdBy)
    {
        if (string.IsNullOrWhiteSpace(createdBy) || createdBy.Length > OrganizationLimits.CreatedByMaxLength)
        {
            throw new ArgumentException(
                $"The creator is the token's subject: 1 to {OrganizationLimits.CreatedByMaxLength} characters.",
                nameof(createdBy));
        }
    }
}
