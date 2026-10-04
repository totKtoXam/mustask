package mustask.keycloak.userprojection;

import java.util.Objects;
import org.keycloak.models.RealmModel;
import org.keycloak.models.UserModel;
import org.keycloak.provider.ProviderEvent;
import org.keycloak.provider.ProviderEventListener;

/**
 * Deletions (ADR-0025 § 2): Keycloak publishes {@code UserModel.UserRemovedEvent}
 * inside the removing transaction, with the user's model still readable, by
 * any path — console, admin REST, self-service account deletion. The deletion
 * is queued on commit carrying the last state (BR-UP-5). Only realm
 * {@code mustask}, and only while the listener is not known to be disabled
 * there: this provider event reaches every realm, whatever its event-listener
 * settings (US-5, "a disabled provider freezes the projection").
 */
public final class UserRemovalListener implements ProviderEventListener {

    private final ChangeCapture capture;

    private final ListenerEnablementWatch enablement;

    public UserRemovalListener(ChangeCapture capture, ListenerEnablementWatch enablement) {
        this.capture = Objects.requireNonNull(capture, "capture");
        this.enablement = Objects.requireNonNull(enablement, "enablement");
    }

    @Override
    public void onEvent(ProviderEvent event) {
        if (!(event instanceof UserModel.UserRemovedEvent removed)) {
            return;
        }

        RealmModel realm = removed.getRealm();

        if (realm == null || !ProjectedRealm.NAME.equals(realm.getName()) || !enablement.isEnabledOrUnknown()) {
            return;
        }

        capture.capture(removed.getKeycloakSession(), removed.getUser(), true);
    }
}
