using System.Reflection;
using Mustask.Application.Abstractions.Organizations;

namespace Mustask.Application.Tests.Features.Organizations.Navigation;

/// <summary>
/// BR-NV-15 · each read across projects takes the caller as a required argument, so that <c>feat-041-project-visibility</c> adds the
/// set of visible projects inside it. A signature test over the reads of «Обзор», the lists and the selectors' offers: every method
/// of <see cref="INavigationReadModel"/> declares a <see cref="CallerAccess"/> parameter that is not optional and not nullable. The
/// second line (feat-048) carries its own test over the item and view reads.
/// </summary>
public sealed class NavigationReadSignatureTests
{
    private static readonly MethodInfo[] Reads = typeof(INavigationReadModel).GetMethods();

    [Fact]
    public void TheInterface_HasExactlyTheTwoReads()
    {
        Assert.Equal(["ReadOverviewAsync", "ReadSelectorOffersAsync"], Reads.Select(read => read.Name).Order());
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
