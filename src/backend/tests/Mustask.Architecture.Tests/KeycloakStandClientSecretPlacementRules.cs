using System.Text.RegularExpressions;

namespace Mustask.Architecture.Tests;

/// <summary>
/// The live bug this guards against was not in <c>Mustask.AppHost.Provisioning.KeycloakStandClientSecret</c>
/// itself — that class did not exist yet — it was in *where* `AppHost.cs`
/// minted the value: inside the `ResourceReadyEvent` subscriber lambda, so
/// every re-firing generated a fresh secret and re-provisioned `mustask-e2e`
/// with it, rotating the live client's secret out from under a stand file
/// the suite had already read. `KeycloakStandClientSecretTests`'s two unit
/// tests (stable per instance, distinct across instances) both stay green
/// if a future edit moves the constructor call back inside the subscriber —
/// neither looks at `AppHost.cs` at all. This is the source-level fact that
/// does, the same idiom <see cref="StandOnlyKeycloakClientRules.StandOnlyProvisioner_ExistsInAppHost_UnderTheExpectedNamespace"/>
/// already uses for its own "not vacuous" check.
/// </summary>
public sealed class KeycloakStandClientSecretPlacementRules
{
    private const string AppHostFileName = "AppHost.cs";

    private const string SubscribeCall = "builder.Eventing.Subscribe<ResourceReadyEvent>";

    [Fact]
    public void AppHostCs_ConstructsTheClientSecretExactlyOnce_BeforeSubscribingToResourceReadyEvent()
    {
        var appHostFile = RepositoryLayout.SourceFilesOf("Mustask.AppHost")
            .SingleOrDefault(file => file.Name == AppHostFileName)
            ?? throw new InvalidOperationException(
                $"Не нашёл {AppHostFileName} в Mustask.AppHost — эта проверка не проверяет ничего.");

        var text = File.ReadAllText(appHostFile.FullName);

        var constructorCalls = Regex.Matches(text, @"new\s+KeycloakStandClientSecret\s*\(");

        Assert.True(
            constructorCalls.Count == 1,
            $"Нашёл {constructorCalls.Count} вызов(ов) `new KeycloakStandClientSecret(` в {AppHostFileName}, "
            + "ожидался ровно один. Значение обязано быть одно на процесс AppHost "
            + "— второй конструктор (в частности, внутри обработчика ResourceReadyEvent) воспроизводит живую "
            + "находку: каждое повторное срабатывание минтит новый секрет и перевыпускает клиента, "
            + "роняя уже прочитанный suite-ом stand-файл.");

        var subscribeIndex = text.IndexOf(SubscribeCall, StringComparison.Ordinal);

        Assert.True(
            subscribeIndex >= 0,
            $"Не нашёл `{SubscribeCall}` в {AppHostFileName} — эта проверка ожидает ровно один такой вызов, "
            + "тот, что провижнит стенд-клиент.");

        Assert.True(
            constructorCalls[0].Index < subscribeIndex,
            $"`new KeycloakStandClientSecret(` в {AppHostFileName} находится не раньше `{SubscribeCall}` — "
            + "то есть либо внутри обработчика, либо после подписки в порядке, который эта проверка не "
            + "признаёт безопасным. Значение обязано быть создано один раз до подписки, не при каждом её "
            + "срабатывании.");
    }
}
