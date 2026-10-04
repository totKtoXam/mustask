package mustask.keycloak.userprojection;

import java.net.URI;
import java.net.URISyntaxException;
import java.time.Duration;
import org.keycloak.Config;

/**
 * The provider's SPI options (plan-backend.md § 1; ADR-0025 § 3), read from
 * Keycloak's configuration only — on the stand, environment variables of the
 * Keycloak process (US-6, "the credential comes only from environment or user
 * secrets"). An invalid option is named in the error, its value never.
 *
 * @param endpoint {@code Mustask.Api}'s base address; the feed's routes are below it
 * @param pushSecret the shared secret requests are signed with (at least 32 characters)
 * @param reconcilePeriod how often a reconciliation runs (at least one minute)
 */
public record FeedOptions(URI endpoint, String pushSecret, Duration reconcilePeriod) {

    public static final String ENDPOINT = "endpoint";

    public static final String PUSH_SECRET = "push-secret";

    public static final String REQUIRE_HTTPS = "require-https";

    public static final String RECONCILE_PERIOD_MINUTES = "reconcile-period-minutes";

    /** The shortest secret accepted, on both sides (api-contract.md, Part D). */
    public static final int MINIMUM_SECRET_LENGTH = 32;

    /** BR-UP-8, D-8. */
    public static final int DEFAULT_RECONCILE_PERIOD_MINUTES = 60;

    /** Reads and validates the options; throws {@link IllegalArgumentException} naming the bad option. */
    public static FeedOptions from(Config.Scope scope) {
        String endpointText = scope.get(ENDPOINT);
        String secret = scope.get(PUSH_SECRET);
        boolean requireHttps = scope.getBoolean(REQUIRE_HTTPS, Boolean.TRUE);
        int periodMinutes = scope.getInt(RECONCILE_PERIOD_MINUTES, DEFAULT_RECONCILE_PERIOD_MINUTES);

        return of(endpointText, secret, requireHttps, periodMinutes);
    }

    /** The validation itself, without Keycloak's configuration type. */
    public static FeedOptions of(String endpointText, String secret, boolean requireHttps, int periodMinutes) {
        if (endpointText == null || endpointText.isBlank()) {
            throw new IllegalArgumentException("option '" + ENDPOINT + "' is required");
        }

        URI endpoint;

        try {
            endpoint = new URI(endpointText.trim());
        } catch (URISyntaxException ex) {
            throw new IllegalArgumentException("option '" + ENDPOINT + "' is not a URI", ex);
        }

        String scheme = endpoint.getScheme();

        if (!endpoint.isAbsolute() || endpoint.getHost() == null || !("https".equals(scheme) || "http".equals(scheme))) {
            throw new IllegalArgumentException("option '" + ENDPOINT + "' must be an absolute http(s) address");
        }

        if (requireHttps && !"https".equals(scheme)) {
            throw new IllegalArgumentException(
                    "option '" + ENDPOINT + "' is not https; only a stand sets '" + REQUIRE_HTTPS + "' to false");
        }

        if (secret == null || secret.length() < MINIMUM_SECRET_LENGTH) {
            throw new IllegalArgumentException(
                    "option '" + PUSH_SECRET + "' is missing or shorter than " + MINIMUM_SECRET_LENGTH + " characters");
        }

        if (periodMinutes < 1) {
            throw new IllegalArgumentException("option '" + RECONCILE_PERIOD_MINUTES + "' must be at least 1");
        }

        return new FeedOptions(endpoint, secret, Duration.ofMinutes(periodMinutes));
    }

    /** Never prints the secret. */
    @Override
    public String toString() {
        return "FeedOptions[endpoint=" + endpoint + ", reconcilePeriod=" + reconcilePeriod + "]";
    }
}
