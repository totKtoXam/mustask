package mustask.keycloak.guardfixtures;

import org.keycloak.models.UserModel;

/** A forbidden credential read, for ReadOnlyGuardIsNotVacuousTest only. */
public final class ReadsCredentials {

    public Object credentials(UserModel user) {
        return user.credentialManager();
    }
}
