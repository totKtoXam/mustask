using Mustask.Application.Abstractions.Imports;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Imports.CopyTemplate;
using Mustask.Application.Features.Imports.CreateTemplate;
using Mustask.Application.Features.Imports.DeleteTemplate;
using Mustask.Application.Features.Imports.ListTemplates;
using Mustask.Application.Features.Imports.UpdateTemplate;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Domain.Identity;
using Mustask.Domain.Imports;
using Mustask.Domain.Imports.DryRun;
using Mustask.Domain.Imports.Templates;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;
using Xunit;

namespace Mustask.Application.Tests.Features.Imports;

/// <summary>mod-024: the template slices over in-memory doubles — the rights, the limit, the copy name, the version.</summary>
public sealed class TemplateHandlersTests
{
    private const string Author = "author-1";
    private const string Stranger = "stranger-2";

    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly Guid ProjectId = Guid.NewGuid();

    private sealed class Templates : IImportTemplateRepository
    {
        public List<ImportTemplate> Rows { get; } = [];

        public Task<ImportTemplate?> FindAsync(Guid organizationId, Guid templateId, CancellationToken cancellationToken) =>
            Task.FromResult(Rows.FirstOrDefault(t => t.Id == templateId && t.OrganizationId == organizationId));

        public Task<ImportTemplate?> FindForUpdateAsync(Guid organizationId, Guid templateId, CancellationToken cancellationToken) =>
            FindAsync(organizationId, templateId, cancellationToken);

        public Task<IReadOnlyList<ImportTemplate>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImportTemplate>>(Rows.Where(t => t.OrganizationId == organizationId).ToList());

        public Task LockOrganizationTemplatesAsync(Guid organizationId, CancellationToken cancellationToken) => Task.CompletedTask;

        public void Add(ImportTemplate template) => Rows.Add(template);

        public void Remove(ImportTemplate template) => Rows.Remove(template);
    }

    private sealed class NoReferences : IImportReferenceReader
    {
        public Task<ImportReferences> LoadAsync(Guid organizationId, IReadOnlyCollection<string> identifiers, CancellationToken cancellationToken) =>
            Task.FromResult(new ImportReferences(
                new Dictionary<Guid, DryRunProjectRef>(),
                new Dictionary<Guid, DryRunTypeRef>(),
                new Dictionary<string, DryRunMemberRef>(),
                new Dictionary<Guid, string>(),
                new Dictionary<Guid, IReadOnlySet<Guid>>(),
                new Dictionary<string, DryRunItemRef>()));
    }

    /// <summary>An access that holds the import codes of a role bundle plus, for a manager, a grant on <see cref="ProjectId"/>.</summary>
    private sealed class Access(OrganizationRole role, bool projectManager) : IOrganizationAccess
    {
        public OrganizationRole Role => role;

        public bool Holds(string code) => OrganizationRoleBundles.Holds(role, code);

        public Task<AccessSet> SetAsync(CancellationToken cancellationToken)
        {
            var none = new Dictionary<Guid, IReadOnlyList<GrantRole>>();
            var project = projectManager
                ? new Dictionary<Guid, IReadOnlyList<GrantRole>> { [ProjectId] = [GrantRole.Manager] }
                : none;

            return Task.FromResult(new AccessSet(
                role, [new AccessNode(Guid.NewGuid(), AreaType.Project, null, ProjectId, false)], none, project, none));
        }
    }

    private sealed class Rig(string caller, OrganizationRole role, bool manager)
    {
        public Templates Store { get; } = new();

        public FakeUnitOfWork Unit { get; } = new();

        private IOrganizationAccess AccessOf => new Access(role, manager);

        private FakeCurrentUser User => new() { SubjectId = caller };

        public CreateTemplateHandler Create => new(Store, new NoReferences(), new FakeUserDirectory(), AccessOf, User, Unit, new FakeClock());

        public UpdateTemplateHandler Update => new(Store, new NoReferences(), new FakeUserDirectory(), AccessOf, User, Unit, new FakeClock());

        public CopyTemplateHandler Copy => new(Store, new FakeUserDirectory(), AccessOf, User, Unit, new FakeClock());

        public DeleteTemplateHandler Delete => new(Store, AccessOf, User, Unit);

        public ListTemplatesHandler List => new(Store, new FakeUserDirectory(), AccessOf, User);

        public ImportTemplate Seed(string name, string author = Author)
        {
            var template = ImportTemplate.Create(Guid.NewGuid(), OrganizationId, name, author, Settings(), DateTimeOffset.UnixEpoch);
            Store.Add(template);

            return template;
        }
    }

    private static Rig Manager(string caller = Author) => new(caller, OrganizationRole.Member, manager: true);

    private static Rig Admin(string caller = "admin-0") => new(caller, OrganizationRole.Admin, manager: false);

    private static Rig Viewer(string caller = Author) => new(caller, OrganizationRole.Viewer, manager: false);

    private static FieldMapping Column(string field, string column) => new()
    {
        Id = Guid.NewGuid(),
        Field = field,
        Source = new MappingSource { Kind = TemplateTokens.SourceColumn, Column = column },
    };

    private static TemplateSettings Settings() => new()
    {
        Source = new TemplateSource { Format = TemplateTokens.FormatCsv, Encoding = "utf-8", Delimiter = ";", Headers = ["Проект", "Тип", "Заголовок"] },
        Mappings = [Column("project", "Проект"), Column("type", "Тип"), Column("title", "Заголовок")],
    };

    private static string Org => OrganizationId.ToString();

    [Fact]
    public async Task Create_ByAManager_StoresTheTemplateWithTheCallerAsAuthor()
    {
        var rig = Manager();

        var view = await rig.Create.Handle(new CreateTemplateCommand(Org, new TemplateBody("Реестр", Settings())), CancellationToken.None);

        Assert.Equal(Author, Assert.Single(rig.Store.Rows).AuthorId);
        Assert.True(view.CanEdit);
        Assert.Equal(1, view.Version);
    }

    [Fact]
    public async Task Create_ByAViewer_IsForbidden_AndStoresNothing()
    {
        var rig = Viewer();

        var error = await Assert.ThrowsAsync<ForbiddenException>(() =>
            rig.Create.Handle(new CreateTemplateCommand(Org, new TemplateBody("Реестр", Settings())), CancellationToken.None));

        Assert.Equal(ImportErrorCodes.AccessForbidden, error.Code);
        Assert.Empty(rig.Store.Rows);
    }

    [Fact]
    public async Task Create_AtTheLimit_IsAConflict()
    {
        var rig = Manager();

        for (var i = 0; i < ImportLimits.MaxTemplates; i++)
        {
            rig.Seed($"Шаблон {i}");
        }

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            rig.Create.Handle(new CreateTemplateCommand(Org, new TemplateBody("Ещё один", Settings())), CancellationToken.None));

        Assert.Equal(ImportErrorCodes.TemplateLimitReached, error.Code);
    }

    [Fact]
    public async Task Create_ANameTakenIgnoringCase_IsAConflict()
    {
        var rig = Manager();
        rig.Seed("Реестр");

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            rig.Create.Handle(new CreateTemplateCommand(Org, new TemplateBody("  реЕСТР ", Settings())), CancellationToken.None));

        Assert.Equal(ImportErrorCodes.TemplateNameTaken, error.Code);
    }

    [Fact]
    public async Task Copy_TakesTheNextFreeName_AndTheCallerAsAuthor()
    {
        var rig = Manager(Stranger);
        var source = rig.Seed("Реестр");

        var first = await rig.Copy.Handle(new CopyTemplateCommand(Org, source.Id.ToString()), CancellationToken.None);
        var second = await rig.Copy.Handle(new CopyTemplateCommand(Org, source.Id.ToString()), CancellationToken.None);

        Assert.Equal("Копия Реестр", first.Name);
        Assert.Equal("Копия Реестр 2", second.Name);
        Assert.Equal(Stranger, second.AuthorId);
    }

    [Fact]
    public async Task Update_BumpsTheVersion_ForTheAuthor_AndRefusesAnotherManager()
    {
        var rig = Manager();
        var own = rig.Seed("Мой");
        var foreign = rig.Seed("Чужой", author: Stranger);

        var saved = await rig.Update.Handle(new UpdateTemplateCommand(Org, own.Id.ToString(), new TemplateBody("Мой 2", Settings())), CancellationToken.None);
        var error = await Assert.ThrowsAsync<ForbiddenException>(() =>
            rig.Update.Handle(new UpdateTemplateCommand(Org, foreign.Id.ToString(), new TemplateBody("Взлом", Settings())), CancellationToken.None));

        Assert.Equal(2, saved.Version);
        Assert.Equal(ImportErrorCodes.AccessForbidden, error.Code);
        Assert.Equal("Чужой", foreign.Name);
    }

    [Fact]
    public async Task Update_AnAuthorWhoLostTheRole_IsForbidden()
    {
        var rig = Viewer();
        var own = rig.Seed("Был моим");

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            rig.Update.Handle(new UpdateTemplateCommand(Org, own.Id.ToString(), new TemplateBody("Новое", Settings())), CancellationToken.None));
    }

    [Fact]
    public async Task Delete_AnAdministratorRemovesAnyTemplate_AManagerOnlyOwn()
    {
        var admin = Admin();
        var someones = admin.Seed("Чужой", author: Stranger);
        await admin.Delete.Handle(new DeleteTemplateCommand(Org, someones.Id.ToString()), CancellationToken.None);

        var manager = Manager();
        var foreign = manager.Seed("Чужой", author: Stranger);
        var own = manager.Seed("Мой");
        await Assert.ThrowsAsync<ForbiddenException>(() => manager.Delete.Handle(new DeleteTemplateCommand(Org, foreign.Id.ToString()), CancellationToken.None));
        await manager.Delete.Handle(new DeleteTemplateCommand(Org, own.Id.ToString()), CancellationToken.None);

        Assert.Empty(admin.Store.Rows);
        Assert.Equal([foreign], manager.Store.Rows);
    }

    [Fact]
    public async Task Delete_AnUnknownTemplate_IsNotFound()
    {
        var rig = Admin();

        var error = await Assert.ThrowsAsync<NotFoundException>(() => rig.Delete.Handle(new DeleteTemplateCommand(Org, Guid.NewGuid().ToString()), CancellationToken.None));

        Assert.Equal(ImportErrorCodes.TemplateNotFound, error.Code);
    }

    [Fact]
    public async Task List_ServesThePageAndRowFlagsFromTheCallersRights()
    {
        var rig = Manager();
        var own = rig.Seed("Б мой");
        var foreign = rig.Seed("А чужой", author: Stranger);

        var page = await rig.List.Handle(new ListTemplatesQuery(Org, null, null, "name", null, null), CancellationToken.None);

        Assert.True(page.CanCreate);
        Assert.True(page.CanRun);
        Assert.Equal([foreign.Id, own.Id], page.Items.Select(i => i.Id));
        Assert.Equal([false, true], page.Items.Select(i => i.CanEdit));
        Assert.Equal([false, true], page.Items.Select(i => i.CanDelete));
        Assert.All(page.Items, row => Assert.True(row.CanCopy));
    }

    [Fact]
    public async Task List_ForAViewer_ServesNoWriteOrRunFlags()
    {
        var rig = Viewer();
        rig.Seed("Реестр");

        var page = await rig.List.Handle(new ListTemplatesQuery(Org, null, null, null, null, null), CancellationToken.None);

        Assert.False(page.CanCreate);
        Assert.False(page.CanRun);
        Assert.False(Assert.Single(page.Items).CanEdit);
    }

    [Fact]
    public async Task List_ABadSort_IsAValidationError()
    {
        var rig = Admin();

        await Assert.ThrowsAsync<ValidationException>(() =>
            rig.List.Handle(new ListTemplatesQuery(Org, null, null, "oops", null, null), CancellationToken.None));
    }
}
