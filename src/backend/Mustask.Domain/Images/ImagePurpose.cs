namespace Mustask.Domain.Images;

/// <summary>
/// What an image is for (ADR-0045 decision 4). It decides who may bind and read it; the closed set grows by one member
/// per feature that stores images (feat-059 adds its own). Stored as text.
/// </summary>
public enum ImagePurpose
{
    /// <summary>An image of a work item's Описание (feat-078).</summary>
    WorkItemDescription,
}
