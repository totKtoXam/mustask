package mustask.keycloak.userprojection;

import static org.junit.jupiter.api.Assertions.assertDoesNotThrow;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import java.time.Duration;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.stream.Stream;
import mustask.keycloak.userprojection.ListenerEnablementWatch.Sighting;
import org.junit.jupiter.api.Test;
import org.keycloak.models.RealmModel;

/** mod-014 US-5 "A disabled provider freezes the projection, not the reads" (ADR-0025 § 5). */
class ListenerEnablementWatchTest {

    /** What the probe finds next; null makes it throw, as an unreadable realm does. */
    private Sighting sighting = Sighting.ENABLED;

    private final AtomicInteger probes = new AtomicInteger();

    private final AtomicInteger reconciliations = new AtomicInteger();

    private final AtomicInteger firstSeenEnabled = new AtomicInteger();

    private final ListenerEnablementWatch watch = new ListenerEnablementWatch(() -> {
        probes.incrementAndGet();

        if (sighting == null) {
            throw new IllegalStateException("realm unreadable");
        }

        return sighting;
    }, reconciliations::incrementAndGet, firstSeenEnabled::incrementAndGet);

    @Test
    void beforeTheFirstCheck_theStateIsUnknown_capturedButNotSent() {
        assertFalse(watch.isEnabled());
        assertTrue(watch.isEnabledOrUnknown());
    }

    @Test
    void theFirstCheck_setsTheState_andHandsTheReconcilerOnlyItsFirstSighting() {
        watch.check();

        assertTrue(watch.isEnabled());
        assertEquals(0, reconciliations.get(), "not a re-enablement: the start reconciliation covers the first state");
        assertEquals(1, firstSeenEnabled.get(), "the reconciler makes up a run only if it skipped one (ReconcilerTest)");
    }

    // Code review F6: a first check that throws leaves the state unknown; the first check that succeeds
    // afterwards is the first sighting — once — and a later disable/enable is a re-enablement, not another.
    @Test
    void aFailedFirstCheck_thenASuccess_isTheFirstSighting_once() {
        sighting = null;
        watch.check();

        assertFalse(watch.isEnabled());
        assertEquals(0, firstSeenEnabled.get());

        sighting = Sighting.ENABLED;
        watch.check();
        watch.check();
        sighting = Sighting.NOT_ENABLED;
        watch.check();
        sighting = Sighting.ENABLED;
        watch.check();

        assertEquals(1, firstSeenEnabled.get());
        assertEquals(1, reconciliations.get());
    }

    @Test
    void aFirstCheckFindingItDisabled_isNoSighting() {
        sighting = Sighting.NOT_ENABLED;
        watch.check();

        assertEquals(0, firstSeenEnabled.get());
        assertEquals(0, reconciliations.get());
    }

    @Test
    void disabledThenEnabledAgain_freezesThenRequestsOneReconciliation() {
        watch.check();
        sighting = Sighting.NOT_ENABLED;
        watch.check();

        assertFalse(watch.isEnabled());
        assertFalse(watch.isEnabledOrUnknown());

        watch.check();
        sighting = Sighting.ENABLED;
        watch.check();
        watch.check();

        assertTrue(watch.isEnabled());
        assertEquals(1, reconciliations.get());
    }

    @Test
    void anUnreadableRealm_keepsTheLastKnownState() {
        watch.check();
        sighting = null;

        watch.check();

        assertTrue(watch.isEnabled());
    }

    // Code review F13: while Keycloak is still importing realm mustask, a missing realm is no answer yet — the state
    // stays unknown, so the realm's arrival with the listener is the first sighting, not a re-enablement that would
    // request a reconciliation beside the start run (ReconcilerTest has the run count).
    @Test
    void aRealmMissingBeforeTheStateIsKnown_leavesItUnknown_andItsArrivalIsTheFirstSighting() {
        sighting = Sighting.NO_REALM;
        watch.check();
        watch.check();

        assertFalse(watch.isEnabled(), "nothing is sent yet");
        assertTrue(watch.isEnabledOrUnknown(), "changes are still captured");

        sighting = Sighting.ENABLED;
        watch.check();

        assertTrue(watch.isEnabled());
        assertEquals(1, firstSeenEnabled.get());
        assertEquals(0, reconciliations.get());
    }

    // Once the state is known, a realm gone is a realm without the listener: frozen, and its return is a re-enablement.
    @Test
    void aRealmGoneOnceTheStateIsKnown_freezes_andItsReturnRequestsAReconciliation() {
        watch.check();
        sighting = Sighting.NO_REALM;
        watch.check();

        assertFalse(watch.isEnabled());
        assertFalse(watch.isEnabledOrUnknown());

        sighting = Sighting.ENABLED;
        watch.check();

        assertTrue(watch.isEnabled());
        assertEquals(1, reconciliations.get());
        assertEquals(1, firstSeenEnabled.get(), "the first sighting was the very first check");
    }

    // Code review F13 (NFR Freshness, 10 s): the provider holds every change until a check finds the listener
    // enabled, so the first check runs within seconds of the start — not after a minute-long poll's first delay.
    @Test
    void theFirstCheck_runsTwoSecondsAfterTheStart_thenEachMinute() {
        VirtualScheduler clock = new VirtualScheduler();
        watch.scheduleOn(clock);

        clock.advance(Duration.ofMillis(1_999));
        assertEquals(0, probes.get());

        clock.advance(Duration.ofMillis(1));
        assertEquals(1, probes.get());
        assertTrue(watch.isEnabled(), "known enabled 2 s after the start: pushes flow from here on");

        clock.advance(Duration.ofSeconds(59));
        assertEquals(1, probes.get(), "known: the next check is a minute after this one");

        clock.advance(Duration.ofSeconds(1));
        assertEquals(2, probes.get());
    }

    // Code review F13: while the realm is not there yet (Keycloak still importing it) the check repeats every 2 s, so
    // the listener is seen enabled within 2 s of the realm's arrival; then each minute.
    @Test
    void whileTheStateIsUnknown_theCheckRepeatsEveryTwoSeconds() {
        VirtualScheduler clock = new VirtualScheduler();
        sighting = Sighting.NO_REALM;
        watch.scheduleOn(clock);

        clock.advance(Duration.ofSeconds(10));
        assertEquals(5, probes.get(), "at 2, 4, 6, 8 and 10 s");
        assertFalse(watch.isEnabled());

        sighting = Sighting.ENABLED;
        clock.advance(Duration.ofSeconds(2));
        assertEquals(6, probes.get());
        assertTrue(watch.isEnabled(), "seen at the first check after the realm arrived");
        assertEquals(1, firstSeenEnabled.get());
        assertEquals(0, reconciliations.get());

        clock.advance(Duration.ofSeconds(59));
        assertEquals(6, probes.get());
        clock.advance(Duration.ofSeconds(1));
        assertEquals(7, probes.get(), "known: each minute");
    }

    // The quick re-checks are bounded: a realm that never appears (or listeners never readable) is checked every 2 s for
    // the first 5 minutes, then each minute, with one INFO line and one WARN — not a line every 2 s.
    @Test
    void aStateStillUnknownAfterFiveMinutes_isCheckedEachMinute_andReportedOnce() {
        VirtualScheduler clock = new VirtualScheduler();
        sighting = null;

        try (LogCapture log = LogCapture.of(ListenerEnablementWatch.class)) {
            watch.scheduleOn(clock);

            clock.advance(Duration.ofMinutes(5));
            assertEquals(150, probes.get(), "every 2 s from 2 s to 300 s");

            clock.advance(Duration.ofMinutes(5));
            assertEquals(155, probes.get(), "then at 360, 420, 480, 540 and 600 s");

            assertEquals(1, log.messagesAtLeast(LogCapture.WARN).size(), log.text());
            assertEquals(2, log.messagesAtLeast(LogCapture.INFO).size(), "one INFO when it began, the WARN when it slowed: " + log.text());
        }

        sighting = Sighting.ENABLED;
        clock.advance(Duration.ofMinutes(1));
        assertTrue(watch.isEnabled());
        assertEquals(1, firstSeenEnabled.get());
    }

    // The provider's close() shuts the scheduler down: the check under way then ends the chain without an error.
    @Test
    void aShutDownScheduler_endsTheChecksQuietly() {
        VirtualScheduler clock = new VirtualScheduler();
        watch.scheduleOn(clock);
        clock.shutdown();

        assertDoesNotThrow(() -> clock.advance(Duration.ofMinutes(5)));
        assertEquals(1, probes.get(), "the check already queued runs; it schedules no other");
    }

    @Test
    void sightingIn_readsTheRealmsEventListeners() {
        RealmModel with = mock(RealmModel.class);
        when(with.getEventsListenersStream()).thenAnswer(invocation -> Stream.of("jboss-logging", UserProjectionListenerFactory.ID));
        RealmModel without = mock(RealmModel.class);
        when(without.getEventsListenersStream()).thenAnswer(invocation -> Stream.of("jboss-logging"));

        assertEquals(Sighting.ENABLED, UserProjectionListenerFactory.sightingIn(with));
        assertEquals(Sighting.NOT_ENABLED, UserProjectionListenerFactory.sightingIn(without));
        assertEquals(Sighting.NO_REALM, UserProjectionListenerFactory.sightingIn(null));
    }
}
