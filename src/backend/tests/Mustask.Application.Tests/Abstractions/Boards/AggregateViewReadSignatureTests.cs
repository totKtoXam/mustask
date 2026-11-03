using System.Reflection;
using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.Organizations;

namespace Mustask.Application.Tests.Abstractions.Boards;

/// <summary>
/// BR-NV-15 for the second line (feat-048) · every read of the aggregate views takes the caller as a required first argument, so that
/// <c>feat-041-project-visibility</c> adds the set of visible projects inside it, and no read can be written without it.
/// </summary>
public sealed class AggregateViewReadSignatureTests
{
    private static readonly MethodInfo[] Reads = typeof(IAggregateViewReadModel).GetMethods();

    [Fact]
    public void TheInterface_HasExactlyTheSixReads()
    {
        Assert.Equal(
            [
                "ReadAssigneeOptionsAsync",
                "ReadChecklistsAsync",
                "ReadCountsAsync",
                "ReadDirectoryAsync",
                "ReadFlatPageAsync",
                "ReadGroupPageAsync",
            ],
            Reads.Select(read => read.Name).Order());
    }

    [Fact]
    public void EveryRead_TakesTheCallerAsItsFirstArgument_Required()
    {
        Assert.NotEmpty(Reads);

        foreach (var read in Reads)
        {
            var first = read.GetParameters().FirstOrDefault();

            Assert.True(first is not null && first.ParameterType == typeof(CallerAccess), $"{read.Name} does not take the caller first.");
            Assert.False(first!.HasDefaultValue, $"{read.Name} makes the caller optional.");
            Assert.Null(Nullable.GetUnderlyingType(first.ParameterType));
        }
    }
}
