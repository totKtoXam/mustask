package mustask.keycloak.userprojection;

import java.util.Objects;

/**
 * One user entry of the push channel (mod-014 api-contract.md, Part B, "The
 * user entry"): a user's whole projected state as of {@code moment}, or that
 * user's deletion carrying the last state.
 */
public record UserEntry(
        String id,
        long moment,
        String username,
        String firstName,
        String lastName,
        String email,
        boolean enabled,
        boolean deleted) {

    public UserEntry {
        Objects.requireNonNull(id, "id");
        Objects.requireNonNull(username, "username");
    }

    /**
     * Whether this entry should replace {@code held} for the same user in the
     * pending queue (ADR-0025 § 4): a deletion supersedes any state, a state
     * never supersedes a deletion, and otherwise the newer marker wins.
     */
    public boolean supersedes(UserEntry held) {
        if (held == null) {
            return true;
        }

        if (deleted != held.deleted) {
            return deleted;
        }

        return moment > held.moment;
    }
}
