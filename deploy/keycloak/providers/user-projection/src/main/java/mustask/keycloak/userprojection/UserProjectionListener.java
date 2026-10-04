package mustask.keycloak.userprojection;

import java.util.EnumSet;
import java.util.Objects;
import java.util.Set;
import org.keycloak.events.Event;
import org.keycloak.events.EventListenerProvider;
import org.keycloak.events.EventType;
import org.keycloak.events.admin.AdminEvent;
import org.keycloak.events.admin.OperationType;
import org.keycloak.events.admin.ResourceType;
import org.keycloak.models.KeycloakSession;
import org.keycloak.models.RealmModel;
import org.keycloak.models.UserModel;

/**
 * The event sources of ADR-0025 § 2. Admin events of resource type
 * {@code USER}, operation {@code CREATE} or {@code UPDATE}, path exactly
 * {@code users/<id>}; user events of the five types below. Everything else is
 * ignored by construction — role and group mappings, credential resets and
 * required actions are other resource types, operations or deeper paths;
 * sign-ins, a temporary lockout and their errors are other user event types.
 * Events of any realm but {@code mustask} are ignored (BR-UP-13). Deletions
 * come from {@link UserRemovalListener}, not from here.
 */
public final class UserProjectionListener implements EventListenerProvider {

    /** The user events that change a projected field or the enabled flag. */
    static final Set<EventType> USER_EVENTS = EnumSet.of(
            EventType.REGISTER,
            EventType.UPDATE_PROFILE,
            EventType.UPDATE_EMAIL,
            EventType.IDENTITY_PROVIDER_FIRST_LOGIN,
            EventType.USER_DISABLED_BY_PERMANENT_LOCKOUT);

    private static final String USERS_PATH = "users/";

    private final KeycloakSession session;

    private final ChangeCapture capture;

    public UserProjectionListener(KeycloakSession session, ChangeCapture capture) {
        this.session = Objects.requireNonNull(session, "session");
        this.capture = Objects.requireNonNull(capture, "capture");
    }

    @Override
    public void onEvent(Event event) {
        if (!USER_EVENTS.contains(event.getType())) {
            return;
        }

        captureUser(event.getRealmId(), event.getUserId());
    }

    @Override
    public void onEvent(AdminEvent event, boolean includeRepresentation) {
        if (event.getResourceType() != ResourceType.USER) {
            return;
        }

        OperationType operation = event.getOperationType();

        if (operation != OperationType.CREATE && operation != OperationType.UPDATE) {
            return;
        }

        captureUser(event.getRealmId(), userIdOf(event.getResourcePath()));
    }

    @Override
    public void close() {
        // Nothing per session: the queue and the threads belong to the factory.
    }

    /** The id of a path that is exactly {@code users/<id>}, or null. */
    static String userIdOf(String resourcePath) {
        if (resourcePath == null || !resourcePath.startsWith(USERS_PATH)) {
            return null;
        }

        String id = resourcePath.substring(USERS_PATH.length());

        return id.isEmpty() || id.indexOf('/') >= 0 ? null : id;
    }

    private void captureUser(String realmId, String userId) {
        if (userId == null || !ProjectedRealm.isProjected(session, realmId)) {
            return;
        }

        RealmModel realm = ProjectedRealm.find(session);
        UserModel user = session.users().getUserById(realm, userId);

        capture.capture(session, user, false);
    }
}
