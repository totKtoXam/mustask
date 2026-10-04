package mustask.keycloak.userprojection;

import org.keycloak.models.KeycloakSession;
import org.keycloak.models.RealmModel;

/**
 * The one realm the provider projects (BR-UP-13): {@code mustask}. This is
 * the only class allowed to look a realm up, and it looks up only this one
 * (ReadOnlyGuardTest).
 */
public final class ProjectedRealm {

    /** The projected realm's name. */
    public static final String NAME = "mustask";

    private ProjectedRealm() {
    }

    /** Realm {@code mustask} in this session, or null when it does not exist (yet). */
    public static RealmModel find(KeycloakSession session) {
        return session.realms().getRealmByName(NAME);
    }

    /**
     * Realm {@code mustask}, bound to {@code session}'s context — or null when
     * it does not exist. The provider's own background sessions
     * ({@code runJobInTransaction}) are bound to no realm, and Keycloak's user
     * lookups require one (its organizations check reads the context realm).
     * This sets session state only, for the one realm projected; nothing is
     * written to Keycloak's model (ReadOnlyGuardTest allows it here only).
     */
    public static RealmModel bind(KeycloakSession session) {
        RealmModel realm = find(session);

        if (realm != null) {
            session.getContext().setRealm(realm);
        }

        return realm;
    }

    /** Whether {@code realmId} is realm {@code mustask}'s id. */
    public static boolean isProjected(KeycloakSession session, String realmId) {
        if (realmId == null) {
            return false;
        }

        RealmModel realm = find(session);

        return realm != null && realmId.equals(realm.getId());
    }
}
