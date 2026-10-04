package mustask.keycloak.userprojection;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import java.time.Duration;
import org.junit.jupiter.api.Test;
import org.keycloak.Config;

/**
 * mod-014 US-6 "The credential comes only from environment or user secrets":
 * the options come from Keycloak's configuration scope only — on the stand,
 * the Keycloak process's environment — and a bad one is named, never printed.
 */
class FeedOptionsTest {

    private static final String SECRET = "feed-options-test-secret-not-real-0123456789";

    private static Config.Scope scope(String endpoint, String secret, Boolean requireHttps, Integer period) {
        Config.Scope scope = mock(Config.Scope.class);
        when(scope.get(FeedOptions.ENDPOINT)).thenReturn(endpoint);
        when(scope.get(FeedOptions.PUSH_SECRET)).thenReturn(secret);
        when(scope.getBoolean(FeedOptions.REQUIRE_HTTPS, Boolean.TRUE)).thenReturn(requireHttps == null ? Boolean.TRUE : requireHttps);
        when(scope.getInt(FeedOptions.RECONCILE_PERIOD_MINUTES, FeedOptions.DEFAULT_RECONCILE_PERIOD_MINUTES))
                .thenReturn(period == null ? FeedOptions.DEFAULT_RECONCILE_PERIOD_MINUTES : period);

        return scope;
    }

    @Test
    void theOptions_areReadFromTheConfigurationScope() {
        FeedOptions options = FeedOptions.from(scope("https://mustask.example.test", SECRET, null, null));

        assertEquals("https://mustask.example.test", options.endpoint().toString());
        assertEquals(SECRET, options.pushSecret());
        assertEquals(Duration.ofMinutes(60), options.reconcilePeriod());
    }

    // BR-UP-8, D-8: the period is configurable, at least one minute.
    @Test
    void thePeriod_isConfigurable_andAtLeastOneMinute() {
        assertEquals(Duration.ofMinutes(2), FeedOptions.from(scope("https://m.test", SECRET, null, 2)).reconcilePeriod());

        IllegalArgumentException refused = assertThrows(IllegalArgumentException.class, () -> FeedOptions.from(scope("https://m.test", SECRET, null, 0)));
        assertTrue(refused.getMessage().contains(FeedOptions.RECONCILE_PERIOD_MINUTES));
    }

    // ADR-0025 § 3: plain HTTP only where require-https is false — on the stand.
    @Test
    void plainHttp_isRefusedUnlessRequireHttpsIsFalse() {
        IllegalArgumentException refused = assertThrows(IllegalArgumentException.class, () -> FeedOptions.from(scope("http://m.test", SECRET, null, null)));
        assertTrue(refused.getMessage().contains(FeedOptions.REQUIRE_HTTPS));

        assertEquals("http", FeedOptions.from(scope("http://m.test", SECRET, false, null)).endpoint().getScheme());
    }

    @Test
    void aMissingEndpoint_isNamed() {
        IllegalArgumentException refused = assertThrows(IllegalArgumentException.class, () -> FeedOptions.from(scope(null, SECRET, null, null)));

        assertTrue(refused.getMessage().contains(FeedOptions.ENDPOINT));
    }

    // US-6 "The credential is never printed" — a short secret is named, its value is not.
    @Test
    void aMissingOrShortSecret_isNamedButNeverPrinted() {
        String shortSecret = "too-short-secret";

        IllegalArgumentException missing = assertThrows(IllegalArgumentException.class, () -> FeedOptions.from(scope("https://m.test", null, null, null)));
        IllegalArgumentException tooShort = assertThrows(IllegalArgumentException.class, () -> FeedOptions.from(scope("https://m.test", shortSecret, null, null)));

        assertTrue(missing.getMessage().contains(FeedOptions.PUSH_SECRET));
        assertTrue(tooShort.getMessage().contains(FeedOptions.PUSH_SECRET));
        assertFalse(tooShort.getMessage().contains(shortSecret));
    }

    @Test
    void toString_neverPrintsTheSecret() {
        assertFalse(FeedOptions.of("https://m.test", SECRET, true, 60).toString().contains(SECRET));
    }
}
