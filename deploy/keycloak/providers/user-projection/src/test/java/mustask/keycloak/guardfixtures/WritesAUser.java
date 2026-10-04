package mustask.keycloak.guardfixtures;

import org.keycloak.models.UserModel;

/** A forbidden write, for ReadOnlyGuardIsNotVacuousTest only — never part of the provider. */
public final class WritesAUser {

    public void disable(UserModel user) {
        user.setEnabled(false);
    }
}
