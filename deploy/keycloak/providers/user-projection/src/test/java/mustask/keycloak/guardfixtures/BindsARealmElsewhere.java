package mustask.keycloak.guardfixtures;

import org.keycloak.models.KeycloakSession;
import org.keycloak.models.RealmModel;

/** A realm binding outside ProjectedRealm, for ReadOnlyGuardIsNotVacuousTest only. */
public final class BindsARealmElsewhere {

    public void bind(KeycloakSession session, RealmModel realm) {
        session.getContext().setRealm(realm);
    }
}
