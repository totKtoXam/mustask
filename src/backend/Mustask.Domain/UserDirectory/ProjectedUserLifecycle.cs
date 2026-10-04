namespace Mustask.Domain.UserDirectory;

/// <summary>
/// Where a projected user stands apart from "enabled" (BR-UP-5, BR-UP-8;
/// spec.md, the lifecycle diagram). Stored as a string.
/// </summary>
public enum ProjectedUserLifecycle
{
    /// <summary>Held from a push or a snapshot entry; active exactly while enabled.</summary>
    Present,

    /// <summary>Missing from a complete snapshot begun after its state: inactive until a newer push.</summary>
    Absent,

    /// <summary>A reported Keycloak deletion: inactive for good.</summary>
    Deleted,
}
