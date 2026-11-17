using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Images;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.Organizations;
using Mustask.Application.Features.WorkItems;
using Mustask.Domain.Images;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Images;

/// <summary>
/// The order of answers the two upload routes share (api-contract § 1-2): the address from the outside in, the item not in
/// «Удалена», only then the body (under the mode's cap), the intake, and last the insert under the item's <c>FOR SHARE</c> lock,
/// where the item is judged again. Lives in the module's root, not in a slice (as <c>WorkItemAddress</c> does).
/// </summary>
public sealed class DescriptionImageIntakeFlow(
    IOrganizationStructure structure,
    IProcessDefinitionReadModel processes,
    IWorkItemReadModel readModel,
    IWorkItemRepository workItems,
    IImageIntake intake,
    IImageStore store,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    private static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(5);

    public async Task<StoredDescriptionImage> RunAsync(
        string organizationId, string projectId, string identifier, IUploadBody body, ImageIntakeMode mode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);

        var address = await WorkItemAddress.ResolveAsync(structure, organizationId, projectId, identifier, cancellationToken);
        var process = await processes.FindByOrganizationAsync(address.OrganizationId, cancellationToken)
            ?? throw new InvalidOperationException($"The organization {address.OrganizationId} has no process.");

        await EnsureLiveItemAsync(address, process, cancellationToken);

        var reduction = mode == ImageIntakeMode.Reduction;
        using var read = await body.ReadAsync(reduction ? ImageLimits.MaxReductionInputBytes : ImageLimits.MaxStoredBytes, cancellationToken);

        if (read.TooLarge)
        {
            throw Refusal(reduction ? ImageErrorCodes.ReductionTooLarge : ImageErrorCodes.UploadTooLarge);
        }

        var result = await intake.AcceptAsync(read.Content, mode, cancellationToken);

        if (result.Outcome != ImageIntakeOutcome.Accepted)
        {
            throw result.Outcome switch
            {
                ImageIntakeOutcome.Format => Refusal(ImageErrorCodes.FormatUnsupported),
                ImageIntakeOutcome.Dimensions => Refusal(ImageErrorCodes.TooManyPixels),
                ImageIntakeOutcome.Animation => Refusal(ImageErrorCodes.AnimationTooLarge),
                ImageIntakeOutcome.AnimatedReduction => Refusal(ImageErrorCodes.ReductionAnimated),
                ImageIntakeOutcome.CannotReduce => Refusal(ImageErrorCodes.CannotReduce),
                ImageIntakeOutcome.Busy => new ServiceUnavailableException(ImageErrorCodes.Busy, RetryAfter),
                _ => new InvalidOperationException($"Unexpected intake outcome {result.Outcome}."),
            };
        }

        var facts = result.Stored!;

        return await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // The item again, under FOR SHARE: a deletion that committed meanwhile is a 409, a purge a 404.
                if (await workItems.ShareLockAsync(address.ProjectId, address.Identifier.Number, ct) is not { } itemId)
                {
                    throw new NotFoundException(WorkItemErrorCodes.WorkItemNotFound);
                }

                await EnsureLiveItemAsync(address, process, ct);

                var image = Image.ForWorkItemDescription(Guid.NewGuid(), facts, currentUser.SubjectId, itemId, clock.UtcNow);
                store.Add(image, result.Content!);
                await unitOfWork.SaveChangesAsync(ct);

                return new StoredDescriptionImage(
                    new ImageUploadView(image.Id, ImageFormats.WireName(facts.Format), facts.ByteLength, facts.Width, facts.Height),
                    OriginalImageView.Of(result.Original!));
            },
            cancellationToken);
    }

    private async Task EnsureLiveItemAsync(AddressedWorkItem address, ProcessDefinitionRow process, CancellationToken cancellationToken)
    {
        var head = await readModel.FindHeadAsync(address.ProjectId, address.Identifier.Number, cancellationToken)
            ?? throw new NotFoundException(WorkItemErrorCodes.WorkItemNotFound);
        var type = ProcessWorkItemTypes.OfItem(process, head.TypeId);

        WorkItemAddress.EnsureTypeKey(type, address.Identifier);

        if (type.IsRemoved(head.StateId))
        {
            throw new ConflictException(WorkItemErrorCodes.WorkItemDeleted);
        }
    }

    /// <summary>A refusal of the body or its content: a top-level 400 of its own code, no field reasons.</summary>
    private static ValidationException Refusal(string code) => new(code, new Dictionary<string, IReadOnlyList<FieldError>>());
}
