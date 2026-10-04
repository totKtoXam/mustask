package mustask.keycloak.userprojection;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyLong;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.mockingDetails;
import static org.mockito.Mockito.times;
import static org.mockito.Mockito.verify;

import java.io.IOException;
import java.time.Duration;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;
import java.util.function.Supplier;
import java.util.logging.Handler;
import java.util.logging.Level;
import java.util.logging.LogRecord;
import java.util.logging.Logger;
import java.util.stream.IntStream;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;

/** ADR-0025 § 5, reconciliation; mod-014 US-4 (the provider half). */
class ReconcilerTest {

    private static final UUID SNAPSHOT = UUID.fromString("3f2a6b1c-0d4e-4f5a-8b6c-7d8e9f0a1b2c");

    private static final long ENUMERATION_START = 1_790_000_000_000_000L;

    private final StubReceiver receiver = new StubReceiver();

    private final ScheduledExecutorService scheduler = mock(ScheduledExecutorService.class);

    private final FakeRealm realm = new FakeRealm();

    private boolean enabled = true;

    private final Reconciler reconciler;

    ReconcilerTest() throws IOException {
        reconciler = new Reconciler(realm, receiver.client(), () -> enabled, scheduler, Duration.ofMinutes(60), () -> SNAPSHOT);
        receiver.answer(request -> request.target().endsWith("/completion")
                ? StubReceiver.Answer.ok("{\"received\":1200,\"added\":3,\"changed\":1,\"unchanged\":1196,\"heldAsDeleted\":0,\"markedAbsent\":2,\"pageCount\":3,\"entryCount\":1200}")
                : StubReceiver.Answer.ok("{}"));
    }

    @AfterEach
    void stopReceiver() {
        receiver.close();
    }

    /** realm {@code mustask} in memory: a list of ids, read back as entries stamped in order. */
    private static final class FakeRealm implements RealmSource {

        private List<String> ids = IntStream.range(0, 1200).mapToObj(i -> "u-" + i).toList();

        private boolean exists = true;

        private long nextMoment = ENUMERATION_START + 1;

        private final List<Integer> readSizes = new ArrayList<>();

        /** Runs once, inside the next enumeration — a trigger arriving mid-run. */
        private Runnable duringEnumeration;

        @Override
        public Enumeration enumerate() {
            if (duringEnumeration != null) {
                Runnable trigger = duringEnumeration;
                duringEnumeration = null;
                trigger.run();
            }

            return exists ? new Enumeration(ENUMERATION_START, ids) : null;
        }

        @Override
        public List<UserEntry> read(List<String> batch) {
            readSizes.add(batch.size());

            return batch.stream()
                    .map(id -> new UserEntry(id, nextMoment++, "user-" + id, "Имя", "Фамилия", null, true, false))
                    .toList();
        }
    }

    // US-4 "Existing users are loaded into an empty projection": begin with E, numbered pages of up to 500, completion with the counts.
    @Test
    void aRun_sendsBeginNumberedPagesAndACompletionWithTheCounts() {
        assertEquals(Reconciler.Outcome.COMPLETED, reconciler.runOnce());

        List<StubReceiver.Request> requests = receiver.requests();
        String base = "snapshots/" + SNAPSHOT;

        assertEquals(
                List.of(base, base + "/pages/0", base + "/pages/1", base + "/pages/2", base + "/completion"),
                requests.stream().map(StubReceiver.Request::target).toList());
        assertTrue(requests.stream().allMatch(request -> request.method().equals("PUT") && request.signedRight()));
        assertEquals("{\"enumerationStart\":" + ENUMERATION_START + "}", requests.get(0).body());
        assertEquals("{\"pageCount\":3,\"entryCount\":1200}", requests.get(4).body());
        assertEquals(List.of(500, 500, 200), realm.readSizes, "read by id in batches of 500, each its own transaction");
    }

    // NFR Logging: the provider logs MUSTASK's counts from the completion's answer.
    @Test
    void aCompletedRun_logsMustasksCounts() {
        try (LogCapture log = LogCapture.of(Reconciler.class)) {
            reconciler.runOnce();

            assertTrue(log.text().contains("received 1200, added 3, changed 1, marked absent 2, deleted 0"), log.text());
        }
    }

    // A realm with no users completes with 0, 0 and sends no page.
    @Test
    void anEmptyRealm_completesWithZeroCounts() {
        realm.ids = List.of();

        reconciler.runOnce();

        assertEquals(2, receiver.requests().size());
        assertEquals("{\"pageCount\":0,\"entryCount\":0}", receiver.requests().get(1).body());
    }

    // US-4 "An interrupted snapshot marks nobody absent": a cut connection sends no completion; the run is retried.
    @Test
    void aFailedPage_sendsNoCompletion() {
        receiver.answer(request -> request.target().endsWith("/pages/1") ? StubReceiver.Answer.status(503) : StubReceiver.Answer.ok("{}"));

        assertEquals(Reconciler.Outcome.FAILED, reconciler.runOnce());

        assertTrue(receiver.requests().stream().noneMatch(request -> request.target().endsWith("/completion")));
    }

    @Test
    void anUnreachableMustask_failsTheRunWithoutCompletion() {
        receiver.close();

        assertEquals(Reconciler.Outcome.FAILED, reconciler.runOnce());
    }

    // Security pass S1: a page MUSTASK refuses as malformed is refused on every run until the data changes, so the
    // operator is told whose data is on it — ids only (NFR Logging), the whole page: the one to look for is among them.
    @Test
    void aPageRefusedAsMalformed_logsTheUserIdsOfThatPageAtError_neverTheirNames() {
        receiver.answer(request -> request.target().endsWith("/pages/1")
                ? new StubReceiver.Answer(400, "{\"errorCode\":\"user_directory.push_malformed\"}", 0)
                : StubReceiver.Answer.ok("{}"));

        try (LogCapture log = LogCapture.of(Reconciler.class)) {
            assertEquals(Reconciler.Outcome.FAILED, reconciler.runOnce());

            List<String> errors = log.messagesAtLeast(LogCapture.ERROR);
            assertEquals(1, errors.size(), log.text());

            String line = errors.get(0);
            assertTrue(line.contains("page 1") && line.contains("push_malformed"), line);
            assertTrue(IntStream.range(500, 1000).allMatch(i -> line.contains("u-" + i + ",") || line.endsWith("u-" + i)), "every id of page 1: " + line);
            assertTrue(IntStream.of(0, 499, 1000, 1199).noneMatch(i -> line.contains("u-" + i + ",") || line.endsWith("u-" + i)), "no id of another page: " + line);
            assertTrue(!line.contains("Имя") && !line.contains("Фамилия") && !line.contains("user-u-"), "no name or username: " + line);
        }

        assertTrue(receiver.requests().stream().noneMatch(request -> request.target().endsWith("/completion")), "nobody is marked absent");
    }

    // The page's ids are not logged for a failure that says nothing about the data: an outage, or a refusal of the begin.
    @Test
    void aPageFailingForAnotherReason_logsNoUserIds() {
        receiver.answer(request -> request.target().endsWith("/pages/1") ? StubReceiver.Answer.status(503) : StubReceiver.Answer.ok("{}"));

        try (LogCapture log = LogCapture.of(Reconciler.class)) {
            assertEquals(Reconciler.Outcome.FAILED, reconciler.runOnce());

            assertTrue(!log.text().contains("u-5"), log.text());
        }
    }

    @Test
    void aBeginRefusedAsMalformed_logsNoUserIds() {
        receiver.answer(request -> request.target().equals("snapshots/" + SNAPSHOT)
                ? new StubReceiver.Answer(400, "{\"errorCode\":\"user_directory.validation_failed\"}", 0)
                : StubReceiver.Answer.ok("{}"));

        try (LogCapture log = LogCapture.of(Reconciler.class)) {
            assertEquals(Reconciler.Outcome.FAILED, reconciler.runOnce());

            assertTrue(log.text().contains("begin") && !log.text().contains("u-0"), log.text());
        }
    }

    // US-5 "A disabled provider freezes the projection, not the reads": no reconciliation while disabled.
    @Test
    void whileTheListenerIsDisabled_nothingIsSent() {
        enabled = false;

        assertEquals(Reconciler.Outcome.SKIPPED, reconciler.runOnce());
        assertTrue(receiver.requests().isEmpty());
    }

    @Test
    void withoutTheRealm_nothingIsSent() {
        realm.exists = false;

        assertEquals(Reconciler.Outcome.SKIPPED, reconciler.runOnce());
        assertTrue(receiver.requests().isEmpty());
    }

    // US-4 "The reconciliation repeats on its period" (and runs 30 s after the start).
    @Test
    void start_schedulesTheStartRunAndThePeriod() {
        reconciler.start();

        verify(scheduler).schedule(any(Runnable.class), eq(30_000L), eq(TimeUnit.MILLISECONDS));
        verify(scheduler).scheduleAtFixedRate(any(Runnable.class), eq(3_600_000L), eq(3_600_000L), eq(TimeUnit.MILLISECONDS));
    }

    // ADR-0025 § 5: one run at a time — requests before a run begins are covered by it.
    @Test
    void requestsBeforeARunBegins_areCoveredByThatRun() {
        ArgumentCaptor<Runnable> submitted = ArgumentCaptor.forClass(Runnable.class);

        reconciler.request();
        reconciler.request();
        reconciler.request();

        verify(scheduler, times(1)).execute(submitted.capture());
        submitted.getValue().run();

        assertEquals(1, completions());
    }

    // ADR-0025 § 5: triggers during a run coalesce into one more run.
    @Test
    void requestsDuringARun_coalesceIntoOneMoreRun() {
        ArgumentCaptor<Runnable> submitted = ArgumentCaptor.forClass(Runnable.class);
        realm.duringEnumeration = () -> {
            reconciler.request();
            reconciler.request();
        };

        reconciler.request();
        verify(scheduler, times(1)).execute(submitted.capture());
        submitted.getValue().run();

        assertEquals(2, completions(), "the run and exactly one coalesced rerun");
    }

    private long completions() {
        return receiver.requests().stream().filter(request -> request.target().endsWith("/completion")).count();
    }

    @Test
    void aFailedRun_isRetriedAMinuteLater() {
        receiver.close();
        ArgumentCaptor<Runnable> submitted = ArgumentCaptor.forClass(Runnable.class);

        reconciler.request();
        verify(scheduler).execute(submitted.capture());
        submitted.getValue().run();

        verify(scheduler).schedule(any(Runnable.class), eq(60_000L), eq(TimeUnit.MILLISECONDS));
        verify(scheduler, times(0)).schedule(any(Runnable.class), eq(30_000L), any());
        verify(scheduler, times(0)).scheduleAtFixedRate(any(Runnable.class), anyLong(), anyLong(), any());
    }

    /** A reconciler on a virtual clock, with a 2-minute period — QA's stand setting for D-2. */
    private Reconciler onVirtualTime(VirtualScheduler clock) {
        return new Reconciler(realm, receiver.client(), () -> enabled, clock, Duration.ofMinutes(2), () -> SNAPSHOT);
    }

    private long begins() {
        return receiver.requests().stream().filter(request -> request.target().equals("snapshots/" + SNAPSHOT)).count();
    }

    // ADR-0025 § 5, triggers coalesce (QA D-2): an outage keeps one retry pending however long it lasts —
    // a period that fires meanwhile replaces the pending retry instead of starting a chain of its own.
    @Test
    void anOutage_keepsOneRetryPending_howeverManyPeriodsPass() {
        VirtualScheduler clock = new VirtualScheduler();
        Reconciler outage = onVirtualTime(clock);
        receiver.answer(request -> StubReceiver.Answer.status(503));

        outage.start();

        for (int halfMinute = 1; halfMinute <= 40; halfMinute++) {
            clock.advance(Duration.ofSeconds(30));

            assertTrue(clock.pendingOneShots() <= 1,
                    "after " + halfMinute * 30 + " s: " + clock.pendingOneShots() + " retries pending");
        }

        long before = begins();
        clock.advance(Duration.ofMinutes(4));

        assertTrue(begins() - before <= 6,
                "4 more minutes of the outage: at most one retry a minute plus two periods, but " + (begins() - before) + " attempts");
    }

    // US-5 "The projection catches up after MUSTASK comes back", ADR-0025 § 4–5 (QA D-2): once delivery recovers,
    // one reconciliation runs — the run the recovery asks for covers the pending retry.
    @Test
    void afterAnOutage_theRecoveryRunsOneReconciliation() {
        VirtualScheduler clock = new VirtualScheduler();
        Reconciler outage = onVirtualTime(clock);
        receiver.answer(request -> StubReceiver.Answer.status(503));

        outage.start();
        long mark = outage.failureBegins();
        clock.advance(Duration.ofSeconds(610));

        mustaskAnswersAgain();
        outage.deliveryRecovered(mark);
        clock.advance(Duration.ofSeconds(100));

        assertEquals(1, completions(), "the recovery's run, and no retry left over from the outage");
        assertEquals(0, clock.pendingOneShots());
    }

    /** MUSTASK is back: every step succeeds, the completion with counts. */
    private void mustaskAnswersAgain() {
        receiver.answer(request -> request.target().endsWith("/completion")
                ? StubReceiver.Answer.ok("{\"received\":1200,\"added\":0,\"changed\":0,\"unchanged\":1200,\"heldAsDeleted\":0,\"markedAbsent\":0,\"pageCount\":3,\"entryCount\":1200}")
                : StubReceiver.Answer.ok("{}"));
    }

    /**
     * Ten minutes of an outage under the default 60-minute period, delivery having started failing at 100 s:
     * the start run (30 s) and every retry fail, the next retry is due at 630 s. Returns the sender's mark.
     */
    private long tenMinutesOfAnOutage(VirtualScheduler clock, Reconciler outage) {
        receiver.answer(request -> StubReceiver.Answer.status(503));
        outage.start();
        clock.advance(Duration.ofSeconds(100));
        long mark = outage.failureBegins();
        clock.advance(Duration.ofSeconds(510));
        assertEquals(10, begins(), "the start run and nine retries, all failed");

        return mark;
    }

    // Code review F1, QA D-4 (ADR-0025 § 5 "its end one reconciliation"): the other order — the pending retry fires
    // before the sender's next attempt. Its run, begun after delivery started failing, completes; the recovery the
    // sender then reports adds no second run.
    @Test
    void afterAnOutage_whenTheRetryCompletesFirst_theRecoveryAddsNoRun() {
        VirtualScheduler clock = new VirtualScheduler();
        Reconciler outage = new Reconciler(realm, receiver.client(), () -> enabled, clock, Duration.ofMinutes(60), () -> SNAPSHOT);
        long mark = tenMinutesOfAnOutage(clock, outage);

        mustaskAnswersAgain();
        clock.advance(Duration.ofSeconds(30));
        assertEquals(1, completions(), "the retry at 630 s completes");

        outage.deliveryRecovered(mark);
        clock.advance(Duration.ofSeconds(100));

        assertEquals(1, completions(), "the retry's run covers the outage");
        assertEquals(0, clock.pendingOneShots());
    }

    // Code review F1: the recovery arrives while the retry's run is under way — the case that used to coalesce into
    // a second run. A run begun after delivery started failing covers it (and had it failed, its retry is pending).
    @Test
    void aRecoveryDuringARunBegunInTheOutage_addsNoRun() {
        VirtualScheduler clock = new VirtualScheduler();
        Reconciler outage = new Reconciler(realm, receiver.client(), () -> enabled, clock, Duration.ofMinutes(60), () -> SNAPSHOT);
        long mark = tenMinutesOfAnOutage(clock, outage);

        mustaskAnswersAgain();
        realm.duringEnumeration = () -> outage.deliveryRecovered(mark);
        clock.advance(Duration.ofSeconds(130));

        assertEquals(1, completions(), "the retry's run, no coalesced second one");
        assertEquals(0, clock.pendingOneShots());
    }

    // Code review F1, the rule is not vacuous: when the only completed run began before delivery started failing,
    // it cannot have healed the outage, and the recovery runs one.
    @Test
    void aRecoveryAfterOnlyRunsBegunBeforeTheFailure_runsOne() {
        VirtualScheduler clock = new VirtualScheduler();
        Reconciler reconciliations = new Reconciler(realm, receiver.client(), () -> enabled, clock, Duration.ofMinutes(60), () -> SNAPSHOT);

        reconciliations.start();
        clock.advance(Duration.ofSeconds(30));
        assertEquals(1, completions(), "the start run");

        long mark = reconciliations.failureBegins();
        reconciliations.deliveryRecovered(mark);
        clock.advance(Duration.ofSeconds(1));

        assertEquals(2, completions());
    }

    // QA re-check 2, O-1: the recovery reads the run counters, and only then may it ask for its run — a log call in
    // between lets a retry that fires meanwhile begin a run the recovery's request then adds a second one to. The
    // request comes first, so the window is the nanoseconds between the reads and the request.
    @Test
    void aRecovery_asksForItsRunBeforeItLogsThatOneFollows() {
        List<Boolean> requestedWhenLogged = new ArrayList<>();
        Handler handler = new Handler() {
            @Override
            public void publish(LogRecord record) {
                if (record.getMessage().contains("follows the recovery of delivery")) {
                    requestedWhenLogged.add(mockingDetails(scheduler).getInvocations().stream()
                            .anyMatch(invocation -> invocation.getMethod().getName().equals("execute")));
                }
            }

            @Override
            public void flush() {
                // Nothing buffered.
            }

            @Override
            public void close() {
                // Nothing to release.
            }
        };
        Logger logger = Logger.getLogger(Reconciler.class.getName());
        Level before = logger.getLevel();
        logger.setLevel(Level.ALL);
        logger.addHandler(handler);

        try {
            reconciler.deliveryRecovered(reconciler.failureBegins());
        } finally {
            logger.removeHandler(handler);
            logger.setLevel(before);
        }

        assertEquals(List.of(true), requestedWhenLogged, "one line, written after the run was requested");
    }

    /**
     * The enablement watch and a reconciler wired as {@code UserProjectionListenerFactory.postInit} wires them, on a
     * virtual clock: the watch's first check at 2 s, every 2 s while the state is unknown and each minute once it is
     * known, the start run at 30 s, the 60-minute period.
     */
    private Reconciler wiredAsTheFactoryDoes(VirtualScheduler clock, Supplier<ListenerEnablementWatch.Sighting> probe) {
        AtomicReference<Reconciler> reconciliations = new AtomicReference<>();
        ListenerEnablementWatch watch = new ListenerEnablementWatch(
                probe, () -> reconciliations.get().request(), () -> reconciliations.get().requestIfSkipped());
        reconciliations.set(new Reconciler(realm, receiver.client(), watch::isEnabled, clock, Duration.ofMinutes(60), () -> SNAPSHOT));

        watch.scheduleOn(clock);
        reconciliations.get().start();

        return reconciliations.get();
    }

    // Code review F6 (BR-UP-8 "at Keycloak start"; US-4, the backfill within 5 minutes): the enablement checks throw
    // until after the start run is due, so that run is skipped. The first check that succeeds makes it up — within
    // the 2 s of the quick re-checks, not at the 60-minute period.
    @Test
    void aStartRunSkippedWhileTheStateWasUnknown_isMadeUpWhenTheListenerIsFirstSeenEnabled() {
        VirtualScheduler clock = new VirtualScheduler();
        boolean[] readable = {false};
        wiredAsTheFactoryDoes(clock, () -> {
            if (!readable[0]) {
                throw new IllegalStateException("realm mustask is not readable yet");
            }

            return ListenerEnablementWatch.Sighting.ENABLED;
        });

        clock.advance(Duration.ofSeconds(31));
        assertEquals(0, begins(), "the start run is skipped: the listener's state is unknown");

        readable[0] = true;
        clock.advance(Duration.ofSeconds(1));
        assertEquals(1, completions(), "made up at the first successful check, 32 s after the start");

        clock.advance(Duration.ofMinutes(10));
        assertEquals(1, completions(), "once");
    }

    // Code review F6, the usual start: the first check succeeds before the start run, so there is nothing to make up
    // and the start run is the only one.
    @Test
    void aFirstCheckBeforeTheStartRun_addsNoRunToIt() {
        VirtualScheduler clock = new VirtualScheduler();
        wiredAsTheFactoryDoes(clock, () -> ListenerEnablementWatch.Sighting.ENABLED);

        clock.advance(Duration.ofSeconds(25));
        assertEquals(0, begins(), "the listener is seen enabled at 2 s; nothing was skipped");

        clock.advance(Duration.ofMinutes(10));
        assertEquals(1, completions(), "the start run alone");
    }

    // Code review F13: realm mustask arrives (Keycloak's import finishing) after the first quick checks. A missing realm
    // was "unknown", not "not enabled", so its arrival is the first sighting and the start run stays the only run —
    // counted as "not enabled", the arrival would be a re-enablement and add a run of its own beside the start run.
    @Test
    void aRealmImportedAfterTheFirstChecks_leavesTheStartRunTheOnlyRun() {
        VirtualScheduler clock = new VirtualScheduler();
        boolean[] imported = {false};
        wiredAsTheFactoryDoes(clock, () -> imported[0] ? ListenerEnablementWatch.Sighting.ENABLED : ListenerEnablementWatch.Sighting.NO_REALM);

        clock.advance(Duration.ofSeconds(7));
        imported[0] = true;
        clock.advance(Duration.ofSeconds(10));
        assertEquals(0, begins(), "seen enabled at 8 s, as the first sighting: no run of its own before the start run");

        clock.advance(Duration.ofSeconds(14));
        assertEquals(1, completions(), "the start run, at 30 s: the listener was known enabled by then");

        clock.advance(Duration.ofMinutes(10));
        assertEquals(1, completions(), "the start run alone");
    }

    // Code review F13, a slow import: realm mustask arrives only after the start run was due. That run was skipped
    // while the state was unknown, and the first check that sees the realm makes it up — the case code review F6's
    // note had handled through a re-enablement, now through the first sighting.
    @Test
    void aRealmImportedAfterTheStartRunWasDue_isReconciledAtItsFirstSighting() {
        VirtualScheduler clock = new VirtualScheduler();
        boolean[] imported = {false};
        wiredAsTheFactoryDoes(clock, () -> imported[0] ? ListenerEnablementWatch.Sighting.ENABLED : ListenerEnablementWatch.Sighting.NO_REALM);

        clock.advance(Duration.ofSeconds(41));
        assertEquals(0, begins(), "the start run at 30 s is skipped: no realm yet");

        imported[0] = true;
        clock.advance(Duration.ofSeconds(1));
        assertEquals(1, completions(), "made up at 42 s, the first check after the import");

        clock.advance(Duration.ofMinutes(10));
        assertEquals(1, completions(), "once");
    }

    // QA re-check 1 (D-2's fix must not lose the retry; US-5 catch-up, NFR Freshness): with the default
    // 60-minute period and no recovery trigger — nothing changed in Keycloak meanwhile — the retry alone
    // carries the outage: one attempt every minute, the first attempt after MUSTASK answers completes, no
    // retry is left behind, and the period still runs afterwards.
    @Test
    void anOutageWithoutARecoveryTrigger_isRetriedEveryMinute_andCaughtUpByTheRetryAlone() {
        VirtualScheduler clock = new VirtualScheduler();
        Reconciler outage = new Reconciler(realm, receiver.client(), () -> enabled, clock, Duration.ofMinutes(60), () -> SNAPSHOT);
        receiver.answer(request -> StubReceiver.Answer.status(503));

        outage.start();
        clock.advance(Duration.ofSeconds(30));
        assertEquals(1, begins(), "the start run");

        clock.advance(Duration.ofMinutes(10));
        assertEquals(11, begins(), "ten more attempts in ten minutes: none lost, none added");
        assertEquals(1, clock.pendingOneShots());

        receiver.answer(request -> request.target().endsWith("/completion")
                ? StubReceiver.Answer.ok("{\"received\":1200,\"added\":0,\"changed\":0,\"unchanged\":1200,\"heldAsDeleted\":0,\"markedAbsent\":0,\"pageCount\":3,\"entryCount\":1200}")
                : StubReceiver.Answer.ok("{}"));
        clock.advance(Duration.ofSeconds(60));

        assertEquals(1, completions(), "the next retry completes");
        assertEquals(0, clock.pendingOneShots(), "and leaves no retry behind");

        clock.advance(Duration.ofMinutes(50));
        assertEquals(2, completions(), "the period at 60 minutes still runs");
    }
}
