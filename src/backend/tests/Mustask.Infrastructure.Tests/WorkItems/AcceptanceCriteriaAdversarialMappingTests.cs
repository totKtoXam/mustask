using System.Text.Json;
using Mustask.Domain.WorkItems;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Tests.WorkItems;

public sealed class AcceptanceCriteriaAdversarialMappingTests
{
    private static MustaskDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MustaskDbContext>()
            .UseNpgsql("Host=localhost;Database=mustask")
            .Options;

        return new MustaskDbContext(options);
    }

    [Fact]
    public void ValueComparer_ChangeDetection_MutatedList_Versus_Snapshot()
    {
        using var context = CreateContext();
        var property = context.Model.FindEntityType(typeof(WorkItem))!.FindProperty(nameof(WorkItem.AcceptanceCriteria))!;
        var comparer = property.GetValueComparer();

        var list = new List<AcceptanceCriterion> { new("G1", "W1", "T1") };
        var snapshot = (IReadOnlyList<AcceptanceCriterion>?)comparer.Snapshot(list);
        Assert.NotNull(snapshot);
        Assert.True(comparer.Equals(list, snapshot));

        // In-place mutation: Add
        list.Add(new AcceptanceCriterion("G2", "W2", "T2"));
        Assert.False(comparer.Equals(list, snapshot));
    }

    [Fact]
    public void ValueComparer_ChangeDetection_MutatedElement_Versus_Snapshot()
    {
        using var context = CreateContext();
        var property = context.Model.FindEntityType(typeof(WorkItem))!.FindProperty(nameof(WorkItem.AcceptanceCriteria))!;
        var comparer = property.GetValueComparer();

        var list = new List<AcceptanceCriterion> { new("G1", "W1", "T1") };
        var snapshot = (IReadOnlyList<AcceptanceCriterion>?)comparer.Snapshot(list);

        // In-place mutation: Replace element at index 0
        list[0] = new AcceptanceCriterion("G1_modified", "W1", "T1");
        Assert.False(comparer.Equals(list, snapshot));
    }

    [Fact]
    public void ValueComparer_ChangeDetection_RemovedElement_Versus_Snapshot()
    {
        using var context = CreateContext();
        var property = context.Model.FindEntityType(typeof(WorkItem))!.FindProperty(nameof(WorkItem.AcceptanceCriteria))!;
        var comparer = property.GetValueComparer();

        var list = new List<AcceptanceCriterion> { new("G1", "W1", "T1"), new("G2", "W2", "T2") };
        var snapshot = (IReadOnlyList<AcceptanceCriterion>?)comparer.Snapshot(list);

        // In-place mutation: Remove
        list.RemoveAt(0);
        Assert.False(comparer.Equals(list, snapshot));
    }

    [Fact]
    public void ValueComparer_NullAndEmpty_SymmetricEqualityAndHashCodes()
    {
        using var context = CreateContext();
        var property = context.Model.FindEntityType(typeof(WorkItem))!.FindProperty(nameof(WorkItem.AcceptanceCriteria))!;
        var comparer = property.GetValueComparer();

        Assert.True(comparer.Equals(null, null));
        Assert.True(comparer.Equals(new List<AcceptanceCriterion>(), new List<AcceptanceCriterion>()));
        Assert.False(comparer.Equals(null, new List<AcceptanceCriterion>()));
        Assert.False(comparer.Equals(new List<AcceptanceCriterion>(), null));

        Assert.Equal(0, comparer.GetHashCode(null!));
        Assert.Equal(0, comparer.GetHashCode(new List<AcceptanceCriterion>()));

        var list1 = new List<AcceptanceCriterion> { new("A", "B", "C") };
        var list2 = new List<AcceptanceCriterion> { new("A", "B", "C") };
        Assert.Equal(comparer.GetHashCode(list1), comparer.GetHashCode(list2));

        var listDifferent = new List<AcceptanceCriterion> { new("A", "B", "DIFFERENT") };
        Assert.NotEqual(comparer.GetHashCode(list1), comparer.GetHashCode(listDifferent));
    }

    [Fact]
    public void ValueConverter_CyrillicEncoding_IsNotEscaped()
    {
        using var context = CreateContext();
        var property = context.Model.FindEntityType(typeof(WorkItem))!.FindProperty(nameof(WorkItem.AcceptanceCriteria))!;
        var converter = property.GetValueConverter()!;

        var cyrillicList = new List<AcceptanceCriterion>
        {
            new("Пользователь авторизован", "Вводит валидные данные", "Система открывает дашборд"),
        };

        var json = (string?)converter.ConvertToProvider(cyrillicList);
        Assert.NotNull(json);

        // Asserts Unicode characters are preserved verbatim and not encoded as \u04...
        Assert.Contains("Пользователь авторизован", json);
        Assert.DoesNotContain("\\u", json);
    }

    [Fact]
    public void ValueConverter_PartialOrMissingFieldsJson_DeserializesGracefully()
    {
        using var context = CreateContext();
        var property = context.Model.FindEntityType(typeof(WorkItem))!.FindProperty(nameof(WorkItem.AcceptanceCriteria))!;
        var converter = property.GetValueConverter()!;

        // JSON where some fields are missing or null
        var jsonWithMissingFields = "[{\"given\":\"Only given\"},{\"when\":\"Only when\",\"then\":null}]";
        var deserialized = (IReadOnlyList<AcceptanceCriterion>?)converter.ConvertFromProvider(jsonWithMissingFields);

        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.Count);
        Assert.Equal("Only given", deserialized[0].Given);
        Assert.Equal(string.Empty, deserialized[0].When);
        Assert.Equal(string.Empty, deserialized[0].Then);

        Assert.Equal(string.Empty, deserialized[1].Given);
        Assert.Equal("Only when", deserialized[1].When);
        Assert.Equal(string.Empty, deserialized[1].Then);
    }
}
