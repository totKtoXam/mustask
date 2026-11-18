using Mustask.Application.Abstractions.Images;

namespace Mustask.Application.Features.Images.UploadDescriptionImage;

/// <summary>The upload's request: the address as sent and the body to read after the address checks.</summary>
public sealed record UploadDescriptionImageCommand(string OrganizationId, string ProjectId, string Identifier, IUploadBody Body);

/// <summary>The upload's answer (api-contract § 1).</summary>
public sealed record UploadDescriptionImageResponse(ImageUploadView Image);

/// <summary>
/// <c>POST …/workitems/{identifier}/images</c> (US-2): makes an unbound upload of the caller for the item. The order of answers
/// is <see cref="DescriptionImageIntakeFlow"/>'s.
/// </summary>
public sealed class UploadDescriptionImageHandler(DescriptionImageIntakeFlow flow)
{
    public async Task<UploadDescriptionImageResponse> Handle(UploadDescriptionImageCommand command, CancellationToken cancellationToken)
    {
        var stored = await flow.RunAsync(
            command.OrganizationId, command.ProjectId, command.Identifier, command.Body, ImageIntakeMode.Upload, cancellationToken);

        return new UploadDescriptionImageResponse(stored.Image);
    }
}
