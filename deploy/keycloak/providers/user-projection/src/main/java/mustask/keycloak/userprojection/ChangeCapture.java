package mustask.keycloak.userprojection;

import java.util.Objects;
import org.keycloak.models.AbstractKeycloakTransaction;
import org.keycloak.models.KeycloakSession;
import org.keycloak.models.UserModel;

/**
 * Captures a user's state inside the transaction that changed it and queues
 * it only after that transaction commits (ADR-0025 § 4): the state is read
 * now, in the event's session — no I/O — and an after-completion task takes
 * the marker and queues the entry on commit; on rollback nothing is queued.
 * The marker is taken after the commit, so a newer marker means a newer
 * committed state (ADR-0025 § 6).
 */
public final class ChangeCapture {

    private final MomentClock clock;

    private final PendingChanges pending;

    public ChangeCapture(MomentClock clock, PendingChanges pending) {
        this.clock = Objects.requireNonNull(clock, "clock");
        this.pending = Objects.requireNonNull(pending, "pending");
    }

    /** Captures {@code user}'s state — or its deletion, carrying the last state — unless it is a service account. */
    public void capture(KeycloakSession session, UserModel user, boolean deleted) {
        if (user == null || ProjectedUserReader.isServiceAccount(user)) {
            return;
        }

        UserState state = ProjectedUserReader.read(user);

        session.getTransactionManager().enlistAfterCompletion(new QueueOnCommit(state, deleted));
    }

    private final class QueueOnCommit extends AbstractKeycloakTransaction {

        private final UserState state;

        private final boolean deleted;

        private QueueOnCommit(UserState state, boolean deleted) {
            this.state = state;
            this.deleted = deleted;
        }

        @Override
        protected void commitImpl() {
            pending.put(state.at(clock.next(), deleted));
        }

        @Override
        protected void rollbackImpl() {
            // The change never happened: nothing to push.
        }
    }
}
