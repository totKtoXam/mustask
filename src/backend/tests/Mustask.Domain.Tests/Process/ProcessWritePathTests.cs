using System.Reflection;
using Mustask.Domain.Process;

namespace Mustask.Domain.Tests.Process;

/// <summary>
/// bug-004 D-3 · "any write path that creates or renames a state refuses a breach": the only such path today is
/// <see cref="ProcessDefinition.Seed"/> (the MVP never changes a process, mod-001 BR-M2-13; doc-007's customization
/// is not built). This pins the aggregate's public surface: a new public way to add, rename or recategorize a state
/// on <see cref="ProcessDefinition"/>, <see cref="WorkItemType"/> or <see cref="StateDefinition"/> fails this test and
/// sends its author to <see cref="StateNameRule"/> and
/// <see cref="ProcessDefinition.EnsureStateNamesHaveOneCategory"/>, which the new path must call before it saves.
/// A write that goes round the aggregate (raw SQL, <c>ExecuteUpdate</c>, a tracked write on the set) is not seen here;
/// <c>ProcessStateWritesRules</c> in the architecture tests watches for it, by source text.
/// </summary>
public sealed class ProcessWritePathTests
{
    private static readonly Type[] AggregateTypes = [typeof(ProcessDefinition), typeof(WorkItemType), typeof(StateDefinition)];

    [Fact]
    public void ProcessAggregate_HasNoPublicWayToAddRenameOrRecategorizeAState()
    {
        var methods = AggregateTypes
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => !method.IsSpecialName)
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}")
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["ProcessDefinition.EnsureStateNamesHaveOneCategory", "ProcessDefinition.Seed"], methods);
    }

    [Fact]
    public void ProcessAggregate_HasNoPublicSetterAndNoPublicConstructor()
    {
        foreach (var type in AggregateTypes)
        {
            Assert.All(
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                property => Assert.False(property.SetMethod?.IsPublic ?? false, $"{type.Name}.{property.Name} has a public setter."));
            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        }
    }
}
