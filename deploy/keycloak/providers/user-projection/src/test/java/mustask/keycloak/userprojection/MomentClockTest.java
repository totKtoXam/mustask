package mustask.keycloak.userprojection;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.time.Clock;
import java.time.Instant;
import java.time.ZoneId;
import java.time.ZoneOffset;
import org.junit.jupiter.api.Test;

/** ADR-0025 § 6: microseconds since the epoch, strictly increasing within one process. */
class MomentClockTest {

    @Test
    void aMarker_isMicrosecondsSinceTheEpoch() {
        MomentClock clock = new MomentClock(Clock.fixed(Instant.parse("2026-10-02T00:00:00.123456789Z"), ZoneOffset.UTC));

        assertEquals(Instant.parse("2026-10-02T00:00:00Z").getEpochSecond() * 1_000_000L + 123_456L, clock.next());
    }

    @Test
    void markersOfOneProcess_areNeverEqual_evenOnAStoppedClock() {
        MomentClock clock = new MomentClock(Clock.fixed(Instant.parse("2026-10-02T00:00:00Z"), ZoneOffset.UTC));

        long first = clock.next();
        long second = clock.next();

        assertEquals(first + 1, second);
    }

    @Test
    void aClockSteppingBack_stillGivesIncreasingMarkers() {
        Instant[] now = {Instant.parse("2026-10-02T00:00:10Z")};
        Clock stepping = new Clock() {
            @Override
            public ZoneId getZone() {
                return ZoneOffset.UTC;
            }

            @Override
            public Clock withZone(ZoneId zone) {
                return this;
            }

            @Override
            public Instant instant() {
                return now[0];
            }
        };
        MomentClock clock = new MomentClock(stepping);

        long before = clock.next();
        now[0] = Instant.parse("2026-10-02T00:00:05Z");
        long after = clock.next();

        assertTrue(after > before);
    }
}
