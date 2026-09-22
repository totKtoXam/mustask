namespace Mustask.Api.Authentication.Readiness;

/// <summary>Почему realm ещё не готов.</summary>
public enum RealmReadinessReason
{
    /// <summary>Discovery-документ не получен (сеть, таймаут, отказ Keycloak).</summary>
    RealmUnavailable,

    /// <summary>Discovery получен, <c>issuer</c> совпал, но набор ключей JWKS пуст.</summary>
    EmptyKeySet,

    /// <summary>Discovery получен, но <c>issuer</c> не совпадает с ожидаемым.</summary>
    IssuerMismatch,
}

/// <summary>Снимок состояния готовности realm'а на момент последней проверки.</summary>
public sealed record RealmReadinessState(
    bool IsReady,
    RealmReadinessReason? Reason,
    string? ExpectedIssuer = null,
    string? ActualIssuer = null)
{
    public static readonly RealmReadinessState Ready = new(true, Reason: null);

    public static RealmReadinessState NotReady(
        RealmReadinessReason reason, string? expectedIssuer = null, string? actualIssuer = null) =>
        new(false, reason, expectedIssuer, actualIssuer);
}

/// <summary>
/// Потокобезопасное состояние готовности realm'а:
/// <see cref="RealmReadinessState.Ready"/> или
/// <see cref="RealmReadinessState.NotReady"/>. Пишет только
/// <see cref="RealmReadinessMonitor"/>, читают гейт 503 и health-check на
/// каждый запрос.
/// </summary>
public sealed class RealmReadiness
{
    private volatile RealmReadinessState _current =
        RealmReadinessState.NotReady(RealmReadinessReason.RealmUnavailable);

    public RealmReadinessState Current => _current;

    public void Set(RealmReadinessState state) => _current = state;
}
