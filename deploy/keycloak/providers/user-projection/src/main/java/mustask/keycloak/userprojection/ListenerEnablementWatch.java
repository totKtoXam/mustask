package mustask.keycloak.userprojection;

import java.time.Duration;
import java.util.Objects;
import java.util.concurrent.RejectedExecutionException;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;
import java.util.function.Supplier;
import org.jboss.logging.Logger;

/**
 * Whether {@code mustask-user-projection} is enabled in realm {@code mustask}'s
 * event listeners (ADR-0025 § 5), checked on the reconciler's scheduler. While
 * it is not, nothing is pushed and no reconciliation runs (US-5, "a disabled
 * provider freezes the projection"); a transition to enabled requests a
 * reconciliation, which heals whatever happened meanwhile.
 *
 * <p>Until a check reads the realm the state is unknown: changes are captured
 * and held, not sent, and a reconciliation due meanwhile is skipped. So the
 * first check runs {@link #FIRST_CHECK} after start and is repeated every
 * {@link #RECHECK_WHILE_UNKNOWN} while the state is unknown — the realm not
 * imported yet, or its listeners unreadable — for up to
 * {@link #QUICK_CHECKS_FOR}; after that, and once the state is known, each
 * {@link #PERIOD}. A change made as Keycloak becomes ready is held for seconds,
 * not until a minute-long poll (NFR Freshness; code review F13).
 *
 * <p>A missing realm before the state is known is unknown, not "not enabled":
 * its import arriving is the first sighting, not a re-enablement that would add
 * a run beside the start run. Once the state is known, a realm gone is a realm
 * without the listener. The first check that finds the listener enabled lets
 * the reconciler make up a run it skipped meanwhile (a check failing until the
 * start run is due would otherwise leave the start reconciliation to the
 * period).
 */
public final class ListenerEnablementWatch {

    /** The first check, this long after {@link #scheduleOn}. */
    static final Duration FIRST_CHECK = Duration.ofSeconds(2);

    /** The next check while the state is unknown. */
    static final Duration RECHECK_WHILE_UNKNOWN = Duration.ofSeconds(2);

    /** How long after the start an unknown state is re-checked quickly. */
    static final Duration QUICK_CHECKS_FOR = Duration.ofMinutes(5);

    /** The next check once the state is known, or once the quick checks are over. */
    static final Duration PERIOD = Duration.ofMinutes(1);

    private static final Logger LOG = Logger.getLogger(ListenerEnablementWatch.class);

    /** What one probe of realm {@code mustask} found. */
    public enum Sighting {
        /** The realm exists and lists this provider among its event listeners. */
        ENABLED,
        /** The realm exists and does not list it. */
        NOT_ENABLED,
        /** The realm does not exist (yet). */
        NO_REALM;

        /** {@link #ENABLED} or {@link #NOT_ENABLED}. */
        public static Sighting of(boolean enabled) {
            return enabled ? ENABLED : NOT_ENABLED;
        }
    }

    private final Supplier<Sighting> probe;

    private final Runnable onEnabledAgain;

    private final Runnable onFirstSeenEnabled;

    private volatile Boolean enabled;

    // The fields below are touched only by the checks, which run one at a time on the scheduler's thread.

    /** When the check that runs next is due, counted from {@link #scheduleOn}. */
    private Duration dueAt = Duration.ZERO;

    /** Why the state is still unknown, for the log; null once it is known. */
    private String unknownBecause;

    private boolean reportedUnknown;

    private boolean probeFailing;

    /**
     * @param probe reads the realm's listener list
     * @param onEnabledAgain runs on each transition from not enabled to enabled
     * @param onFirstSeenEnabled runs when the first check that reads the realm finds the listener enabled
     */
    public ListenerEnablementWatch(Supplier<Sighting> probe, Runnable onEnabledAgain, Runnable onFirstSeenEnabled) {
        this.probe = Objects.requireNonNull(probe, "probe");
        this.onEnabledAgain = Objects.requireNonNull(onEnabledAgain, "onEnabledAgain");
        this.onFirstSeenEnabled = Objects.requireNonNull(onFirstSeenEnabled, "onFirstSeenEnabled");
    }

    /** Schedules the checks on {@code scheduler}, the first {@link #FIRST_CHECK} from now; they stop with it. */
    public void scheduleOn(ScheduledExecutorService scheduler) {
        Objects.requireNonNull(scheduler, "scheduler");
        scheduleNext(scheduler, FIRST_CHECK);
    }

    private void scheduleNext(ScheduledExecutorService scheduler, Duration delay) {
        dueAt = dueAt.plus(delay);

        try {
            scheduler.schedule(() -> checkAndScheduleNext(scheduler), delay.toMillis(), TimeUnit.MILLISECONDS);
        } catch (RejectedExecutionException ex) {
            // The scheduler is shut down: the provider is closing, so the checks end here.
        }
    }

    private void checkAndScheduleNext(ScheduledExecutorService scheduler) {
        try {
            check();
        } finally {
            scheduleNext(scheduler, nextDelay());
        }
    }

    /** How long until the next check, given what this one found. */
    private Duration nextDelay() {
        if (enabled != null) {
            return PERIOD;
        }

        if (dueAt.plus(RECHECK_WHILE_UNKNOWN).compareTo(QUICK_CHECKS_FOR) <= 0) {
            return RECHECK_WHILE_UNKNOWN;
        }

        if (dueAt.compareTo(QUICK_CHECKS_FOR) <= 0) {
            LOG.warnf("mustask-user-projection: the listener's state in realm %s is still unknown %d min after start (%s); "
                            + "changes stay held, and it is checked every %d s from now on.",
                    ProjectedRealm.NAME, QUICK_CHECKS_FOR.toMinutes(), unknownBecause, PERIOD.toSeconds());
        }

        return PERIOD;
    }

    /** One check. A failing probe leaves the state as it was; so does a missing realm while the state is unknown. */
    public void check() {
        Sighting sighting;

        try {
            sighting = Objects.requireNonNull(probe.get(), "the probe answered no sighting");
        } catch (RuntimeException ex) {
            probeFailed(ex.getClass().getSimpleName());
            return;
        }

        probeFailing = false;
        Boolean previous = enabled;

        if (previous == null && sighting == Sighting.NO_REALM) {
            stillUnknown("realm " + ProjectedRealm.NAME + " does not exist yet");
            return;
        }

        boolean now = sighting == Sighting.ENABLED;
        enabled = now;

        if (previous == null) {
            unknownBecause = null;
            LOG.infof("mustask-user-projection: the listener is %s in realm %s.", now ? "enabled" : "not enabled", ProjectedRealm.NAME);

            if (now) {
                onFirstSeenEnabled.run();
            }

            return;
        }

        if (!previous && now) {
            LOG.infof("mustask-user-projection: the listener was enabled again in realm %s; a reconciliation follows.", ProjectedRealm.NAME);
            onEnabledAgain.run();
        } else if (previous && sighting == Sighting.NO_REALM) {
            LOG.warnf("mustask-user-projection: realm %s no longer exists; the projection is frozen until it exists with the listener enabled.",
                    ProjectedRealm.NAME);
        } else if (previous && !now) {
            LOG.warnf("mustask-user-projection: the listener was disabled in realm %s; the projection is frozen until it is enabled again.", ProjectedRealm.NAME);
        }
    }

    private void probeFailed(String reason) {
        if (enabled == null) {
            stillUnknown("could not read realm " + ProjectedRealm.NAME + "'s event listeners: " + reason);
            return;
        }

        if (!probeFailing) {
            LOG.warnf("mustask-user-projection: could not read realm %s's event listeners (%s); keeping the last known state.",
                    ProjectedRealm.NAME, reason);
        } else {
            LOG.debugf("mustask-user-projection: still could not read realm %s's event listeners (%s).", ProjectedRealm.NAME, reason);
        }

        probeFailing = true;
    }

    /** The state stays unknown: reported once at INFO — a quick re-check is routine while Keycloak starts. */
    private void stillUnknown(String because) {
        unknownBecause = because;

        if (!reportedUnknown) {
            reportedUnknown = true;
            LOG.infof("mustask-user-projection: %s; changes are held until the listener is known to be enabled, checked again every %d s.",
                    because, RECHECK_WHILE_UNKNOWN.toSeconds());
        } else {
            LOG.debugf("mustask-user-projection: %s; the listener's state is still unknown.", because);
        }
    }

    /** Known to be enabled: the sender and the reconciler run. */
    public boolean isEnabled() {
        return Boolean.TRUE.equals(enabled);
    }

    /** Enabled, or not known yet: changes are still captured (and held until known). */
    public boolean isEnabledOrUnknown() {
        Boolean state = enabled;

        return state == null || state;
    }
}
