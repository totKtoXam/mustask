package mustask.keycloak.userprojection;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import org.keycloak.models.KeycloakSessionFactory;
import org.keycloak.models.RealmModel;
import org.keycloak.models.UserModel;
import org.keycloak.models.UserProvider;

/**
 * {@link RealmSource} in-process (BR-UP-9): Keycloak's own user provider in
 * short transactions of its own, no REST surface, reading only the projected
 * fields (BR-UP-13).
 */
public final class KeycloakRealmSource implements RealmSource {

    /** Users that are not service accounts — the query parameter Keycloak's user search understands. */
    private static final Map<String, String> WITHOUT_SERVICE_ACCOUNTS =
            Map.of(UserModel.INCLUDE_SERVICE_ACCOUNT, Boolean.FALSE.toString());

    private final KeycloakSessionFactory factory;

    private final MomentClock clock;

    private final TransactionRunner transactions;

    public KeycloakRealmSource(KeycloakSessionFactory factory, MomentClock clock, TransactionRunner transactions) {
        this.factory = Objects.requireNonNull(factory, "factory");
        this.clock = Objects.requireNonNull(clock, "clock");
        this.transactions = Objects.requireNonNull(transactions, "transactions");
    }

    @Override
    public Enumeration enumerate() {
        return transactions.call(factory, session -> {
            RealmModel realm = ProjectedRealm.bind(session);

            if (realm == null) {
                return null;
            }

            long enumerationStart = clock.next();
            UserProvider users = session.users();
            List<String> ids = users.searchForUserStream(realm, WITHOUT_SERVICE_ACCOUNTS)
                    .map(UserModel::getId)
                    .toList();

            return new Enumeration(enumerationStart, ids);
        });
    }

    @Override
    public List<UserEntry> read(List<String> ids) {
        return transactions.call(factory, session -> {
            RealmModel realm = ProjectedRealm.bind(session);
            List<UserEntry> entries = new ArrayList<>(ids.size());

            if (realm == null) {
                return entries;
            }

            UserProvider users = session.users();

            for (String id : ids) {
                long moment = clock.next();
                UserModel user = users.getUserById(realm, id);

                if (user == null || ProjectedUserReader.isServiceAccount(user)) {
                    continue;
                }

                entries.add(ProjectedUserReader.read(user).at(moment, false));
            }

            return entries;
        });
    }
}
