namespace Mustask.Domain.Imports.Templates;

/// <summary>
/// An import template of one organization (BR-IE-18): its author, a name unique in the organization ignoring case, a version raised by
/// every save, and the settings as one document. Deleting it is final.
/// </summary>
public sealed class ImportTemplate
{
    private ImportTemplate()
    {
        // EF materialization.
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; } = null!;

    /// <summary>The name lowered and trimmed: the key of its uniqueness.</summary>
    public string NameKey { get; private set; } = null!;

    public string AuthorId { get; private set; } = null!;

    public int Version { get; private set; }

    public TemplateSettings Settings { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static string KeyOf(string name) => TemplateValidator.NormalizeName(name).ToLowerInvariant();

    /// <summary>A new template at version 1. The caller has validated name and settings.</summary>
    public static ImportTemplate Create(Guid id, Guid organizationId, string name, string authorId, TemplateSettings settings, DateTimeOffset now)
    {
        var trimmed = TemplateValidator.NormalizeName(name);

        return new ImportTemplate
        {
            Id = id,
            OrganizationId = organizationId,
            Name = trimmed,
            NameKey = KeyOf(trimmed),
            AuthorId = authorId,
            Version = 1,
            Settings = settings,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>A save: the new name and settings, the version + 1 (last write wins, BR-IE-18).</summary>
    public void Update(string name, TemplateSettings settings, DateTimeOffset now)
    {
        Name = TemplateValidator.NormalizeName(name);
        NameKey = KeyOf(Name);
        Settings = settings;
        Version++;
        UpdatedAt = now;
    }

    /// <summary>«Копия &lt;name&gt;», « 2», « 3»… while <paramref name="isTaken"/>; cut to the name limit.</summary>
    public static string CopyName(string name, Func<string, bool> isTaken)
    {
        for (var n = 1; ; n++)
        {
            var suffix = n == 1 ? string.Empty : $" {n}";
            var prefix = "Копия ";
            var room = ImportLimits.MaxTemplateNameLength - prefix.Length - suffix.Length;
            var body = name.Length > room ? name[..room] : name;
            var candidate = prefix + body + suffix;

            if (!isTaken(KeyOf(candidate)))
            {
                return candidate;
            }
        }
    }
}
