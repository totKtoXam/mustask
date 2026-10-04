package mustask.keycloak.userprojection;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import org.junit.jupiter.api.Test;
import org.keycloak.models.RealmModel;
import org.keycloak.models.UserModel;
import org.keycloak.provider.ProviderEvent;

/** ADR-0025 § 2, deletions; mod-014 US-3 (the provider half). */
class UserRemovalListenerTest {

    private final KeycloakFixture keycloak = new KeycloakFixture();

    private Boolean enabled = Boolean.TRUE;

    private final ListenerEnablementWatch watch =
            new ListenerEnablementWatch(() -> ListenerEnablementWatch.Sighting.of(enabled), () -> { }, () -> { });

    private final UserRemovalListener listener = new UserRemovalListener(keycloak.capture, watch);

    private UserModel.UserRemovedEvent removal(RealmModel realm, UserModel user) {
        UserModel.UserRemovedEvent event = mock(UserModel.UserRemovedEvent.class);
        when(event.getRealm()).thenReturn(realm);
        when(event.getUser()).thenReturn(user);
        when(event.getKeycloakSession()).thenReturn(keycloak.session);

        return event;
    }

    // US-3 "A deleted account becomes inactive for good and keeps its name" — the deletion carries the last state.
    @Test
    void aRemovalInRealmMustask_queuesADeletionCarryingTheLastStateOnCommit() {
        watch.check();
        UserModel user = keycloak.user("u-1", "o.belova", "Ольга", "Белова", "o.belova@example.test", true);

        listener.onEvent(removal(keycloak.realm, user));
        keycloak.commit();

        UserEntry entry = keycloak.pending.take(10).get(0);
        assertTrue(entry.deleted());
        assertFalse(entry.enabled());
        assertEquals("Белова", entry.lastName());
        assertEquals("o.belova@example.test", entry.email());
        assertEquals("o.belova", entry.username());
    }

    // Security pass S1: the deletion carries the last state, cleaned like every other path's.
    @Test
    void aRemovalOfAUserWithALoneSurrogateInTheName_carriesTheNameWellFormed() {
        watch.check();
        UserModel user = keycloak.user("u-1", "o.belova", "Оль\uD800га", "Белова\u0000", "o.belova@example.test", true);

        listener.onEvent(removal(keycloak.realm, user));
        keycloak.commit();

        UserEntry entry = keycloak.pending.take(10).get(0);
        assertTrue(entry.deleted());
        assertEquals("Оль�га", entry.firstName());
        assertEquals("Белова", entry.lastName());
    }

    @Test
    void aRemovalBeforeTheFirstEnablementCheck_isCapturedAndHeld() {
        UserModel user = keycloak.user("u-1", "o.belova", "Ольга", "Белова", null, true);

        listener.onEvent(removal(keycloak.realm, user));
        keycloak.commit();

        assertEquals(1, keycloak.pending.size());
    }

    // US-2 "A user of another realm is never projected" — its removal neither.
    @Test
    void aRemovalInAnotherRealm_isIgnored() {
        watch.check();
        UserModel user = keycloak.user("u-1", "admin", null, null, null, true);

        listener.onEvent(removal(keycloak.otherRealm, user));

        assertTrue(keycloak.afterCompletion.isEmpty());
    }

    // US-5 "A disabled provider freezes the projection, not the reads".
    @Test
    void aRemovalWhileTheListenerIsDisabled_isIgnored() {
        enabled = Boolean.FALSE;
        watch.check();
        UserModel user = keycloak.user("u-1", "o.belova", "Ольга", "Белова", null, true);

        listener.onEvent(removal(keycloak.realm, user));

        assertTrue(keycloak.afterCompletion.isEmpty());
    }

    // US-2 "A service-account user is never projected" — nor its removal.
    @Test
    void aServiceAccountsRemoval_isIgnored() {
        watch.check();

        listener.onEvent(removal(keycloak.realm, keycloak.serviceAccount("sa-1")));
        keycloak.commit();

        assertEquals(0, keycloak.pending.size());
    }

    @Test
    void anotherProviderEvent_isIgnored() {
        listener.onEvent(mock(ProviderEvent.class));

        assertTrue(keycloak.afterCompletion.isEmpty());
    }
}
