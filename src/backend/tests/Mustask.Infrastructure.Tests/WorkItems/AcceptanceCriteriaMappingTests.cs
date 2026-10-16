using System.Text.Json;
using Mustask.Domain.WorkItems;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Tests.WorkItems;

public sealed class AcceptanceCriteriaMappingTests
{
    private static MustaskDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MustaskDbContext>()
            .UseNpgsql("Host=localhost;Database=mustask")
            .Options;

        return new MustaskDbContext(options);
    }

    [Fact]
    public void WorkItem_AcceptanceCriteria_HasValueConverterAndValueComparerConfigured()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(WorkItem));
        Assert.NotNull(entityType);

        var property = entityType.FindProperty(nameof(WorkItem.AcceptanceCriteria));
        Assert.NotNull(property);

        var converter = property.GetValueConverter();
        Assert.NotNull(converter);

        var comparer = property.GetValueComparer();
        Assert.NotNull(comparer);
    }

    [Fact]
    public void WorkItem_AcceptanceCriteria_Converter_RoundtripsStructuredScenarios()
    {
        using var context = CreateContext();
        var property = context.Model.FindEntityType(typeof(WorkItem))!.FindProperty(nameof(WorkItem.AcceptanceCriteria))!;
        var converter = property.GetValueConverter()!;

        var original = new List<AcceptanceCriterion>
        {
            new("Пользователь авторизован", "Нажимает Сохранить", "Данные обновлены"),
            new("Гость", "Открывает страницу", "Редирект на вход"),
        };

        var serialized = (string?)converter.ConvertToProvider(original);
        Assert.NotNull(serialized);
        Assert.Contains("Пользователь авторизован", serialized);
        Assert.Contains("given", serialized);
        Assert.Contains("when", serialized);
        Assert.Contains("then", serialized);

        var deserialized = (IReadOnlyList<AcceptanceCriterion>?)converter.ConvertFromProvider(serialized);
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.Count);
        Assert.Equal("Пользователь авторизован", deserialized[0].Given);
        Assert.Equal("Нажимает Сохранить", deserialized[0].When);
        Assert.Equal("Данные обновлены", deserialized[0].Then);
        Assert.Equal("Гость", deserialized[1].Given);
    }

    [Fact]
    public void WorkItem_AcceptanceCriteria_Converter_HandlesNullAndEmpty()
    {
        using var context = CreateContext();
        var property = context.Model.FindEntityType(typeof(WorkItem))!.FindProperty(nameof(WorkItem.AcceptanceCriteria))!;
        var converter = property.GetValueConverter()!;

        Assert.Null(converter.ConvertToProvider(null));
        Assert.Null(converter.ConvertFromProvider(null));

        var emptyList = new List<AcceptanceCriterion>();
        var serializedEmpty = (string?)converter.ConvertToProvider(emptyList);
        Assert.Equal("[]", serializedEmpty);

        var deserializedEmpty = (IReadOnlyList<AcceptanceCriterion>?)converter.ConvertFromProvider("[]");
        Assert.NotNull(deserializedEmpty);
        Assert.Empty(deserializedEmpty);

        var deserializedFromEmptyString = (IReadOnlyList<AcceptanceCriterion>?)converter.ConvertFromProvider("");
        Assert.NotNull(deserializedFromEmptyString);
        Assert.Empty(deserializedFromEmptyString);
    }

    [Fact]
    public void WorkItem_AcceptanceCriteria_Converter_HandlesLegacyPlainTextGracefully()
    {
        using var context = CreateContext();
        var property = context.Model.FindEntityType(typeof(WorkItem))!.FindProperty(nameof(WorkItem.AcceptanceCriteria))!;
        var converter = property.GetValueConverter()!;

        var legacyText = "Старый плоский текст критериев";
        var deserialized = (IReadOnlyList<AcceptanceCriterion>?)converter.ConvertFromProvider(legacyText);
        Assert.NotNull(deserialized);
        Assert.Single(deserialized);
        Assert.Equal(string.Empty, deserialized[0].Given);
        Assert.Equal(string.Empty, deserialized[0].When);
        Assert.Equal(legacyText, deserialized[0].Then);
    }

    [Fact]
    public void WorkItem_AcceptanceCriteria_Comparer_ComparesAndClonesCorrectly()
    {
        using var context = CreateContext();
        var property = context.Model.FindEntityType(typeof(WorkItem))!.FindProperty(nameof(WorkItem.AcceptanceCriteria))!;
        var comparer = property.GetValueComparer();

        var listA = new List<AcceptanceCriterion> { new("G", "W", "T") };
        var listB = new List<AcceptanceCriterion> { new("G", "W", "T") };
        var listC = new List<AcceptanceCriterion> { new("G2", "W", "T") };

        Assert.True(comparer.Equals(listA, listB));
        Assert.False(comparer.Equals(listA, listC));
        Assert.True(comparer.Equals(null, null));
        Assert.False(comparer.Equals(listA, null));

        var snapshot = (IReadOnlyList<AcceptanceCriterion>?)comparer.Snapshot(listA);
        Assert.NotNull(snapshot);
        Assert.NotSame(listA, snapshot);
        Assert.True(comparer.Equals(listA, snapshot));
    }
}
