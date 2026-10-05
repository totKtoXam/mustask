using Mustask.Application.Abstractions;

namespace Mustask.Application.Tests.TestDoubles;

public sealed class FakeCurrentUser : ICurrentUser
{
    public string DisplayName { get; set; } = "test-user";

    public string SubjectId { get; set; } = "0b6b3c5e-7d1a-4c39-9f1e-5a2b8c4d6e0f";
}
