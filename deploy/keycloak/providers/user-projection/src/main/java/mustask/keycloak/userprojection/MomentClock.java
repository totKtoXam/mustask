package mustask.keycloak.userprojection;

import java.time.Clock;
import java.time.Instant;
import java.util.Objects;

/**
 * The ordering marker (ADR-0025 § 6): microseconds since the Unix epoch on
 * this Keycloak node's clock, strictly increasing within one provider process
 * — {@code max(now, last + 1)} — so two markers of one process are never
 * equal. Only markers are compared with markers.
 */
public final class MomentClock {

    private static final long MICROS_PER_SECOND = 1_000_000L;

    private static final long NANOS_PER_MICRO = 1_000L;

    private final Clock clock;

    private long last;

    public MomentClock(Clock clock) {
        this.clock = Objects.requireNonNull(clock, "clock");
    }

    /** The next marker. */
    public synchronized long next() {
        Instant now = clock.instant();
        long micros = Math.addExact(
                Math.multiplyExact(now.getEpochSecond(), MICROS_PER_SECOND),
                now.getNano() / NANOS_PER_MICRO);

        last = Math.max(micros, last + 1);

        return last;
    }
}
