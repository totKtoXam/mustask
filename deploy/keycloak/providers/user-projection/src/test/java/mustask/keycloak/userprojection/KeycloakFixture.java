package mustask.keycloak.userprojection;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.doAnswer;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.ArrayList;
import java.util.List;
import org.keycloak.models.KeycloakContext;
import org.keycloak.models.KeycloakSession;
import org.keycloak.models.KeycloakTransaction;
import org.keycloak.models.KeycloakTransactionManager;
import org.keycloak.models.RealmModel;
import org.keycloak.models.RealmProvider;
import org.keycloak.models.UserModel;
import org.keycloak.models.UserProvider;

/**
 * A mocked Keycloak session with realm {@code mustask} (and another realm),
 * its users, and a transaction manager that records after-completion tasks
 * so a test can commit or roll them back.
 */
final class KeycloakFixture {

    static final String REALM_ID = "realm-mustask-id";

    static final String OTHER_REALM_ID = "realm-master-id";

    final KeycloakSession session = mock(KeycloakSession.class);

    final RealmProvider realms = mock(RealmProvider.class);

    final UserProvider users = mock(UserProvider.class);

    final RealmModel realm = mock(RealmModel.class);

    final RealmModel otherRealm = mock(RealmModel.class);

    final KeycloakTransactionManager transactions = mock(KeycloakTransactionManager.class);

    final KeycloakContext context = mock(KeycloakContext.class);

    final List<KeycloakTransaction> afterCompletion = new ArrayList<>();

    final PendingChanges pending = new PendingChanges(PendingChanges.DEFAULT_CAPACITY, () -> { });

    final MomentClock clock = new MomentClock(Clock.fixed(Instant.parse("2026-10-02T10:00:00Z"), ZoneOffset.UTC));

    final ChangeCapture capture = new ChangeCapture(clock, pending);

    KeycloakFixture() {
        when(session.realms()).thenReturn(realms);
        when(session.users()).thenReturn(users);
        when(session.getTransactionManager()).thenReturn(transactions);
        when(session.getContext()).thenReturn(context);
        when(realms.getRealmByName(ProjectedRealm.NAME)).thenReturn(realm);
        when(realm.getId()).thenReturn(REALM_ID);
        when(realm.getName()).thenReturn(ProjectedRealm.NAME);
        when(otherRealm.getId()).thenReturn(OTHER_REALM_ID);
        when(otherRealm.getName()).thenReturn("master");

        doAnswer(invocation -> {
            afterCompletion.add(invocation.getArgument(0));
            return null;
        }).when(transactions).enlistAfterCompletion(any());
    }

    /** A user of realm {@code mustask}, findable by id. */
    UserModel user(String id, String username, String firstName, String lastName, String email, boolean enabled) {
        UserModel user = mock(UserModel.class);
        when(user.getId()).thenReturn(id);
        when(user.getUsername()).thenReturn(username);
        when(user.getFirstName()).thenReturn(firstName);
        when(user.getLastName()).thenReturn(lastName);
        when(user.getEmail()).thenReturn(email);
        when(user.isEnabled()).thenReturn(enabled);
        when(users.getUserById(realm, id)).thenReturn(user);

        return user;
    }

    /** The service-account user of a client. */
    UserModel serviceAccount(String id) {
        UserModel user = user(id, "service-account-" + id, null, null, null, true);
        when(user.getServiceAccountClientLink()).thenReturn("client-" + id);

        return user;
    }

    /** Every recorded after-completion task, committed as Keycloak commits them. */
    void commit() {
        for (KeycloakTransaction transaction : afterCompletion) {
            transaction.begin();
            transaction.commit();
        }

        afterCompletion.clear();
    }

    /** Every recorded after-completion task, rolled back. */
    void rollback() {
        for (KeycloakTransaction transaction : afterCompletion) {
            transaction.begin();
            transaction.rollback();
        }

        afterCompletion.clear();
    }
}
