package mustask.keycloak.userprojection;

import java.util.Objects;

/**
 * The projected fields of one user as read from Keycloak's model (BR-UP-2):
 * id, username, first and last name, e-mail and enabled — nothing else. A
 * state has no ordering marker yet; {@link #at(long, boolean)} gives it one
 * when it is queued (ADR-0025 § 6).
 */
public record UserState(String id, String username, String firstName, String lastName, String email, boolean enabled) {

    public UserState {
        Objects.requireNonNull(id, "id");
        Objects.requireNonNull(username, "username");
    }

    /** This state as an entry of the push channel, with its marker; a deletion carries the last state. */
    public UserEntry at(long moment, boolean deleted) {
        return new UserEntry(id, moment, username, firstName, lastName, email, enabled && !deleted, deleted);
    }
}
