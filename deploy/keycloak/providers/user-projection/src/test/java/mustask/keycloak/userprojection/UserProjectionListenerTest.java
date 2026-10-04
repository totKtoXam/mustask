package mustask.keycloak.userprojection;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

import java.util.List;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.keycloak.events.Event;
import org.keycloak.events.EventType;
import org.keycloak.events.admin.AdminEvent;
import org.keycloak.events.admin.OperationType;
import org.keycloak.events.admin.ResourceType;
import org.keycloak.models.RealmModel;

/** ADR-0025 § 2, the event sources; mod-014 US-2, US-3 (the provider half). */
class UserProjectionListenerTest {

    private final KeycloakFixture keycloak = new KeycloakFixture();

    private final UserProjectionListener listener = new UserProjectionListener(keycloak.session, keycloak.capture);

    private static AdminEvent adminEvent(ResourceType type, OperationType operation, String path, String realmId) {
        AdminEvent event = mock(AdminEvent.class);
        when(event.getResourceType()).thenReturn(type);
        when(event.getOperationType()).thenReturn(operation);
        when(event.getResourcePath()).thenReturn(path);
        when(event.getRealmId()).thenReturn(realmId);

        return event;
    }

    private static Event userEvent(EventType type, String userId, String realmId) {
        Event event = mock(Event.class);
        when(event.getType()).thenReturn(type);
        when(event.getUserId()).thenReturn(userId);
        when(event.getRealmId()).thenReturn(realmId);

        return event;
    }

    // US-2 "A user created in the console becomes findable" — the admin CREATE event pushes the user's whole state, id = sub.
    @Test
    void adminCreate_queuesTheUsersProjectedStateOnCommit() {
        keycloak.user("u-1", "a.smirnova", "Анна", "Смирнова", "anna.s@example.test", true);

        listener.onEvent(adminEvent(ResourceType.USER, OperationType.CREATE, "users/u-1", KeycloakFixture.REALM_ID), false);

        assertEquals(0, keycloak.pending.size(), "nothing is queued before the commit");
        keycloak.commit();

        UserEntry entry = keycloak.pending.take(10).get(0);
        assertEquals("u-1", entry.id());
        assertEquals("a.smirnova", entry.username());
        assertEquals("Анна", entry.firstName());
        assertEquals("Смирнова", entry.lastName());
        assertEquals("anna.s@example.test", entry.email());
        assertTrue(entry.enabled());
        assertTrue(entry.moment() > 0);
    }

    // US-2 "A change of name is followed", "A change of e-mail is followed", "A change of username alone
    // moves the fallback display name"; US-3 "A disabled account becomes inactive" — any console edit is an UPDATE.
    @Test
    void adminUpdate_queuesTheNewState_includingADisable() {
        keycloak.user("u-1", "a.kuznetsova", "Анна", "Кузнецова", "anna.k@example.test", false);

        listener.onEvent(adminEvent(ResourceType.USER, OperationType.UPDATE, "users/u-1", KeycloakFixture.REALM_ID), true);
        keycloak.commit();

        UserEntry entry = keycloak.pending.take(10).get(0);
        assertEquals("Кузнецова", entry.lastName());
        assertEquals("a.kuznetsova", entry.username());
        assertFalse(entry.enabled());
    }

    // US-2 "A user created already disabled is inactive from the start".
    @Test
    void adminCreateOfADisabledUser_queuesItDisabled() {
        keycloak.user("u-2", "disabled", "Пётр", "Волков", null, false);

        listener.onEvent(adminEvent(ResourceType.USER, OperationType.CREATE, "users/u-2", KeycloakFixture.REALM_ID), false);
        keycloak.commit();

        assertFalse(keycloak.pending.take(10).get(0).enabled());
    }

    // ADR-0025 § 4: a rolled-back change is never pushed.
    @Test
    void aRolledBackChange_isNeverQueued() {
        keycloak.user("u-1", "a.smirnova", "Анна", "Смирнова", null, true);

        listener.onEvent(adminEvent(ResourceType.USER, OperationType.UPDATE, "users/u-1", KeycloakFixture.REALM_ID), false);
        keycloak.rollback();

        assertEquals(0, keycloak.pending.size());
    }

    // US-2 "A user's own profile change is carried like an administrator's"; ADR-0025 § 2's user events.
    @ParameterizedTest
    @ValueSource(strings = {"REGISTER", "UPDATE_PROFILE", "UPDATE_EMAIL", "IDENTITY_PROVIDER_FIRST_LOGIN", "USER_DISABLED_BY_PERMANENT_LOCKOUT"})
    void theUserEventsThatChangeAProjectedField_queueTheState(String type) {
        keycloak.user("u-1", "a.smirnova", "Анна", "Петрова", null, true);

        listener.onEvent(userEvent(EventType.valueOf(type), "u-1", KeycloakFixture.REALM_ID));
        keycloak.commit();

        assertEquals("Петрова", keycloak.pending.take(10).get(0).lastName());
    }

    // Security pass S1: a user who puts a lone surrogate in their own name (account REST accepts it, 204) is
    // queued with it replaced by U+FFFD — MUSTASK's reader refuses the escape the surrogate becomes in JSON.
    @Test
    void anOwnProfileUpdateWithALoneSurrogateInTheName_isQueuedWellFormed() {
        keycloak.user("u-1", "a.smirnova", "Сам\uD800", "Смир\uDC00нова", "anna@example.test", true);

        listener.onEvent(userEvent(EventType.UPDATE_PROFILE, "u-1", KeycloakFixture.REALM_ID));
        keycloak.commit();

        UserEntry entry = keycloak.pending.take(10).get(0);
        assertEquals("Сам�", entry.firstName());
        assertEquals("Смир�нова", entry.lastName());
    }

    // US-2 "A change outside the projected fields leaves the projection as it is"; US-3 "A temporary brute-force
    // lockout is not inactivity" — every other user event, sign-ins and their errors included, is ignored.
    @Test
    void everyOtherUserEvent_isIgnoredWithoutReadingTheUser() {
        keycloak.user("u-1", "a.smirnova", "Анна", "Смирнова", null, true);

        for (EventType type : EventType.values()) {
            if (UserProjectionListener.USER_EVENTS.contains(type)) {
                continue;
            }

            listener.onEvent(userEvent(type, "u-1", KeycloakFixture.REALM_ID));
        }

        assertTrue(keycloak.afterCompletion.isEmpty());
        verify(keycloak.users, never()).getUserById(any(RealmModel.class), any());
    }

    @Test
    void theNamedNonProjectingUserEvents_areAmongTheIgnored() {
        for (String type : List.of("LOGIN", "LOGIN_ERROR", "LOGOUT", "USER_DISABLED_BY_TEMPORARY_LOCKOUT", "RESET_PASSWORD")) {
            assertFalse(UserProjectionListener.USER_EVENTS.contains(EventType.valueOf(type)), type);
        }
    }

    // US-2 "A change outside the projected fields": role and group mappings, credential resets, required actions.
    @Test
    void adminEventsOfOtherResourcesOperationsOrDeeperPaths_areIgnored() {
        keycloak.user("u-1", "a.smirnova", "Анна", "Смирнова", null, true);

        for (ResourceType type : ResourceType.values()) {
            if (type != ResourceType.USER) {
                listener.onEvent(adminEvent(type, OperationType.CREATE, "users/u-1", KeycloakFixture.REALM_ID), false);
            }
        }

        for (OperationType operation : OperationType.values()) {
            if (operation != OperationType.CREATE && operation != OperationType.UPDATE) {
                listener.onEvent(adminEvent(ResourceType.USER, operation, "users/u-1", KeycloakFixture.REALM_ID), false);
            }
        }

        for (String path : List.of("users/u-1/reset-password", "users/u-1/role-mappings/realm", "users/u-1/groups/g-1", "users/", "groups/g-1")) {
            listener.onEvent(adminEvent(ResourceType.USER, OperationType.UPDATE, path, KeycloakFixture.REALM_ID), false);
        }

        assertTrue(keycloak.afterCompletion.isEmpty());
    }

    // US-2 "A user of another realm is never projected" (BR-UP-13).
    @Test
    void eventsOfAnotherRealm_areIgnored() {
        keycloak.user("u-1", "admin", "Админ", null, null, true);

        listener.onEvent(adminEvent(ResourceType.USER, OperationType.CREATE, "users/u-1", KeycloakFixture.OTHER_REALM_ID), false);
        listener.onEvent(userEvent(EventType.UPDATE_PROFILE, "u-1", KeycloakFixture.OTHER_REALM_ID));
        listener.onEvent(userEvent(EventType.UPDATE_PROFILE, "u-1", null));

        assertTrue(keycloak.afterCompletion.isEmpty());
    }

    // US-2 "A service-account user is never projected" (BR-UP-2).
    @Test
    void aServiceAccountUser_isNeverQueued() {
        keycloak.serviceAccount("sa-1");

        listener.onEvent(adminEvent(ResourceType.USER, OperationType.UPDATE, "users/sa-1", KeycloakFixture.REALM_ID), false);
        keycloak.commit();

        assertEquals(0, keycloak.pending.size());
    }

    @Test
    void aUserGoneByTheTimeOfTheEvent_isIgnored() {
        listener.onEvent(adminEvent(ResourceType.USER, OperationType.UPDATE, "users/nobody", KeycloakFixture.REALM_ID), false);

        assertTrue(keycloak.afterCompletion.isEmpty());
    }

    @Test
    void userIdOf_acceptsExactlyUsersSlashId() {
        assertEquals("abc", UserProjectionListener.userIdOf("users/abc"));
        assertNull(UserProjectionListener.userIdOf("users/abc/groups"));
        assertNull(UserProjectionListener.userIdOf("users/"));
        assertNull(UserProjectionListener.userIdOf("clients/abc"));
        assertNull(UserProjectionListener.userIdOf(null));
    }
}
