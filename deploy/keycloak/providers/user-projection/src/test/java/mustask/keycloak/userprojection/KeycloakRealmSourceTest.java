package mustask.keycloak.userprojection;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

import java.util.List;
import java.util.Map;
import java.util.function.Function;
import java.util.stream.Stream;
import org.junit.jupiter.api.Test;
import org.keycloak.models.KeycloakSession;
import org.keycloak.models.KeycloakSessionFactory;
import org.keycloak.models.UserModel;
import org.mockito.ArgumentMatchers;

/** ADR-0025 § 5: the reconciliation's in-process reads (BR-UP-9, BR-UP-13). */
class KeycloakRealmSourceTest {

    private final KeycloakFixture keycloak = new KeycloakFixture();

    private final TransactionRunner inTheFixturesSession = new TransactionRunner() {
        @Override
        public <T> T call(KeycloakSessionFactory factory, Function<KeycloakSession, T> task) {
            return task.apply(keycloak.session);
        }
    };

    private final KeycloakRealmSource source =
            new KeycloakRealmSource(mock(KeycloakSessionFactory.class), keycloak.clock, inTheFixturesSession);

    // US-2 "A service-account user is never projected": the id list asks Keycloak for users without service accounts.
    @Test
    void enumerate_takesTheMarkerFirst_thenListsTheIdsWithoutServiceAccounts() {
        UserModel anna = keycloak.user("u-1", "anna", "Анна", null, null, true);
        UserModel ivan = keycloak.user("u-2", "ivan", "Иван", null, null, true);
        when(keycloak.users.searchForUserStream(eq(keycloak.realm), ArgumentMatchers.<Map<String, String>>any())).thenAnswer(invocation -> Stream.of(anna, ivan));

        RealmSource.Enumeration enumeration = source.enumerate();

        assertEquals(List.of("u-1", "u-2"), enumeration.ids());
        assertTrue(enumeration.enumerationStart() < keycloak.clock.next(), "the enumeration start is taken before the reads that follow");
        verify(keycloak.users).searchForUserStream(keycloak.realm, Map.of(UserModel.INCLUDE_SERVICE_ACCOUNT, "false"));
        // Live finding: a background session must be bound to the realm before Keycloak's user lookups (organizations).
        verify(keycloak.context).setRealm(keycloak.realm);
    }

    @Test
    void enumerate_withoutTheRealm_isNull() {
        when(keycloak.realms.getRealmByName(ProjectedRealm.NAME)).thenReturn(null);

        assertNull(source.enumerate());
    }

    // ADR-0025 § 5: each entry's marker taken before its read; users gone since the list, and service accounts, skipped.
    @Test
    void read_stampsEachEntry_skippingGoneUsersAndServiceAccounts() {
        keycloak.user("u-1", "anna", "Анна", "Смирнова", "anna@example.test", true);
        keycloak.serviceAccount("sa-1");
        keycloak.user("u-3", "ivan", "Иван", null, null, false);

        List<UserEntry> entries = source.read(List.of("u-1", "sa-1", "gone", "u-3"));

        assertEquals(List.of("u-1", "u-3"), entries.stream().map(UserEntry::id).toList());
        assertTrue(entries.get(0).moment() < entries.get(1).moment());
        assertEquals(new UserEntry("u-1", entries.get(0).moment(), "anna", "Анна", "Смирнова", "anna@example.test", true, false), entries.get(0));
        assertFalse(entries.get(1).enabled());
    }

    // Security pass S1: a page of the snapshot must not carry what MUSTASK cannot parse — one such user would make
    // MUSTASK refuse the page (400) on every run, and no reconciliation would ever complete.
    @Test
    void read_makesEveryStringWellFormed_soNoUserCanPoisonAPage() {
        keycloak.user("u-1", "anna", "Сам\uD800", "Смирнова", "anna@example.test", true);
        keycloak.user("u-2", "iv\u0000an", "Иван", "\uDC00", null, true);

        List<UserEntry> entries = source.read(List.of("u-1", "u-2"));

        assertEquals("Сам�", entries.get(0).firstName());
        assertEquals("ivan", entries.get(1).username());
        assertEquals("�", entries.get(1).lastName());
    }
}
