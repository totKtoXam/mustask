package mustask.keycloak.userprojection;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verifyNoInteractions;
import static org.mockito.Mockito.when;

import java.util.List;
import org.junit.jupiter.api.Test;
import org.keycloak.Config;
import org.keycloak.events.EventListenerProvider;
import org.keycloak.models.KeycloakSession;
import org.keycloak.models.KeycloakSessionFactory;

/** ADR-0025 § 4: a misconfigured feed logs an error and stays inert; it never stops Keycloak. */
class UserProjectionListenerFactoryTest {

    private static final String SHORT_SECRET = "short-secret-value";

    @Test
    void theProviderId_isTheOneTheRealmImportEnables() {
        assertEquals("mustask-user-projection", new UserProjectionListenerFactory().getId());
    }

    // US-6 "A provider with the wrong credential fails loudly" — and a missing or short one too; its value is never printed.
    @Test
    void invalidOptions_logAnErrorAndLeaveTheProviderInert() {
        Config.Scope scope = mock(Config.Scope.class);
        when(scope.get(FeedOptions.ENDPOINT)).thenReturn("http://mustask.test");
        when(scope.get(FeedOptions.PUSH_SECRET)).thenReturn(SHORT_SECRET);
        when(scope.getBoolean(FeedOptions.REQUIRE_HTTPS, Boolean.TRUE)).thenReturn(Boolean.FALSE);
        when(scope.getInt(FeedOptions.RECONCILE_PERIOD_MINUTES, FeedOptions.DEFAULT_RECONCILE_PERIOD_MINUTES)).thenReturn(60);
        UserProjectionListenerFactory factory = new UserProjectionListenerFactory();
        KeycloakSessionFactory sessions = mock(KeycloakSessionFactory.class);

        try (LogCapture log = LogCapture.of(UserProjectionListenerFactory.class)) {
            factory.init(scope);
            factory.postInit(sessions);

            List<String> errors = log.messagesAtLeast(LogCapture.ERROR);
            assertEquals(1, errors.size(), log.text());
            assertTrue(errors.get(0).contains(FeedOptions.PUSH_SECRET));
            assertFalse(log.text().contains(SHORT_SECRET));
        }

        verifyNoInteractions(sessions);
        EventListenerProvider listener = factory.create(mock(KeycloakSession.class));
        assertFalse(listener instanceof UserProjectionListener);
        factory.close();
    }

    @Test
    void beforePostInit_theListenerIsInert() {
        assertInstanceOf(EventListenerProvider.class, new UserProjectionListenerFactory().create(mock(KeycloakSession.class)));
    }
}
