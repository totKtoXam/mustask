using Mustask.AppHost.Provisioning;

namespace Mustask.Contracts.Tests;

/// <summary>
/// mod-014 US-4 "Stands of two checkouts take turns on one external database"
/// (ADR-0025 § 7; D-10, D-12): the Keycloak store's volume name in
/// external-database mode depends on the database only — host, port, name —
/// so every checkout on it shares the store and another database never does.
/// </summary>
public sealed class StandStoreKeyTests
{
    private const string Base = "Host=localhost;Port=5432;Database=mustask_mod014;Username=postgres;Password=example-not-real";

    [Theory]
    [InlineData("Host=localhost;Port=5432;Database=mustask_mod014;Username=postgres;Password=example-not-real")]
    [InlineData("Host=127.0.0.1;Port=5432;Database=mustask_mod014;Username=postgres;Password=another-not-real")]
    [InlineData("Host=::1;Database=mustask_mod014;Username=someone;Password=x")]
    [InlineData("Server=LOCALHOST;Port=5432;Database=mustask_mod014")]
    [InlineData("host=localhost; port=5432; database=mustask_mod014; Include Error Detail=true")]
    public void TheSameDatabase_SpelledOrCredentialedDifferently_SharesOneStore(string connectionString)
    {
        Assert.Equal(StandStoreKey.ForExternalDatabase(Base), StandStoreKey.ForExternalDatabase(connectionString));
    }

    [Theory]
    [InlineData("Host=localhost;Port=5432;Database=mustask")]
    [InlineData("Host=localhost;Port=5433;Database=mustask_mod014")]
    [InlineData("Host=db.example.test;Port=5432;Database=mustask_mod014")]
    public void AnotherDatabase_NeverSharesTheStore(string connectionString)
    {
        Assert.NotEqual(StandStoreKey.ForExternalDatabase(Base), StandStoreKey.ForExternalDatabase(connectionString));
    }

    [Fact]
    public void TheName_IsThePrefixAndSixteenHexDigits_AndCarriesNoCredential()
    {
        var name = StandStoreKey.ForExternalDatabase(Base);

        Assert.Matches("^mustask-keycloak-[0-9a-f]{16}$", name);
        Assert.DoesNotContain("example-not-real", name, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTarget_NamesHostPortAndDatabase_NeverTheCredential()
    {
        var target = StandStoreKey.Target(Base);

        Assert.Equal(new DatabaseTarget("localhost", 5432, "mustask_mod014"), target);
        Assert.Equal("localhost:5432/mustask_mod014", target.ToString());
    }

    [Theory]
    [InlineData("Host=localhost;Port=5432")]
    [InlineData("Port=5432;Database=mustask")]
    [InlineData("Host=localhost;Port=abc;Database=mustask")]
    public void AConnectionStringWithoutHostOrDatabase_IsRefused(string connectionString)
    {
        Assert.Throws<InvalidOperationException>(() => StandStoreKey.ForExternalDatabase(connectionString));
    }
}
