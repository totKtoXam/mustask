namespace Mustask.AppHost.Provisioning;

/// <summary>
/// The command line of the live-realm migration (mod-016, BR-M1-28): <c>dotnet run --project src/backend/Mustask.AppHost --
/// --migrate-live-realm</c>, wrapped by <c>scripts/migrate-live-realm.sh</c> and <c>.ps1</c>. A stand-only command like
/// <c>--stand-reset</c> (ADR-0023, ADR-0025): it is handled before the distributed application is built, so Aspire never sees the
/// flag, no resource starts and nothing but the realm is touched. The administrator's password comes from the environment
/// (<c>KEYCLOAK_ADMIN_PASSWORD</c>) — never from an argument, which any user of the machine could read in the process list; the
/// address from <c>KEYCLOAK_URI</c> (default <c>http://localhost:5010</c>, the stand's Keycloak port).
/// </summary>
public static class KeycloakLiveRealmCommand
{
    /// <summary>The AppHost flag that turns a start into the migration.</summary>
    public const string Flag = "--migrate-live-realm";

    public const string PasswordVariable = "KEYCLOAK_ADMIN_PASSWORD";

    public const string UriVariable = "KEYCLOAK_URI";

    public const string DefaultUri = "http://localhost:5010";

    public static bool IsRequested(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        return args.Contains(Flag, StringComparer.Ordinal);
    }

    /// <summary>
    /// Runs the migration with the flag's arguments and the environment. <paramref name="handler"/> replaces the network (the tests'
    /// fake Keycloak); production passes <c>null</c>. Nothing here prints a value taken from an argument or from the environment.
    /// </summary>
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        Func<string, string?> environment,
        TextWriter output,
        HttpMessageHandler? handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(output);

        // The flag alone. A password given as an argument would sit in the process list; it is neither accepted nor echoed back.
        if (args.Count != 1 || args[0] != Flag)
        {
            output.WriteLine($"{Flag} takes no other argument ({args.Count - 1} given). The administrator's password is read from {PasswordVariable}; the address from {UriVariable}.");

            return KeycloakLiveRealmMigration.ExitStopped;
        }

        var password = environment(PasswordVariable);

        if (string.IsNullOrEmpty(password))
        {
            output.WriteLine(
                $"{PasswordVariable} is not set — it is the password of the master realm's admin. Get it from AppHost's user-secrets: "
                + "dotnet user-secrets list --project src/backend/Mustask.AppHost");

            return KeycloakLiveRealmMigration.ExitStopped;
        }

        var address = environment(UriVariable);

        if (!Uri.TryCreate(string.IsNullOrWhiteSpace(address) ? DefaultUri : address, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            output.WriteLine($"{UriVariable} is not an absolute http(s) address.");

            return KeycloakLiveRealmMigration.ExitStopped;
        }

        using var http = handler is null ? new HttpClient { BaseAddress = uri } : new HttpClient(handler, disposeHandler: false) { BaseAddress = uri };

        return await KeycloakLiveRealmMigration.RunAsync(
            http, new KeycloakLiveRealmMigrationOptions { AdminPassword = password }, output, cancellationToken);
    }
}
