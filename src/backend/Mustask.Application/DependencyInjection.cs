using Mustask.Application.Features.Boards.GetBacklog;
using Mustask.Application.Features.Boards.GetBoard;
using Mustask.Application.Features.Boards.ReorderBacklog;
using Mustask.Application.Features.Dictionaries.CreateLanguage;
using Mustask.Application.Features.Dictionaries.GetLanguages;
using Mustask.Application.Features.Dictionaries.SetDefaultLanguage;
using Mustask.Application.Features.Dictionaries.UpdateLanguage;
using Mustask.Application.Features.Organizations.AddMember;
using Mustask.Application.Features.Organizations.AddTeam;
using Mustask.Application.Features.Organizations.AddTeamMember;
using Mustask.Application.Features.Organizations.ChangeMemberRole;
using Mustask.Application.Features.Organizations.CreateArea;
using Mustask.Application.Features.Organizations.CreateOrganization;
using Mustask.Application.Features.Organizations.CreateProject;
using Mustask.Application.Features.Organizations.DeactivateOrganization;
using Mustask.Application.Features.Organizations.DeactivateProject;
using Mustask.Application.Features.Organizations.DeleteArea;
using Mustask.Application.Features.Organizations.DeleteTeam;
using Mustask.Application.Features.Organizations.GetAreaTree;
using Mustask.Application.Features.Organizations.GetDeactivatedOrganizations;
using Mustask.Application.Features.Organizations.GetDeactivatedProjects;
using Mustask.Application.Features.Organizations.GetMembers;
using Mustask.Application.Features.Organizations.GetOrganization;
using Mustask.Application.Features.Organizations.GetOrganizationAccess;
using Mustask.Application.Features.Organizations.GetOrganizations;
using Mustask.Application.Features.Organizations.GetProject;
using Mustask.Application.Features.Organizations.GetProjects;
using Mustask.Application.Features.Organizations.GetTeam;
using Mustask.Application.Features.Organizations.GetTeamMembers;
using Mustask.Application.Features.Organizations.GetTeams;
using Mustask.Application.Features.Organizations.MoveArea;
using Mustask.Application.Features.Organizations.RemoveMember;
using Mustask.Application.Features.Organizations.RemoveTeamMember;
using Mustask.Application.Features.Organizations.RenameArea;
using Mustask.Application.Features.Organizations.RenameOrganization;
using Mustask.Application.Features.Organizations.RenameProject;
using Mustask.Application.Features.Organizations.RenameTeam;
using Mustask.Application.Features.Organizations.RestoreOrganization;
using Mustask.Application.Features.Organizations.RestoreProject;
using Mustask.Application.Features.Organizations.SearchMemberCandidates;
using Mustask.Application.Features.Organizations.SearchTeamMemberCandidates;
using Mustask.Application.Features.Organizations.SetAreaOwner;
using Mustask.Application.Features.Organizations.SetTeamDefaultArea;
using Mustask.Application.Features.Process;
using Mustask.Application.Features.Process.GetProcessDefinition;
using Mustask.Application.Features.Process.SeedProcess;
using Mustask.Application.Features.UserDirectory.ApplySnapshotPage;
using Mustask.Application.Features.UserDirectory.ApplyUserChanges;
using Mustask.Application.Features.UserDirectory.BeginSnapshot;
using Mustask.Application.Features.UserDirectory.CompleteSnapshot;
using Mustask.Application.Features.WorkItems.CreateWorkItem;
using Mustask.Application.Features.WorkItems.GetAssigneeCandidates;
using Mustask.Application.Features.WorkItems.GetParentCandidates;
using Mustask.Application.Features.WorkItems.GetWorkItem;
using Mustask.Application.Features.WorkItems.GetWorkItems;
using Mustask.Application.Features.WorkItems.SaveWorkItem;
using Mustask.Application.Features.WorkItems.SetWorkItemParent;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Application;

/// <summary>
/// Регистрация handler'ов слайсов. Не инфраструктурный пакет —
/// `Microsoft.Extensions.DependencyInjection.Abstractions` разрешён
/// архитектурным тестом (запрещены только EF Core/Npgsql/AspNetCore/Aspire/Serilog).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // One handler per route of DictionaryEndpoints.cs.
        services.AddScoped<GetLanguagesHandler>();
        services.AddScoped<CreateLanguageHandler>();
        services.AddScoped<UpdateLanguageHandler>();
        services.AddScoped<SetDefaultLanguageHandler>();

        // Process (mod-001): the seed seam for the organization-creation flow
        // (IProcessSeed is the interface other modules depend on, not the slice).
        services.AddScoped<IProcessSeed, SeedProcessHandler>();
        services.AddScoped<GetProcessDefinitionHandler>();
        // One handler per route of UserDirectoryEndpoints.cs (mod-014).
        services.AddScoped<ApplyUserChangesHandler>();
        services.AddScoped<BeginSnapshotHandler>();
        services.AddScoped<ApplySnapshotPageHandler>();
        services.AddScoped<CompleteSnapshotHandler>();

        // Organizations (mod-002): one handler per route of OrganizationEndpoints.cs.
        services.AddScoped<GetOrganizationsHandler>();
        services.AddScoped<GetDeactivatedOrganizationsHandler>();
        services.AddScoped<CreateOrganizationHandler>();
        services.AddScoped<GetOrganizationHandler>();
        services.AddScoped<RenameOrganizationHandler>();
        services.AddScoped<DeactivateOrganizationHandler>();
        services.AddScoped<RestoreOrganizationHandler>();
        services.AddScoped<GetProjectsHandler>();
        services.AddScoped<GetDeactivatedProjectsHandler>();
        services.AddScoped<CreateProjectHandler>();
        services.AddScoped<GetProjectHandler>();
        services.AddScoped<RenameProjectHandler>();
        services.AddScoped<DeactivateProjectHandler>();
        services.AddScoped<RestoreProjectHandler>();
        services.AddScoped<GetTeamsHandler>();
        services.AddScoped<GetTeamHandler>();
        services.AddScoped<AddTeamHandler>();
        services.AddScoped<RenameTeamHandler>();
        services.AddScoped<SetTeamDefaultAreaHandler>();
        services.AddScoped<DeleteTeamHandler>();
        services.AddScoped<GetAreaTreeHandler>();
        services.AddScoped<CreateAreaHandler>();
        services.AddScoped<RenameAreaHandler>();
        services.AddScoped<MoveAreaHandler>();
        services.AddScoped<SetAreaOwnerHandler>();
        services.AddScoped<DeleteAreaHandler>();

        // Organization access (mod-016): one handler per route of MembershipEndpoints.cs.
        services.AddScoped<GetOrganizationAccessHandler>();
        services.AddScoped<GetMembersHandler>();
        services.AddScoped<SearchMemberCandidatesHandler>();
        services.AddScoped<AddMemberHandler>();
        services.AddScoped<ChangeMemberRoleHandler>();
        services.AddScoped<RemoveMemberHandler>();
        services.AddScoped<GetTeamMembersHandler>();
        services.AddScoped<SearchTeamMemberCandidatesHandler>();
        services.AddScoped<AddTeamMemberHandler>();
        services.AddScoped<RemoveTeamMemberHandler>();

        // Work items (mod-003): one handler per route of WorkItemEndpoints.cs.
        services.AddScoped<GetWorkItemsHandler>();
        services.AddScoped<CreateWorkItemHandler>();
        services.AddScoped<GetWorkItemHandler>();
        services.AddScoped<SaveWorkItemHandler>();
        services.AddScoped<GetAssigneeCandidatesHandler>();

        // Work item links (feat-001): the parent write and the «Родитель» picker, routes of WorkItemEndpoints.cs too.
        services.AddScoped<SetWorkItemParentHandler>();
        services.AddScoped<GetParentCandidatesHandler>();

        // Boards and backlogs (mod-006): one handler per route of BoardEndpoints.cs.
        services.AddScoped<GetBacklogHandler>();
        services.AddScoped<ReorderBacklogHandler>();
        services.AddScoped<GetBoardHandler>();

        return services;
    }
}
