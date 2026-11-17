using Mustask.Application.Abstractions.Images;

namespace Mustask.Application.Features.Images.ReduceDescriptionImage;

/// <summary>The reduction's request: the address as sent and the body (up to 20 MB) to read after the address checks.</summary>
public sealed record ReduceDescriptionImageCommand(string OrganizationId, string ProjectId, string Identifier, IUploadBody Body);

/// <summary>The reduction's answer (api-contract § 2): the stored result and the file as sent.</summary>
public sealed record ReduceDescriptionImageResponse(ImageUploadView Image, OriginalImageView Original);

/// <summary>
/// <c>POST …/workitems/{identifier}/images/reductions</c> (US-6): the upload's processing, then the reduction within the floors;
/// the first result within 5 MB is stored as the caller's unbound upload, the original never.
/// </summary>
public sealed class ReduceDescriptionImageHandler(DescriptionImageIntakeFlow flow)
{
    public async Task<ReduceDescriptionImageResponse> Handle(ReduceDescriptionImageCommand command, CancellationToken cancellationToken)
    {
        var stored = await flow.RunAsync(
            command.OrganizationId, command.ProjectId, command.Identifier, command.Body, ImageIntakeMode.Reduction, cancellationToken);

        return new ReduceDescriptionImageResponse(stored.Image, stored.Original);
    }
}
