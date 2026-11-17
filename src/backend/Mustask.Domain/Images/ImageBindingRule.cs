namespace Mustask.Domain.Images;

/// <summary>
/// Who may take an image into what, supplied by the purpose (ADR-0045 decision 4). Today one factory: an image of a work
/// item's Описание belongs to that item and is bound by the member who uploaded it.
/// </summary>
public sealed record ImageBindingRule(ImagePurpose Purpose, Guid WorkItemId, string WriterSubject)
{
    public static ImageBindingRule ForWorkItemDescription(Guid workItemId, string writerSubject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(writerSubject);

        return new ImageBindingRule(ImagePurpose.WorkItemDescription, workItemId, writerSubject);
    }
}

/// <summary>The one verdict set of <see cref="Image.CheckBinding"/>; a refusal never says why (non-disclosure).</summary>
public enum ImageBindingVerdict
{
    /// <summary>Bound to the rule's item already: a re-save keeps it.</summary>
    Bound,

    /// <summary>Unbound, the rule's item, the writer's own: it may be bound now.</summary>
    Bindable,

    /// <summary>Anything else.</summary>
    Unavailable,
}
