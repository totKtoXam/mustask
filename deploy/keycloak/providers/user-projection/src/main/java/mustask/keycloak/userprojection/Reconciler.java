package mustask.keycloak.userprojection;

import java.io.IOException;
import java.time.Duration;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.UUID;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.ScheduledFuture;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicLong;
import java.util.function.BooleanSupplier;
import java.util.function.Supplier;
import java.util.stream.Collectors;
import org.jboss.logging.Logger;

/**
 * Reconciliation (BR-UP-8; ADR-0025 § 5): 30 s after start, after delivery
 * recovers, every period, and when the listener is enabled again. One run at
 * a time; requests during a run coalesce into one more run. A run reads the
 * realm in-process and sends a snapshot — begin with the enumeration start,
 * numbered pages of up to 500 entries, completion with the counts — that
 * MUSTASK proves whole before it marks anyone absent. A run that fails
 * part-way sends no completion, so it marks nobody; it is retried a minute
 * later. A page MUSTASK refuses as malformed is logged with the ids of its
 * users (ids only), since it will be refused on every run until one of them
 * changes. At most one retry is pending, and any run that starts — the retry,
 * a period, a recovery — cancels it first. Delivery's recovery asks for a run
 * only when no run begun since delivery started failing has completed or is
 * under way ({@link DeliveryRecovery}): whichever comes first at an outage's
 * end, the retry or the recovery, runs, and the other is covered. So an
 * outage of any length leaves one attempt a minute (plus the periods) and its
 * end one reconciliation. Runs skipped while the listener's state was still
 * unknown are made up once it is first seen enabled ({@link #requestIfSkipped}).
 */
public final class Reconciler implements DeliveryRecovery {

    static final Duration START_DELAY = Duration.ofSeconds(30);

    static final Duration RETRY_DELAY = Duration.ofSeconds(60);

    static final int PAGE_SIZE = 500;

    private static final Logger LOG = Logger.getLogger(Reconciler.class);

    private final RealmSource source;

    private final PushClient client;

    private final BooleanSupplier enabled;

    private final ScheduledExecutorService scheduler;

    private final Duration period;

    private final Supplier<UUID> snapshotIds;

    private final AtomicBoolean running = new AtomicBoolean();

    private final AtomicBoolean rerun = new AtomicBoolean();

    /** Runs begun so far; a run's number is this count when it begins. */
    private final AtomicLong runsBegun = new AtomicLong();

    /** The number of the run under way, 0 between runs. */
    private volatile long runUnderWay;

    /** The number of the latest run that completed, 0 before any. */
    private volatile long lastCompleted;

    /** A run was skipped (the listener not known to be enabled, or no realm) and none has completed since. */
    private volatile boolean skippedSinceCompleted;

    private final Object retryLock = new Object();

    /** The one pending retry of a failed run, or {@code null}; guarded by {@link #retryLock}. */
    private ScheduledFuture<?> retry;

    public Reconciler(
            RealmSource source,
            PushClient client,
            BooleanSupplier enabled,
            ScheduledExecutorService scheduler,
            Duration period,
            Supplier<UUID> snapshotIds) {
        this.source = Objects.requireNonNull(source, "source");
        this.client = Objects.requireNonNull(client, "client");
        this.enabled = Objects.requireNonNull(enabled, "enabled");
        this.scheduler = Objects.requireNonNull(scheduler, "scheduler");
        this.period = Objects.requireNonNull(period, "period");
        this.snapshotIds = Objects.requireNonNull(snapshotIds, "snapshotIds");
    }

    /** What one run did. */
    public enum Outcome {
        /** The listener is not known to be enabled, or the realm does not exist: nothing sent. */
        SKIPPED,
        /** The snapshot was completed. */
        COMPLETED,
        /** It stopped part-way: no completion was sent, nobody is marked. */
        FAILED,
    }

    /** Schedules the start run and the periodic runs. */
    public void start() {
        scheduler.schedule(this::request, START_DELAY.toMillis(), TimeUnit.MILLISECONDS);
        scheduler.scheduleAtFixedRate(this::request, period.toMillis(), period.toMillis(), TimeUnit.MILLISECONDS);
    }

    /** Asks for a run; single-flight — a request during a run makes one more run after it. */
    public void request() {
        if (running.compareAndSet(false, true)) {
            scheduler.execute(this::runCoalesced);
        } else {
            rerun.set(true);
        }
    }

    /**
     * Asks for a run if one was skipped since the last completed run — called when the listener is first
     * seen enabled. Checks that fail, or find realm {@code mustask} not imported yet, until the start run is
     * due leave the state unknown then, and that run is skipped; without this, the backfill would wait for
     * the period.
     */
    public void requestIfSkipped() {
        if (skippedSinceCompleted) {
            LOG.infof("mustask-user-projection: the listener is enabled in realm %s; the reconciliation skipped meanwhile follows.",
                    ProjectedRealm.NAME);
            request();
        }
    }

    /** The mark the sender takes when delivery starts failing: the number of runs begun so far. */
    @Override
    public long failureBegins() {
        return runsBegun.get();
    }

    /**
     * Delivery recovered after failing since {@code mark}. A run begun after the mark read the realm after
     * the failure began, so it heals the outage as well as a new one would: if one has completed, or is under
     * way (should it fail, it schedules the retry itself), no further run is asked for.
     */
    @Override
    public void deliveryRecovered(long mark) {
        // In this order: a run records its completion before it clears runUnderWay.
        long underWay = runUnderWay;
        long completed = lastCompleted;

        if (underWay > mark || completed > mark) {
            LOG.infof("mustask-user-projection: a reconciliation begun during the outage covers it; no further one follows.");
            return;
        }

        // The request comes before the log call: a retry that fires between the reads above and this request begins
        // a run, and the request then adds a second one; a log call in between would widen that window to microseconds.
        request();
        LOG.infof("mustask-user-projection: a reconciliation follows the recovery of delivery.");
    }

    private void runCoalesced() {
        try {
            do {
                long run = runsBegun.incrementAndGet();
                // Published before the rerun flag is cleared: a recovery that sees no run under way asks for one,
                // and a request that arrives before this line is covered by this run.
                runUnderWay = run;
                rerun.set(false);
                // Whatever triggered this run — a period, a recovery, a retry — it covers what a pending retry was for.
                cancelRetry();

                Outcome outcome = runOnce();

                if (outcome == Outcome.COMPLETED) {
                    skippedSinceCompleted = false;
                    lastCompleted = run;
                } else if (outcome == Outcome.FAILED) {
                    scheduleRetry();
                } else {
                    skippedSinceCompleted = true;
                }

                runUnderWay = 0;
            } while (rerun.get());
        } catch (RuntimeException ex) {
            LOG.errorf(ex, "mustask-user-projection: a reconciliation failed unexpectedly (%s); retried in %d s.",
                    ex.getClass().getName(), RETRY_DELAY.toSeconds());
            scheduleRetry();
        } finally {
            runUnderWay = 0;
            running.set(false);

            if (rerun.getAndSet(false)) {
                request();
            }
        }
    }

    /** At most one retry is pending: a failure during an outage replaces it, it never adds a chain beside it. */
    private void scheduleRetry() {
        synchronized (retryLock) {
            if (retry == null || retry.isDone()) {
                retry = scheduler.schedule(this::request, RETRY_DELAY.toMillis(), TimeUnit.MILLISECONDS);
            }
        }
    }

    private void cancelRetry() {
        synchronized (retryLock) {
            if (retry != null) {
                retry.cancel(false);
                retry = null;
            }
        }
    }

    /** One whole reconciliation. */
    public Outcome runOnce() {
        if (!enabled.getAsBoolean()) {
            LOG.debugf("mustask-user-projection: the listener is not enabled in realm %s; no reconciliation.", ProjectedRealm.NAME);
            return Outcome.SKIPPED;
        }

        RealmSource.Enumeration enumeration = source.enumerate();

        if (enumeration == null) {
            LOG.warnf("mustask-user-projection: realm %s does not exist; no reconciliation.", ProjectedRealm.NAME);
            return Outcome.SKIPPED;
        }

        String snapshot = "snapshots/" + snapshotIds.get();
        String step = "begin";
        // The page being sent, while it is: the users to name if MUSTASK refuses it as malformed.
        List<UserEntry> sending = null;

        try {
            expectSuccess(client.send("PUT", snapshot, FeedJson.begin(enumeration.enumerationStart())), step);

            int pageNumber = 0;
            int entryCount = 0;
            List<String> ids = enumeration.ids();

            for (int from = 0; from < ids.size(); from += PAGE_SIZE) {
                List<UserEntry> entries = source.read(ids.subList(from, Math.min(from + PAGE_SIZE, ids.size())));

                if (entries.isEmpty()) {
                    continue;
                }

                step = "page " + pageNumber;
                sending = entries;
                expectSuccess(client.send("PUT", snapshot + "/pages/" + pageNumber, FeedJson.page(entries)), step);
                sending = null;
                pageNumber++;
                entryCount += entries.size();
            }

            step = "completion";
            PushClient.Response completed = client.send("PUT", snapshot + "/completion", FeedJson.completion(pageNumber, entryCount));
            expectSuccess(completed, step);

            Map<String, Long> counts = FeedJson.counts(completed.body());
            LOG.infof(
                    "mustask-user-projection: reconciliation %s completed — MUSTASK received %d, added %d, changed %d, marked absent %d, deleted %d (%d page(s), %d entries).",
                    snapshot, counts.getOrDefault("received", -1L), counts.getOrDefault("added", -1L),
                    counts.getOrDefault("changed", -1L), counts.getOrDefault("markedAbsent", -1L),
                    counts.getOrDefault("heldAsDeleted", -1L), pageNumber, entryCount);

            return Outcome.COMPLETED;
        } catch (IOException | RefusedException ex) {
            boolean credential = ex instanceof RefusedException refused && refused.status == 401;
            String reason = ex.getMessage() == null ? ex.getClass().getSimpleName() : ex.getMessage();
            String message = "mustask-user-projection: reconciliation %s stopped at its %s (%s); nobody is marked absent, it is retried in %d s.";

            if (credential) {
                LOG.errorf(message + " Check that the provider's push-secret equals MUSTASK's UserDirectory:Push:Secret and that both clocks are right.",
                        snapshot, step, reason, RETRY_DELAY.toSeconds());
            } else if (sending != null && ex instanceof RefusedException refused && refused.status == 400) {
                // MUSTASK refused the page's content, and will every run until a user on it changes: say whose data is on it.
                LOG.errorf(message + " MUSTASK refused the page as malformed — input it cannot take; the page holds these users, ids only: %s",
                        snapshot, step, reason, RETRY_DELAY.toSeconds(), idsOf(sending));
            } else {
                LOG.warnf(message, snapshot, step, reason, RETRY_DELAY.toSeconds());
            }

            return Outcome.FAILED;
        } catch (InterruptedException ex) {
            Thread.currentThread().interrupt();
            return Outcome.FAILED;
        }
    }

    /** The ids of a whole page — at most {@link #PAGE_SIZE} of them; the one to look for is among them. */
    private static String idsOf(List<UserEntry> page) {
        return page.stream().map(UserEntry::id).collect(Collectors.joining(", "));
    }

    private static void expectSuccess(PushClient.Response response, String step) throws RefusedException {
        if (!response.isSuccess()) {
            throw new RefusedException(response.status(), step, FeedJson.errorCodes(response.body()));
        }
    }

    /** MUSTASK answered a step with an error. */
    private static final class RefusedException extends Exception {

        private static final long serialVersionUID = 1L;

        private final int status;

        private RefusedException(int status, String step, List<String> codes) {
            super("HTTP " + status + " at " + step + (codes.isEmpty() ? "" : " " + codes));
            this.status = status;
        }
    }
}
