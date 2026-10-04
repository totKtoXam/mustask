package mustask.keycloak.userprojection;

import java.util.List;

/** What a reconciliation reads of realm {@code mustask} (ADR-0025 § 5). */
public interface RealmSource {

    /**
     * The marker {@code E} — the enumeration start — taken first, then the ids
     * of every user that is not a service account, in one statement; null
     * when the realm does not exist.
     */
    Enumeration enumerate();

    /**
     * The users with these ids, read in one transaction, each entry's marker
     * taken before its read; users gone since the enumeration and service
     * accounts are skipped.
     */
    List<UserEntry> read(List<String> ids);

    /** An enumeration's result. */
    record Enumeration(long enumerationStart, List<String> ids) {

        public Enumeration {
            ids = List.copyOf(ids);
        }
    }
}
