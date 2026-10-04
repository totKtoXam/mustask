using Mustask.Application.Abstractions.Process;
using Mustask.Domain.Process;

namespace Mustask.Application.Tests.Features.Process.GetProcessDefinition;

/// <summary>
/// Answers with rows built from a seeded process, in the order the real read model
/// fixes: levels by rank descending, types by level rank descending then position,
/// states by position.
/// </summary>
public sealed class FakeProcessDefinitionReadModel : IProcessDefinitionReadModel
{
    public List<Guid> Asked { get; } = [];

    public ProcessDefinitionRow? Answer { get; set; }

    public Exception? ThrowOnRead { get; set; }

    public static FakeProcessDefinitionReadModel Seeded() => new() { Answer = SeededRow() };

    public static ProcessDefinitionRow SeededRow()
    {
        var process = ProcessDefinition.Seed(ProcessId.New(), Guid.NewGuid());

        return new ProcessDefinitionRow(
            process.Id,
            process.BacklogLevels
                .OrderByDescending(level => level.Rank)
                .Select(level => new BacklogLevelRow(level.Id, level.Name, level.Rank, level.Kind))
                .ToList(),
            process.WorkItemTypes
                .Select(type => (Type: type, Level: process.BacklogLevels.Single(level => level.Id == type.BacklogLevelId)))
                .OrderByDescending(pair => pair.Level.Rank)
                .ThenBy(pair => pair.Type.Position)
                .Select(pair => new WorkItemTypeRow(
                    pair.Type.Id,
                    pair.Type.Key,
                    pair.Type.Name,
                    pair.Type.Appearance,
                    pair.Level.Id,
                    pair.Level.Name,
                    pair.Type.States
                        .OrderBy(state => state.Position)
                        .Select(state => new StateRow(state.Id, state.Name, state.Position, state.Category))
                        .ToList()))
                .ToList());
    }

    public Task<ProcessDefinitionRow?> FindByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        Asked.Add(organizationId);

        if (ThrowOnRead is not null)
        {
            throw ThrowOnRead;
        }

        return Task.FromResult(Answer);
    }
}
