using System.Reflection;
using Mustask.Api.ErrorHandling.Catalog;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Errors;

namespace Mustask.Contracts.Tests;

/// <summary>
/// The work items module's error codes against its catalog file (mod-003
/// api-contract § 4, spec § Error codes): each code is in
/// <c>contracts/errors/workitems.json</c> with the contract's status, params and
/// Russian text verbatim, and a Kazakh text of its own marked for review.
/// </summary>
public sealed class WorkItemErrorCatalogTests
{
    /// <summary>
    /// api-contract.md § 4 (mod-003) and feat-001's api-contract § 5 (the six <c>workitems.link.*</c> codes), row by
    /// row: code, status, params and the Russian text.
    /// </summary>
    public static TheoryData<string, int, string, string> ContractTable() => new()
    {
        { WorkItemErrorCodes.WorkItemNotFound, 404, "", "Рабочий элемент не найден." },
        { WorkItemErrorCodes.ValidationFailed, 400, "", "Проверьте правильность заполнения полей." },
        { WorkItemErrorCodes.TitleRequired, 400, "", "Введите заголовок." },
        { WorkItemErrorCodes.TitleTooLong, 400, "", "Не более 255 символов." },
        { WorkItemErrorCodes.TitleMultiline, 400, "", "Заголовок должен быть в одну строку." },
        { WorkItemErrorCodes.DescriptionTooLong, 400, "", "Не более 100 000 символов." },
        { WorkItemErrorCodes.RequestIdInvalid, 400, "", "Не удалось отправить форму. Обновите страницу и попробуйте ещё раз." },
        { WorkItemErrorCodes.InvalidPage, 400, "", "Некорректный номер страницы." },
        { WorkItemErrorCodes.TypeNotFound, 404, "", "Тип рабочего элемента не найден." },
        { WorkItemErrorCodes.StateNotAllowed, 409, "", "Этот статус недоступен для рабочего элемента этого типа." },
        { WorkItemErrorCodes.AssigneeUnavailable, 409, "", "Этого пользователя нельзя назначить исполнителем." },
        { WorkItemErrorCodes.ParentNotFound, 404, "", "Родитель не найден в этом проекте." },
        { WorkItemErrorCodes.ParentLevelNotAllowed, 409, "", "Родителем может быть только рабочий элемент того же или более высокого уровня." },
        { WorkItemErrorCodes.SelfParent, 409, "", "Рабочий элемент не может быть родителем самого себя." },
        { WorkItemErrorCodes.Cycle, 409, "item,parent,path", "Нельзя выбрать родителем {parent}: он вложен в {item} ({path})." },
        { WorkItemErrorCodes.ParentChanged, 409, "", "Родитель уже изменён другим участником. Данные обновлены — проверьте и повторите." },
        { WorkItemErrorCodes.ItemDeleted, 409, "", "Удалённый рабочий элемент нельзя связывать." },

        // feat-022 api-contract § 6: the field reasons of the fields per type.
        { WorkItemErrorCodes.NumberOutOfRange, 400, "", "Допустимо значение от 0 до 9 999." },
        { WorkItemErrorCodes.NumberTooPrecise, 400, "", "Не более двух знаков после запятой." },
        { WorkItemErrorCodes.IntegerRequired, 400, "", "Введите целое число." },
        { WorkItemErrorCodes.ValueNotInList, 400, "", "Выберите значение из списка." },
        { WorkItemErrorCodes.DateOutOfRange, 400, "", "Укажите дату с 2000 по 2099 год." },
        { WorkItemErrorCodes.TargetBeforeStart, 400, "", "Целевая дата не может быть раньше даты начала." },
        { WorkItemErrorCodes.TextTooLong, 400, "", "Не более 100 000 символов." },
        { WorkItemErrorCodes.FieldNotOnType, 400, "", "Это поле недоступно для рабочего элемента этого типа." },
    };

    // mod-003 api-contract § 4: every code is registered with its status, params, the contract's ru text and a kk text.
    [Theory]
    [MemberData(nameof(ContractTable))]
    public void Code_IsInTheWorkItemsCatalogAsTheContractStates(string code, int status, string parameters, string ru)
    {
        var found = CatalogFixture.Catalog.TryGet(code, out var entry);

        Assert.True(found, $"{code} is not in the error catalog.");
        Assert.Equal("workitems", entry!.Module);
        Assert.Equal(ErrorCatalogOrigin.Slice, entry.Origin);
        Assert.Equal(new[] { status }, entry.Statuses);
        Assert.Equal(parameters, string.Join(",", entry.Params.Keys.Order(StringComparer.Ordinal)));
        Assert.False(entry.Deprecated);
        Assert.Equal(ru, entry.Ru);
        Assert.False(string.IsNullOrWhiteSpace(entry.Kk));
        Assert.NotEqual(entry.Ru, entry.Kk);
        Assert.True(entry.KkNeedsReview, $"{code}: the Kazakh text is marked for review.");
    }

    // The table above is the whole registry: no constant of WorkItemErrorCodes is left out of it, and none is invented.
    [Fact]
    public void ContractTable_CoversEveryConstantOfWorkItemErrorCodes()
    {
        var constants = typeof(WorkItemErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal);

        var table = ContractTable().Select(row => (string)row[0]).Order(StringComparer.Ordinal);

        Assert.Equal(constants, table);
        Assert.True(typeof(WorkItemErrorCodes).IsDefined(typeof(ErrorCodesAttribute)), "The scanner sees only classes marked [ErrorCodes].");
    }

    // spec § Error codes: the generated reference lists the item's not-found code.
    [Fact]
    public void WorkItemNotFound_IsListedInTheGeneratedErrorCodeReference()
    {
        var reference = File.ReadAllText(Path.Combine(RepositoryLayout.Root.FullName, "docs", "error-codes.md"));
        var row = reference.Split('\n').SingleOrDefault(line => line.StartsWith("|workitems.workitem.not_found|", StringComparison.Ordinal));

        Assert.NotNull(row);
        Assert.Contains("|404|", row, StringComparison.Ordinal);
        Assert.Contains("Рабочий элемент не найден.", row, StringComparison.Ordinal);
    }
}
