using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Mustask.Api.Authentication.Readiness;

/// <summary>
/// Проверяет discovery-документ realm'а до
/// старта Kestrel, затем раз в 30 секунд в фоне. Регистрируется двумя
/// строками — <c>AddSingleton&lt;RealmReadinessMonitor&gt;()</c> и
/// <c>AddHostedService(sp =&gt; sp.GetRequiredService&lt;RealmReadinessMonitor&gt;())</c>
/// — чтобы тесты могли резолвить и звать <see cref="CheckAsync"/> напрямую,
/// без ожидания цикла (подключение — шаг 5).
/// </summary>
public sealed class RealmReadinessMonitor(
    IConfigurationManager<OpenIdConnectConfiguration> configurationManager,
    RealmReadiness readiness,
    KeycloakOptions options,
    ILogger<RealmReadinessMonitor> logger) : BackgroundService
{
    /// <summary>≤ 1 минута по требованию; 30 с — с запасом и строго меньше двойного <see cref="RealmMetadata.RefreshInterval"/>.</summary>
    public static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

    private bool _loggedReadyOnce;

    /// <summary>
    /// Стартовая проверка — до <c>base.StartAsync</c> (в minimal hosting
    /// <c>GenericWebHostService</c> регистрируется последним, поэтому
    /// hosted-сервисы приложения стартуют раньше сервера). Решает по
    /// возвращённому состоянию, не по исключению: <see cref="RealmReadinessReason.IssuerMismatch"/>
    /// — единственная причина прервать старт («issuer не совпадает»).
    /// </summary>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var state = await CheckAsyncCore(isFirstCheck: true, cancellationToken);

        if (state.Reason == RealmReadinessReason.IssuerMismatch)
        {
            throw new InvalidOperationException(
                $"Keycloak realm issuer mismatch: expected «{state.ExpectedIssuer}», got «{state.ActualIssuer}».");
        }

        await base.StartAsync(cancellationToken);
    }

    /// <summary>
    /// Цикл фона. Ни одно исключение наружу не выходит — умолчание хоста
    /// (<c>BackgroundServiceExceptionBehavior.StopHost</c>) иначе остановило
    /// бы весь <c>Mustask.Api</c> на первом сбое проверки, что прямо запрещено
    /// («старт не прерывается, повтор в фоне»).
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(CheckInterval);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CheckAsyncCore(isFirstCheck: false, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Штатная остановка приложения — не ошибка, состояние готовности не трогаем.
        }
    }

    /// <summary>Публичный — тесты дёргают проверку без ожидания тика таймера.</summary>
    public Task<RealmReadinessState> CheckAsync(CancellationToken cancellationToken = default) =>
        CheckAsyncCore(isFirstCheck: false, cancellationToken);

    private async Task<RealmReadinessState> CheckAsyncCore(bool isFirstCheck, CancellationToken cancellationToken)
    {
        RealmReadinessState state;

        try
        {
            // Каждый вызов, кроме первого, начинается с RequestRefresh() —
            // первый ничего не обновляет, потому что ConfigurationManager
            // ещё не получал документ.
            if (!isFirstCheck)
            {
                configurationManager.RequestRefresh();
            }

            var configuration = await configurationManager.GetConfigurationAsync(cancellationToken);

            if (!string.Equals(configuration.Issuer, options.Issuer, StringComparison.Ordinal))
            {
                logger.LogError(
                    "Keycloak realm issuer mismatch: expected {ExpectedIssuer}, got {ActualIssuer}.",
                    options.Issuer,
                    configuration.Issuer);

                state = RealmReadinessState.NotReady(
                    RealmReadinessReason.IssuerMismatch, options.Issuer, configuration.Issuer);
            }
            else if (configuration.SigningKeys.Count == 0)
            {
                logger.LogWarning("Keycloak realm {Authority} responded but its signing key set is empty.", options.Authority);

                state = RealmReadinessState.NotReady(RealmReadinessReason.EmptyKeySet);
            }
            else
            {
                if (!_loggedReadyOnce)
                {
                    logger.LogInformation(
                        "Keycloak realm {Authority} is ready, {KeyCount} signing key(s).",
                        options.Authority,
                        configuration.SigningKeys.Count);
                    _loggedReadyOnce = true;
                }

                state = RealmReadinessState.Ready;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Любое исключение проверки (HttpRequestException, таймаут,
            // разбор документа, отказ логгера) — NotReady(RealmUnavailable),
            // а не исключение наружу.
            logger.LogError(ex, "Keycloak realm {Authority} is unavailable.", options.Authority);
            state = RealmReadinessState.NotReady(RealmReadinessReason.RealmUnavailable);
        }

        // После Ready недоступность realm'а не сбрасывает готовность —
        // ConfigurationManager отдаёт кэш, проверка токенов не ходит в
        // Keycloak. IssuerMismatch — единственная причина,
        // переводящая уже готовый realm обратно в NotReady, и делает это
        // бессрочно.
        if (readiness.Current.IsReady
            && state.Reason is RealmReadinessReason.RealmUnavailable or RealmReadinessReason.EmptyKeySet)
        {
            return readiness.Current;
        }

        readiness.Set(state);
        return state;
    }
}
