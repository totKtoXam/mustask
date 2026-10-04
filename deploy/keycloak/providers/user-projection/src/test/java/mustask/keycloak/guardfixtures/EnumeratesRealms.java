package mustask.keycloak.guardfixtures;

import org.keycloak.models.RealmProvider;

/** A forbidden realm enumeration (BR-UP-13: realm mustask only), for ReadOnlyGuardIsNotVacuousTest only. */
public final class EnumeratesRealms {

    public long count(RealmProvider realms) {
        return realms.getRealmsStream().count();
    }
}
