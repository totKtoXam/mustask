namespace Mustask.Domain.UserDirectory;

/// <summary>
/// The named limits of the user directory (mod-014 api-contract.md, Part D).
/// They are part of the contract with the read model's consumers: changing one is a contract change, not
/// configuration (docs/conventions/backend.md, "Named values", item 3).
/// </summary>
public static class UserDirectoryLimits
{
    /// <summary>BR-UP-14, D-8: a search text shorter than this, after trimming, answers nobody.</summary>
    public const int SearchTextMinLength = 2;

    /// <summary>BR-UP-14: a longer search text is refused.</summary>
    public const int SearchTextMaxLength = 100;

    /// <summary>BR-UP-14, D-8: the most users one search answers.</summary>
    public const int SearchAnswerSize = 20;

    /// <summary>BR-UP-14, BR-UP-16, D-8: the most ids one search restriction or one lookup takes.</summary>
    public const int IdsPerRead = 500;

    /// <summary>Keycloak's own column size for an id, a username, a first or last name and an e-mail; a sign-in's claims are held to it.</summary>
    public const int FieldMaxLength = 255;

    /// <summary>First name, one space, last name — each at most <see cref="FieldMaxLength"/>.</summary>
    public const int DisplayNameMaxLength = (FieldMaxLength * 2) + 1;
}
