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
        { OrganizationErrorCodes.DescriptionLength, 400, "", "Описание должно содержать не более 2000 символов" },
        { OrganizationErrorCodes.TeamDestinationRequired, 400, "", "Выберите команду или «Без команды»" },
        { OrganizationErrorCodes.OrganizationCreateForbidden, 403, "", "Недостаточно прав" },
        { OrganizationErrorCodes.OrganizationCreateFailed, 500, "", "Не удалось создать организацию. Попробуйте ещё раз" },
        { OrganizationErrorCodes.OrganizationNameTaken, 409, "name:string", "Название «{name}» уже занято" },
        { OrganizationErrorCodes.ProjectNameTaken, 409, "", "Проект с таким названием уже есть в организации" },
        { OrganizationErrorCodes.ProjectNameTakenByDeactivated, 409, "", "Проект с таким названием уже есть в организации: он деактивирован" },
        { OrganizationErrorCodes.ProjectKeyRequired, 400, "", "Укажите ключ проекта" },
        { OrganizationErrorCodes.ProjectKeyInvalid, 400, "", "Ключ проекта: от 2 до 10 латинских заглавных букв и цифр, начиная с буквы" },
        { OrganizationErrorCodes.ProjectKeyTaken, 409, "key:string", "Ключ «{key}» уже используется в организации" },
        { OrganizationErrorCodes.TeamNameTaken, 409, "", "Команда с таким названием уже есть в организации" },
        { OrganizationErrorCodes.TeamNotInProject, 409, "", "Команда не участвует в проекте." },
        { OrganizationErrorCodes.AreaNameTaken, 409, "", "Область с таким названием уже есть на этом уровне" },
        { OrganizationErrorCodes.AreaMoveIntoOwnSubtree, 409, "", "Нельзя переместить область внутрь неё самой или её вложенной области" },
        { OrganizationErrorCodes.AreaNotEmpty, 409, "", "Нельзя удалить область, в которой есть рабочие элементы (включая удалённые) или вложенные области" },
        { OrganizationErrorCodes.AreaRootIsFixed, 409, "", "Корневая область меняется только вместе с проектом" },

        // mod-016 api-contract.md § 5 (the spec's list plus the two field reasons of P-9, status.md D-16 OP-5).
        { OrganizationErrorCodes.AccessForbidden, 403, "", "Недостаточно прав" },
        { OrganizationErrorCodes.MembershipAlreadyMember, 409, "", "Пользователь уже состоит в организации" },
        { OrganizationErrorCodes.MembershipLastAdmin, 409, "", "В организации должен остаться хотя бы один администратор" },
        { OrganizationErrorCodes.MembershipUserUnavailable, 409, "", "Этого пользователя нельзя добавить в организацию" },
        { OrganizationErrorCodes.MembershipNotFound, 404, "", "Участник не найден." },
        { OrganizationErrorCodes.MembershipRoleInvalid, 400, "", "Выберите роль: администратор, участник или читатель" },
        { OrganizationErrorCodes.MembershipUserInvalid, 400, "", "Укажите пользователя" },
        { OrganizationErrorCodes.TeamMemberExists, 409, "", "Пользователь уже состоит в команде" },
        { OrganizationErrorCodes.TeamMemberNotInOrganization, 409, "", "Добавить в команду можно только активного участника организации" },
        { OrganizationErrorCodes.TeamMemberNotFound, 404, "", "Участник команды не найден." },
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

    // mod-017 api-contract § 4, plan-backend D-5 · the four codes BR-M1-14 and BR-M1-18 retired keep their catalog entries, marked
    // deprecated, with the texts they had: a retired id stays taken and is never reused (ADR-0005 rule 3); their C# constants are
    // gone, so the table above does not list them.
    [Theory]
    [InlineData("organizations.team.default_area_not_owned", "Область по умолчанию должна принадлежать самой команде")]
    [InlineData("organizations.team.first_team_default_fixed", "У первой команды область по умолчанию не меняется")]
    [InlineData("organizations.team.first_team_cannot_be_deleted", "Нельзя удалить команду, которой принадлежит корневая область проекта")]
    [InlineData("organizations.area.is_team_default", "Область используется как область по умолчанию команды «{team}»")]
    // feat-028 BR-OL-10: the last-team guard is retired the same way.
    [InlineData("organizations.team.last_team_cannot_be_deleted", "Нельзя удалить единственную команду проекта.")]
    public void RetiredCode_StaysInTheCatalog_Deprecated_WithNoConstant(string code, string ru)
    {
        var found = CatalogFixture.Catalog.TryGet(code, out var entry);

        Assert.True(found, $"{code} left the error catalog: a retired code stays, marked deprecated.");
        Assert.True(entry!.Deprecated);
        Assert.Equal(ru, entry.Ru);
        Assert.DoesNotContain(
            typeof(OrganizationErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static),
            field => field.IsLiteral && (string?)field.GetRawConstantValue() == code);
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
