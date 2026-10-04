package mustask.keycloak.userprojection;

import java.io.IOException;
import java.time.Duration;
import java.util.List;
import java.util.Objects;
import java.util.stream.Collectors;
import org.jboss.logging.Logger;

/**
 * Delivers the pending queue (ADR-0025 § 4), on the provider's own thread:
 * batches of up to 500, waiting at most 200 ms to fill one. On a 2xx the batch
 * is done; on a refusal (401), a 5xx, a timeout or a connection failure the
 * batch goes back into the queue (newer states win the merge) and the sender
 * backs off 1 s, doubling to 60 s; a 400 means MUSTASK cannot take what was
 * sent — a contract bug, or input Keycloak accepted and MUSTASK cannot parse or refuses
 * (an unpaired surrogate or a noncharacter in a name or the e-mail), which
 * {@link ProjectedUserReader} now prevents by making every projected string
 * well-formed — so the batch is
 * dropped and logged with MUSTASK's error codes. The first success after a
 * failure tells the reconciler, with the mark taken when the failure began,
 * and a reconciliation follows unless one begun since then covers the outage
 * ({@link DeliveryRecovery}). Logs name users by id only — never a name, an
 * e-mail or the secret.
 */
public final class FeedSender implements Runnable {

    static final int BATCH_SIZE = 500;

    static final Duration FILL_WINDOW = Duration.ofMillis(200);

    static final Duration IDLE_WAIT = Duration.ofSeconds(1);

    static final Duration FIRST_BACKOFF = Duration.ofSeconds(1);

    static final Duration MAX_BACKOFF = Duration.ofSeconds(60);

    static final int IDS_LOGGED = 20;

    private static final Logger LOG = Logger.getLogger(FeedSender.class);

    private final PendingChanges pending;

    private final PushClient client;

    private final ListenerEnablementWatch enablement;

    private final DeliveryRecovery recovery;

    private final Sleeper sleeper;

    private volatile boolean running = true;

    private boolean failing;

    /** The reconciler's mark from when the current failure began; meaningful while {@link #failing}. */
    private long failureMark;

    private Duration backoff = FIRST_BACKOFF;

    public FeedSender(
            PendingChanges pending,
            PushClient client,
            ListenerEnablementWatch enablement,
            DeliveryRecovery recovery,
            Sleeper sleeper) {
        this.pending = Objects.requireNonNull(pending, "pending");
        this.client = Objects.requireNonNull(client, "client");
        this.enablement = Objects.requireNonNull(enablement, "enablement");
        this.recovery = Objects.requireNonNull(recovery, "recovery");
        this.sleeper = Objects.requireNonNull(sleeper, "sleeper");
    }

    /** What one {@link #drainOnce()} did. */
    public enum Outcome {
        /** Nothing queued. */
        IDLE,
        /** The listener is not known to be enabled: nothing sent (US-5). */
        HELD,
        /** A batch was accepted. */
        SENT,
        /** A batch was refused as malformed and dropped. */
        DROPPED,
        /** A batch failed and went back into the queue. */
        FAILED,
    }

    @Override
    public void run() {
        while (running) {
            try {
                Outcome outcome = drainOnce();

                if (outcome == Outcome.HELD) {
                    sleeper.sleep(IDLE_WAIT);
                } else if (outcome == Outcome.FAILED) {
                    sleeper.sleep(backoff);
                    backoff = backoff.multipliedBy(2).compareTo(MAX_BACKOFF) > 0 ? MAX_BACKOFF : backoff.multipliedBy(2);
                }
            } catch (InterruptedException ex) {
                Thread.currentThread().interrupt();
                return;
            } catch (RuntimeException ex) {
                LOG.errorf(ex, "mustask-user-projection: the sender failed unexpectedly (%s); it continues.", ex.getClass().getName());
            }
        }
    }

    public void stop() {
        running = false;
    }

    /** One attempt: wait for entries, fill a batch, send it. */
    public Outcome drainOnce() throws InterruptedException {
        if (!enablement.isEnabled()) {
            return Outcome.HELD;
        }

        if (pending.awaitSize(1, IDLE_WAIT) == 0) {
            return Outcome.IDLE;
        }

        pending.awaitSize(BATCH_SIZE, FILL_WINDOW);
        List<UserEntry> batch = pending.take(BATCH_SIZE);

        if (batch.isEmpty()) {
            return Outcome.IDLE;
        }

        PushClient.Response response;

        try {
            response = client.send("POST", "changes", FeedJson.changes(batch));
        } catch (IOException ex) {
            fail(batch, ex.getClass().getSimpleName(), false);
            return Outcome.FAILED;
        }

        if (response.isSuccess()) {
            if (failing) {
                failing = false;
                backoff = FIRST_BACKOFF;
                LOG.infof("mustask-user-projection: delivery to MUSTASK recovered.");
                recovery.deliveryRecovered(failureMark);
            }

            return Outcome.SENT;
        }

        if (response.status() == 400) {
            LOG.errorf(
                    "mustask-user-projection: MUSTASK refused %d change(s) as malformed (%s) — a contract bug or input it cannot parse or refuses; dropped. User ids: %s",
                    batch.size(), FeedJson.errorCodes(response.body()), idsOf(batch));
            return Outcome.DROPPED;
        }

        fail(batch, "HTTP " + response.status(), response.status() == 401);
        return Outcome.FAILED;
    }

    /** The backoff the next failure waits, for tests. */
    Duration backoff() {
        return backoff;
    }

    private void fail(List<UserEntry> batch, String reason, boolean refused) {
        if (!failing) {
            // Only the first failure of an outage marks it: a run begun after this covers the whole outage.
            failureMark = recovery.failureBegins();
        }

        failing = true;
        pending.requeue(batch);

        if (refused) {
            LOG.errorf(
                    "mustask-user-projection: MUSTASK refused the push credential (%s) for %d change(s); retrying. "
                            + "Check that the provider's push-secret equals MUSTASK's UserDirectory:Push:Secret and that both clocks are right. User ids: %s",
                    reason, batch.size(), idsOf(batch));
        } else {
            LOG.warnf(
                    "mustask-user-projection: could not deliver %d change(s) to MUSTASK (%s); retrying in %d s. User ids: %s",
                    batch.size(), reason, backoff.toSeconds(), idsOf(batch));
        }
    }

    private static String idsOf(List<UserEntry> batch) {
        String ids = batch.stream().limit(IDS_LOGGED).map(UserEntry::id).collect(Collectors.joining(", "));

        return batch.size() > IDS_LOGGED ? ids + ", …" : ids;
    }
}
