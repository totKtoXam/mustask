namespace Mustask.Testing;

/// <summary>
/// The constants every test project used to declare for itself (tech-035): one creator, one fixed "now", one way to make a
/// name nobody else uses. The file is compiled into every test project by <c>tests/Directory.Build.props</c>, so there is one
/// implementation; <see cref="Name"/> is in <c>TestDataNames.cs</c> because it needs the domain.
/// </summary>
internal static partial class TestData
{
    /// <summary>The user a test organization is founded by: its only administrator unless the test adds others.</summary>
    public const string Creator = "1c9e2b7d-0000-4000-8000-00000000c0de";

    /// <summary>A fixed moment, for the code that takes the time as an argument.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A name no other test uses: the prefix and 32 hex digits (a shared database is never emptied).</summary>
    public static string UniqueName(string prefix = "Организация") => $"{prefix} {Guid.CreateVersion7():N}";
}
