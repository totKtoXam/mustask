using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Process;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Mustask.Infrastructure.Tests.Organizations.OrganizationsDatabaseFixture;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// plan-backend § 6.3: a race on a business unique index, the case where a
/// handler's pre-check passed but a concurrent write won, is answered with the
/// same 409 code as the pre-check. Each case skips the pre-check and saves a
/// duplicate, which is exactly what the loser of a race does.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class OrganizationsSaveChangesTests(OrganizationsDatabaseFixture database)
{
    // US-1 · "Two simultaneous creations": the loser hears name_taken with the submitted, trimmed name.
    [Fact]
    public async Task SaveAsync_OrganizationNameRace_IsNameTakenWithTheSubmittedName()
    {
        var structure = await database.CreateStructureAsync();
        var submitted = structure.Organization.Name.ToUpperInvariant();

        var refusal = await SaveRacingAsync(async (scope, ct) =>
        {
            var organizationId = Guid.CreateVersion7();
            var processId = await scope.ServiceProvider.GetRequiredService<IProcessSeed>().SeedAsync(organizationId, ct);
            var repository = scope.ServiceProvider.GetRequiredService<IOrganizationRepository>();

            repository.Add(Organization.Create(organizationId, Name("  " + submitted + " "), processId, Now, Creator));
            await repository.SaveAsync(ct);
        });

        Assert.Equal(OrganizationErrorCodes.OrganizationNameTaken, refusal.Code);
        Assert.Equal(submitted, refusal.Params["name"].Value);
    }

    // US-3 · "name unique in the organization": a race on the project name.
    [Fact]
    public async Task SaveAsync_ProjectNameRace_IsProjectNameTaken()
    {
        var structure = await database.CreateStructureAsync();

        var refusal = await SaveRacingAsync(async (scope, ct) =>
        {
            var repository = scope.ServiceProvider.GetRequiredService<IProjectRepository>();

            repository.Add(Project.Create(structure.Organization.Id, Name("пример ПРОЕКТА"), "OTHER", Now, Creator));
            await repository.SaveAsync(ct);
        });

        Assert.Equal(OrganizationErrorCodes.ProjectNameTaken, refusal.Code);
    }

    // US-3 · "Two projects with one key at the same moment": the loser hears key_taken with its key.
    [Fact]
    public async Task SaveAsync_ProjectKeyRace_IsKeyTakenWithTheKey()
    {
        var structure = await database.CreateStructureAsync();

        var refusal = await SaveRacingAsync(async (scope, ct) =>
        {
            var repository = scope.ServiceProvider.GetRequiredService<IProjectRepository>();

            repository.Add(Project.Create(structure.Organization.Id, Name("Другой проект"), "PRM", Now, Creator));
            await repository.SaveAsync(ct);
        });

        Assert.Equal(OrganizationErrorCodes.ProjectKeyTaken, refusal.Code);
        Assert.Equal("PRM", refusal.Params["key"].Value);
    }

    // US-4 · "Two teams of one name at the same moment": the team index fires first, before the area's.
    [Fact]
    public async Task SaveAsync_TeamNameRace_IsTeamNameTaken()
    {
        var structure = await database.CreateStructureAsync();

        var refusal = await SaveRacingAsync(async (scope, ct) =>
        {
            var repository = scope.ServiceProvider.GetRequiredService<ITeamRepository>();
            var root = await scope.ServiceProvider.GetRequiredService<IAreaRepository>()
                .LockRootAsync(structure.Project.Project.Id, forUpdate: false, ct);

            repository.Add(Team.CreateWithArea(structure.Project.Project.Id, Name("ПЛАТФОРМА"), root));
            await repository.SaveAsync(ct);
        });

        Assert.Equal(OrganizationErrorCodes.TeamNameTaken, refusal.Code);
    }

    // US-5 · "Two creations of one sibling name keep one".
    [Fact]
    public async Task SaveAsync_SiblingNameRace_IsAreaNameTaken()
    {
        var structure = await database.CreateStructureAsync();

        var refusal = await SaveRacingAsync(async (scope, ct) =>
        {
            var repository = scope.ServiceProvider.GetRequiredService<IAreaRepository>();
            var root = await repository.LockRootAsync(structure.Project.Project.Id, forUpdate: false, ct);

            repository.Add(Area.CreateChild(root, Name(" платформа "), ownerTeam: null));
            await repository.SaveAsync(ct);
        });

        Assert.Equal(OrganizationErrorCodes.AreaNameTaken, refusal.Code);
    }

    // plan-backend § 6.3: an index no handler can reach is a defect and stays a 500, not a business code.
    [Fact]
    public async Task SaveAsync_UnmappedIndex_IsRethrownAsIs()
    {
        var structure = await database.CreateStructureAsync();

        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var projectId = structure.Project.Project.Id;
                var areas = scope.ServiceProvider.GetRequiredService<IAreaRepository>();
                var team = await scope.ServiceProvider.GetRequiredService<ITeamRepository>()
                    .LockForShareAsync(projectId, structure.SecondTeam.Team.Id, ct);
                var root = await areas.LockRootAsync(projectId, forUpdate: false, ct);
                var owned = Area.CreateChild(root, Name("Второе"), team);
                areas.Add(owned);
                await areas.SaveAsync(ct);

                // A second default of one team, skipping the clear: ix_areas_default_of_team_id, which no handler maps.
                owned.MarkDefaultOf(team!);
                await areas.SaveAsync(ct);

                return true;
            },
            CancellationToken.None));

        Assert.Contains("ix_areas_default_of_team_id", error.InnerException?.Message ?? string.Empty, StringComparison.Ordinal);
    }

    private async Task<ConflictException> SaveRacingAsync(Func<IServiceScope, CancellationToken, Task> write)
    {
        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        return await Assert.ThrowsAsync<ConflictException>(() => unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                await write(scope, ct);
                return true;
            },
            CancellationToken.None));
    }
}
