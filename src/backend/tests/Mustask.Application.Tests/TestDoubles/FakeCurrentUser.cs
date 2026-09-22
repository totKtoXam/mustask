using Mustask.Application.Abstractions;

namespace Mustask.Application.Tests.TestDoubles;

public sealed class FakeCurrentUser : ICurrentUser
{
    public string DisplayName { get; set; } = "test-user";
}
