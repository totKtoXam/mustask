package mustask.keycloak.guardfixtures;

import org.keycloak.models.RealmModel;
import org.keycloak.models.RealmProvider;

/** A realm lookup outside ProjectedRealm, for ReadOnlyGuardIsNotVacuousTest only. */
public final class LooksUpAnotherRealm {

    public RealmModel master(RealmProvider realms) {
        return realms.getRealmByName("master");
    }
}
