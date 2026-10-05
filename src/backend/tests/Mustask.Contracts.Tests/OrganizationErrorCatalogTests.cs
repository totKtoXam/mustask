using System.Reflection;
using Mustask.Api.ErrorHandling.Catalog;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;

namespace Mustask.Contracts.Tests;

/// <summary>
/// The organizations module's error codes against its catalog file. mod-001
/// registered the first code (US-2 of mod-001: "The not-found code is registered
/// with its texts", BR-M2-11); mod-002 owns the file from then on (D-k) and adds
/// every code of its api-contract.md § 6. Each code is in
/// <c>contracts/errors/organizations.json</c> with the contract's status, params
/// and Russian text verbatim, and a Kazakh text of its own.
/// </summary>
public sealed class OrganizationErrorCatalogTests
{
    /// <summary>api-contract.md § 6 (mod-002), row by row: code, status, params and the Russian text.</summary>
    public static TheoryData<string, int, string, string> ContractTable() => new()
    {
        { OrganizationErrorCodes.OrganizationNotFound, 404, "", "Организация не найдена." },
        { OrganizationErrorCodes.ProjectNotFound, 404, "", "Проект не найден." },
        { OrganizationErrorCodes.TeamNotFound, 404, "", "Команда не найдена." },
        { OrganizationErrorCodes.AreaNotFound, 404, "", "Область не найдена." },
        { OrganizationErrorCodes.ValidationFailed, 400, "", "Проверьте правильность заполнения полей." },
        { OrganizationErrorCodes.NameLength, 400, "", "Название должно содержать от 1 до 100 символов" },
        { OrganizationErrorCodes.OrganizationCreateForbidden, 403, "", "Недостаточно прав" },
        { OrganizationErrorCodes.OrganizationCreateFailed, 500, "", "Не удалось создать организацию. Попробуйте ещё раз" },
        { OrganizationErrorCodes.OrganizationNameTaken, 409, "name:string", "Название «{name}» уже занято" },
        { OrganizationErrorCodes.ProjectNameTaken, 409, "", "Проект с таким названием уже есть в организации" },
        { OrganizationErrorCodes.ProjectNameTakenByDeactivated, 409, "", "Проект с таким названием уже есть в организации: он деактивирован" },
        { OrganizationErrorCodes.ProjectKeyRequired, 400, "", "Укажите ключ проекта" },
        { OrganizationErrorCodes.ProjectKeyInvalid, 400, "", "Ключ проекта: от 2 до 10 латинских заглавных букв и цифр, начиная с буквы" },
        { OrganizationErrorCodes.ProjectKeyTaken, 409, "key:string", "Ключ «{key}» уже используется в организации" },
        { OrganizationErrorCodes.TeamNameTaken, 409, "", "Команда с таким названием уже есть в проекте" },
        { OrganizationErrorCodes.TeamDefaultAreaNotOwned, 409, "", "Область по умолчанию должна принадлежать самой команде" },
        { OrganizationErrorCodes.TeamFirstTeamDefaultFixed, 409, "", "У первой команды область по умолчанию не меняется" },
        { OrganizationErrorCodes.TeamFirstTeamCannotBeDeleted, 409, "", "Нельзя удалить команду, которой принадлежит корневая область проекта" },
        { OrganizationErrorCodes.AreaNameTaken, 409, "", "Область с таким названием уже есть на этом уровне" },
        { OrganizationErrorCodes.AreaMoveIntoOwnSubtree, 409, "", "Нельзя переместить область внутрь неё самой или её вложенной области" },
        { OrganizationErrorCodes.AreaIsTeamDefault, 409, "team:string", "Область используется как область по умолчанию команды «{team}»" },
        { OrganizationErrorCodes.AreaNotEmpty, 409, "", "Нельзя удалить область, в которой есть рабочие элементы (включая удалённые) или вложенные области" },
        { OrganizationErrorCodes.AreaRootIsFixed, 409, "", "Корневая область меняется только вместе с проектом" },
    };

    // mod-002 api-contract § 6: every code is registered with its status, params, the contract's ru text and a kk text.
    [Theory]
    [MemberData(nameof(ContractTable))]
    public void Code_IsInTheOrganizationsCatalogAsTheContractStates(string code, int status, string parameters, string ru)
    {
        var found = CatalogFixture.Catalog.TryGet(code, out var entry);

        Assert.True(found, $"{code} is not in the error catalog.");
        Assert.Equal("organizations", entry!.Module);
        Assert.Equal(ErrorCatalogOrigin.Slice, entry.Origin);
        Assert.Equal(new[] { status }, entry.Statuses);
        Assert.Equal(parameters, Describe(entry.Params));
        Assert.False(entry.Deprecated);
        Assert.Equal(ru, entry.Ru);
        Assert.False(string.IsNullOrWhiteSpace(entry.Kk));
        Assert.NotEqual(entry.Ru, entry.Kk);
        Assert.True(entry.KkNeedsReview, $"{code}: the Kazakh text is marked for review (plan-backend P-2).");
    }

    // The table above is the whole registry: no constant of OrganizationErrorCodes is left out of it, and none is invented.
    [Fact]
    public void ContractTable_CoversEveryConstantOfOrganizationErrorCodes()
    {
        var constants = typeof(OrganizationErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal);

        var table = ContractTable().Select(row => (string)row[0]).Order(StringComparer.Ordinal);

        Assert.Equal(constants, table);
    }

    // mod-001 US-2 · "The not-found code is registered with its texts": the generated reference lists it.
    [Fact]
    public void OrganizationNotFound_IsListedInTheGeneratedErrorCodeReference()
    {
        var reference = File.ReadAllText(Path.Combine(RepositoryLayout.Root.FullName, "docs", "error-codes.md"));

        var row = reference.Split('\n').SingleOrDefault(line => line.StartsWith("|organizations.organization.not_found|", StringComparison.Ordinal));

        Assert.NotNull(row);
        Assert.Contains("|404|", row, StringComparison.Ordinal);
        Assert.Contains("Организация не найдена.", row, StringComparison.Ordinal);
    }

    private static string Describe(IReadOnlyDictionary<string, ErrorParamType> parameters) =>
        string.Join(
            ",",
            parameters
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}:{(pair.Value == ErrorParamType.Text ? "string" : pair.Value.ToString().ToLowerInvariant())}"));
}
