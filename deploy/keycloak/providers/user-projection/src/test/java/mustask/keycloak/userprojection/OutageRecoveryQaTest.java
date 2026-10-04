package mustask.keycloak.userprojection;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import java.io.IOException;
import java.time.Duration;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ScheduledThreadPoolExecutor;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicLong;
import java.util.concurrent.atomic.AtomicReference;
import java.util.logging.Level;
import java.util.logging.Logger;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.Test;
import org.keycloak.Config;
import org.keycloak.models.KeycloakSessionFactory;

/**
 * QA re-check 2 (qa.md, "Re-check 2", RC2-5..RC2-15): what the code review's fixes changed in how the sender, the
 * reconciler and the enablement watch work <em>together</em>. The developer's tests drive each class against a stand-in
 * of the other (a recovery recorded by a fake, a reconciler without a sender). Here the real {@link FeedSender} hands
 * its real mark to the real {@link Reconciler} on a virtual clock; the real watch drives the real reconciler; and one
 * test puts real threads on the reconciler's triggers.
 */
class OutageRecoveryQaTest {

    private static final UUID SNAPSHOT = UUID.fromString("3f2a6b1c-0d4e-4f5a-8b6c-7d8e9f0a1b2c");

    private static final String COUNTS =
            "{\"received\":3,\"added\":0,\"changed\":0,\"unchanged\":3,\"heldAsDeleted\":0,\"markedAbsent\":0,\"pageCount\":1,\"entryCount\":3}";

    private final StubReceiver receiver = new StubReceiver();

    /** While true the changes route answers 503 and the snapshot routes keep answering: MUSTASK partly down. */
    private volatile boolean changesRouteDown;

    private final PendingChanges pending = new PendingChanges(PendingChanges.DEFAULT_CAPACITY, () -> { });

    /** What the watch's probe finds, for the tests that wire the watch to the reconciler. */
    private volatile ListenerEnablementWatch.Sighting sighting = ListenerEnablementWatch.Sighting.ENABLED;

    OutageRecoveryQaTest() throws IOException {
        receiver.answer(request -> changesRouteDown && request.target().equals("changes")
                ? StubReceiver.Answer.status(503)
                : request.target().endsWith("/completion") ? StubReceiver.Answer.ok(COUNTS) : StubReceiver.Answer.ok("{}"));
    }

    @AfterEach
    void stopReceiver() {
        receiver.close();
    }

    /** Three users in realm {@code mustask}. */
    private static final class ThreeUsers implements RealmSource {

        /** Runs once, inside the next enumeration — something happening while a run is under way. */
        private Runnable duringEnumeration;

        @Override
        public Enumeration enumerate() {
            if (duringEnumeration != null) {
                Runnable event = duringEnumeration;
                duringEnumeration = null;
                event.run();
            }

            return new Enumeration(1_790_000_000_000_000L, List.of("u-1", "u-2", "u-3"));
        }

        @Override
        public List<UserEntry> read(List<String> ids) {
            return ids.stream().map(id -> new UserEntry(id, 1_790_000_000_000_001L, "user-" + id, "Имя", "Фамилия", null, true, false)).toList();
        }
    }

    private static long count(StubReceiver from, String suffix) {
        return from.requests().stream().filter(request -> request.target().endsWith(suffix)).count();
    }

    private long snapshotBegins() {
        return receiver.requests().stream().filter(request -> request.target().equals("snapshots/" + SNAPSHOT)).count();
    }

    private long completions() {
        return count(receiver, "/completion");
    }

    private static UserEntry entry(int i) {
        return new UserEntry("id-" + i, 1_790_000_000_000_000L + i, "user" + i, "Имя", "Фамилия", null, true, false);
    }

    private static ListenerEnablementWatch enabledWatch() {
        ListenerEnablementWatch watch = new ListenerEnablementWatch(() -> ListenerEnablementWatch.Sighting.ENABLED, () -> { }, () -> { });
        watch.check();

        return watch;
    }

    private Reconciler reconcilerOn(VirtualScheduler clock, StubReceiver to, Duration period) {
        return new Reconciler(new ThreeUsers(), to.client(), () -> true, clock, period, () -> SNAPSHOT);
    }

    // RC2-8: only the change route fails (MUSTASK partly down). A period run begun after the failure completes meanwhile,
    // so the recovery asks for nothing more — and the change the sender kept is delivered, not lost with the run.
    @Test
    void aChangeRouteOutage_healedByAPeriodRun_costsNoRunAtRecovery_andLosesNoChange() throws Exception {
        VirtualScheduler clock = new VirtualScheduler();
        Reconciler reconciler = reconcilerOn(clock, receiver, Duration.ofMinutes(2));
        FeedSender sender = new FeedSender(pending, receiver.client(), enabledWatch(), reconciler, duration -> { });
        reconciler.start();

        clock.advance(Duration.ofSeconds(30));
        assertEquals(1, completions(), "the start run");

        changesRouteDown = true;
        pending.put(entry(1));
        assertEquals(FeedSender.Outcome.FAILED, sender.drainOnce());
        assertEquals(1, pending.size(), "the change is kept");

        clock.advance(Duration.ofSeconds(100));
        assertEquals(2, completions(), "the period run at 120 s, begun after the failure, completed");

        changesRouteDown = false;
        assertEquals(FeedSender.Outcome.SENT, sender.drainOnce());
        clock.advance(Duration.ofSeconds(60));

        assertEquals(0, pending.size(), "the kept change was delivered");
        assertEquals(2, completions(), "the period run covered the outage: the recovery added none");
        assertEquals(0, clock.pendingOneShots());
    }

    // RC2-8 control, and the other half of the F1 rule through the real hand-off: no run has begun since the failure,
    // so the recovery runs exactly one.
    @Test
    void aChangeRouteOutage_withNoRunSinceTheFailure_endsInExactlyOneRun() throws Exception {
        VirtualScheduler clock = new VirtualScheduler();
        Reconciler reconciler = reconcilerOn(clock, receiver, Duration.ofMinutes(60));
        FeedSender sender = new FeedSender(pending, receiver.client(), enabledWatch(), reconciler, duration -> { });
        reconciler.start();

        clock.advance(Duration.ofSeconds(30));
        assertEquals(1, completions(), "the start run, begun before the failure");

        changesRouteDown = true;
        pending.put(entry(1));
        assertEquals(FeedSender.Outcome.FAILED, sender.drainOnce());
        clock.advance(Duration.ofSeconds(60));

        changesRouteDown = false;
        assertEquals(FeedSender.Outcome.SENT, sender.drainOnce());
        clock.advance(Duration.ofSeconds(10));

        assertEquals(2, completions(), "the start run does not cover an outage that began after it; the recovery's run does");

        clock.advance(Duration.ofMinutes(10));
        assertEquals(2, completions(), "and nothing more follows");
    }

    // RC2-4: a run that was already under way when delivery started failing began before the failure — it enumerated
    // before it — so it does not cover the outage even though it is still running when the recovery arrives (and even if it
    // completes): the recovery asks for one more run, which follows it. "Begun since" is a strict comparison.
    @Test
    void aRecoveryDuringARunThatBeganBeforeTheFailure_runsOneMore() {
        VirtualScheduler clock = new VirtualScheduler();
        ThreeUsers realm = new ThreeUsers();
        Reconciler reconciler = new Reconciler(realm, receiver.client(), () -> true, clock, Duration.ofMinutes(60), () -> SNAPSHOT);
        // Inside run 1: delivery fails (the mark is taken), and delivery recovers — both while that run is under way.
        realm.duringEnumeration = () -> reconciler.deliveryRecovered(reconciler.failureBegins());

        reconciler.request();
        clock.advance(Duration.ofSeconds(1));

        assertEquals(2, completions(), "the run under way, and the one the recovery asked for after it");
        assertEquals(2, snapshotBegins());

        clock.advance(Duration.ofMinutes(10));
        assertEquals(2, completions());
    }

    // RC2-5: the mark belongs to its outage. Two outages one after the other each end in a run of their own — the run
    // the first recovery asked for does not stand in for the second.
    @Test
    void twoOutagesInARow_eachEndInOneRunOfItsOwn() throws Exception {
        VirtualScheduler clock = new VirtualScheduler();
        Reconciler reconciler = reconcilerOn(clock, receiver, Duration.ofMinutes(60));
        FeedSender sender = new FeedSender(pending, receiver.client(), enabledWatch(), reconciler, duration -> { });
        reconciler.start();
        clock.advance(Duration.ofSeconds(30));
        assertEquals(1, completions());

        changesRouteDown = true;
        pending.put(entry(1));
        assertEquals(FeedSender.Outcome.FAILED, sender.drainOnce());
        changesRouteDown = false;
        assertEquals(FeedSender.Outcome.SENT, sender.drainOnce());
        clock.advance(Duration.ofSeconds(5));
        assertEquals(2, completions(), "the first outage's end");

        changesRouteDown = true;
        pending.put(entry(2));
        assertEquals(FeedSender.Outcome.FAILED, sender.drainOnce());
        changesRouteDown = false;
        assertEquals(FeedSender.Outcome.SENT, sender.drainOnce());
        clock.advance(Duration.ofSeconds(5));

        assertEquals(3, completions(), "the second outage's end: the first outage's run began before this failure");

        clock.advance(Duration.ofMinutes(10));
        assertEquals(3, completions());
    }

    // RC2-5: a sender that never failed has nothing to recover from — its first success asks for no run.
    @Test
    void aSendersFirstEverSuccess_asksForNoRun() throws Exception {
        VirtualScheduler clock = new VirtualScheduler();
        Reconciler reconciler = reconcilerOn(clock, receiver, Duration.ofMinutes(60));
        FeedSender sender = new FeedSender(pending, receiver.client(), enabledWatch(), reconciler, duration -> { });

        pending.put(entry(1));
        assertEquals(FeedSender.Outcome.SENT, sender.drainOnce());
        clock.advance(Duration.ofMinutes(5));

        assertEquals(0, snapshotBegins());
    }

    // RC2-7 (F1 / D-4 through the real sender): an outage of every route, long enough for a period run and a chain of
    // retries to fail, ends in exactly one completed run — whichever of the pending retry and the sender's recovery
    // arrives first — and leaves no retry behind.
    @Test
    void anOutageOfEveryRoute_endsInOneRun_whicheverOfRetryAndRecoveryComesFirst() throws Exception {
        outageEndsInOneRun(true);
        outageEndsInOneRun(false);
    }

    private void outageEndsInOneRun(boolean retryFirst) throws Exception {
        String order = retryFirst ? "retry first" : "recovery first";

        try (StubReceiver fresh = new StubReceiver()) {
            AtomicBoolean down = new AtomicBoolean();
            fresh.answer(request -> down.get()
                    ? StubReceiver.Answer.status(503)
                    : request.target().endsWith("/completion") ? StubReceiver.Answer.ok(COUNTS) : StubReceiver.Answer.ok("{}"));
            VirtualScheduler clock = new VirtualScheduler();
            PendingChanges queue = new PendingChanges(PendingChanges.DEFAULT_CAPACITY, () -> { });
            Reconciler reconciler = reconcilerOn(clock, fresh, Duration.ofMinutes(10));
            FeedSender sender = new FeedSender(queue, fresh.client(), enabledWatch(), reconciler, duration -> { });
            reconciler.start();

            clock.advance(Duration.ofSeconds(30));
            assertEquals(1, count(fresh, "/completion"), order + ": the start run");

            down.set(true);
            queue.put(entry(1));
            assertEquals(FeedSender.Outcome.FAILED, sender.drainOnce());

            // Until t = 990 s: the period run at 600 s fails, then a retry fails every minute; so do the sender's attempts.
            for (int minute = 0; minute < 16; minute++) {
                clock.advance(Duration.ofSeconds(60));
                assertEquals(FeedSender.Outcome.FAILED, sender.drainOnce());
            }

            assertEquals(1, count(fresh, "/completion"), order + ": every attempt of the outage failed");
            assertEquals(1, clock.pendingOneShots(), order + ": exactly one retry pending");

            down.set(false);

            if (retryFirst) {
                clock.advance(Duration.ofSeconds(60));
                assertEquals(2, count(fresh, "/completion"), order + ": the retry's run completed");
                assertEquals(FeedSender.Outcome.SENT, sender.drainOnce());
                clock.advance(Duration.ofSeconds(60));
            } else {
                assertEquals(FeedSender.Outcome.SENT, sender.drainOnce());
                clock.advance(Duration.ofSeconds(120));
            }

            assertEquals(2, count(fresh, "/completion"), order + ": the start run and one run at the outage's end");
            assertEquals(0, clock.pendingOneShots(), order + ": no retry left behind");
            assertEquals(0, queue.size(), order + ": the change was delivered");
        }
    }

    private Reconciler wiredAsTheFactoryDoes(VirtualScheduler clock) {
        AtomicReference<Reconciler> holder = new AtomicReference<>();
        ListenerEnablementWatch watch = new ListenerEnablementWatch(
                () -> sighting, () -> holder.get().request(), () -> holder.get().requestIfSkipped());
        holder.set(new Reconciler(new ThreeUsers(), receiver.client(), watch::isEnabled, clock, Duration.ofMinutes(60), () -> SNAPSHOT));
        watch.scheduleOn(clock);
        holder.get().start();

        return holder.get();
    }

    // RC2-12: the first reading finds realm mustask WITHOUT the listener. The start run (30 s) is skipped — the state is
    // known, "not enabled" — and the listener added later is a re-enablement: one run, not the start run made up as well.
    @Test
    void aRealmFirstSeenWithoutTheListener_thenEnabled_isReconciledOnce() {
        VirtualScheduler clock = new VirtualScheduler();
        sighting = ListenerEnablementWatch.Sighting.NOT_ENABLED;
        wiredAsTheFactoryDoes(clock);

        clock.advance(Duration.ofSeconds(100));
        assertEquals(0, snapshotBegins(), "frozen: nothing sent, the start run skipped");

        sighting = ListenerEnablementWatch.Sighting.ENABLED;
        clock.advance(Duration.ofSeconds(60));
        assertEquals(1, completions(), "the re-enablement's reconciliation");

        clock.advance(Duration.ofMinutes(10));
        assertEquals(1, completions(), "and the skipped start run is not made up a second time");
    }

    // RC2-14: the operator removes the listener mid-life: a period run in the window is skipped, and adding it back
    // runs exactly one reconciliation (the skipped period run is not made up on top of it).
    @Test
    void aListenerRemovedAndAddedBack_costsOneRun_notOnePerSkippedTrigger() {
        VirtualScheduler clock = new VirtualScheduler();
        wiredAsTheFactoryDoes(clock);
        clock.advance(Duration.ofSeconds(40));
        assertEquals(1, completions(), "the start run");

        sighting = ListenerEnablementWatch.Sighting.NOT_ENABLED;
        clock.advance(Duration.ofMinutes(61));
        assertEquals(1, completions(), "removed: the period run at 60 min is skipped");

        sighting = ListenerEnablementWatch.Sighting.ENABLED;
        clock.advance(Duration.ofMinutes(2));
        assertEquals(2, completions(), "added back: one reconciliation");

        clock.advance(Duration.ofMinutes(10));
        assertEquals(2, completions());
    }

    // RC2-11: a realm that vanishes after the state was known (a stand reset in flight) freezes the feed; its return with
    // the listener is a re-enablement and runs one reconciliation.
    @Test
    void aRealmThatVanishesAndReturns_isReconciledOnceOnReturn() {
        VirtualScheduler clock = new VirtualScheduler();
        wiredAsTheFactoryDoes(clock);
        clock.advance(Duration.ofSeconds(40));
        assertEquals(1, completions());

        sighting = ListenerEnablementWatch.Sighting.NO_REALM;
        clock.advance(Duration.ofMinutes(3));
        assertEquals(1, completions());

        sighting = ListenerEnablementWatch.Sighting.ENABLED;
        clock.advance(Duration.ofMinutes(1));
        assertEquals(2, completions());

        clock.advance(Duration.ofMinutes(10));
        assertEquals(2, completions());
    }

    // RC2-9: a realm import so slow that it ends after the 5 quick minutes. The start run (30 s) was skipped, the checks
    // went to one a minute, and the first one that sees the realm makes the skipped run up — once, within a minute.
    @Test
    void aRealmImportedAfterTheQuickChecksEnded_isReconciledWithinAMinuteOfItsArrival() {
        VirtualScheduler clock = new VirtualScheduler();
        sighting = ListenerEnablementWatch.Sighting.NO_REALM;
        wiredAsTheFactoryDoes(clock);

        clock.advance(Duration.ofMinutes(7));
        assertEquals(0, snapshotBegins(), "no realm yet: held");

        sighting = ListenerEnablementWatch.Sighting.ENABLED;
        clock.advance(Duration.ofSeconds(60));
        assertEquals(1, completions(), "made up within a minute of the arrival");

        clock.advance(Duration.ofMinutes(10));
        assertEquals(1, completions(), "once");
    }

    // RC2-13 with RC2-9: twenty probes in a row throw (realm not readable), the start run is skipped meanwhile; the next
    // probe that answers makes it up once, and the failing probe was reported once, not twenty times.
    @Test
    void checksThatFailUntilAfterTheStartRun_stillLeaveOneReconciliation_andOneLogLine() {
        VirtualScheduler clock = new VirtualScheduler();
        AtomicInteger probes = new AtomicInteger();
        AtomicReference<Reconciler> holder = new AtomicReference<>();
        ListenerEnablementWatch watch = new ListenerEnablementWatch(() -> {
            if (probes.incrementAndGet() <= 20) {
                throw new IllegalStateException("realm not readable yet");
            }

            return ListenerEnablementWatch.Sighting.ENABLED;
        }, () -> holder.get().request(), () -> holder.get().requestIfSkipped());
        holder.set(new Reconciler(new ThreeUsers(), receiver.client(), watch::isEnabled, clock, Duration.ofMinutes(60), () -> SNAPSHOT));

        try (LogCapture log = LogCapture.of(ListenerEnablementWatch.class)) {
            watch.scheduleOn(clock);
            holder.get().start();
            clock.advance(Duration.ofMinutes(2));

            assertEquals(1, completions(), "the 21st probe, at 42 s, sees the realm; the start run skipped at 30 s is made up once");
            assertEquals(1, log.messagesAtLeast(LogCapture.INFO).stream().filter(message -> message.contains("could not read")).count(),
                    "the failing probe is reported once, not twenty times: " + log.text());
        }
    }

    // RC2-6: real threads on the reconciler's triggers — a retry's, a period's and the sender's recovery arriving
    // together, and storms of requests. The virtual-clock tests above pin the orders; this one looks for what only a real
    // interleaving shows: a trigger nobody served, a flag left set that makes the next lone request run twice, a stuck
    // run. (The one µs window it can reach — see qa.md, Observations — gives two runs, never none.)
    @Test
    void triggersFromManyThreads_leaveNoTriggerUnserved_noStuckFlag_andNoMissingRecoveryRun() throws Exception {
        Logger noise = Logger.getLogger(Reconciler.class.getName());
        Level before = noise.getLevel();
        noise.setLevel(Level.WARNING);

        ScheduledThreadPoolExecutor scheduler = new ScheduledThreadPoolExecutor(1, runnable -> {
            Thread thread = new Thread(runnable, "qa-reconciler");
            thread.setDaemon(true);
            return thread;
        });
        AtomicInteger begins = new AtomicInteger();
        AtomicInteger completionsSeen = new AtomicInteger();
        AtomicLong lastEnumerationNanos = new AtomicLong();

        try {
            RealmSource source = new RealmSource() {
                @Override
                public Enumeration enumerate() {
                    begins.incrementAndGet();
                    lastEnumerationNanos.set(System.nanoTime());

                    return new Enumeration(1_790_000_000_000_000L, List.of("u-1", "u-2"));
                }

                @Override
                public List<UserEntry> read(List<String> ids) {
                    return ids.stream().map(id -> new UserEntry(id, 1_790_000_000_000_001L, "n-" + id, null, null, null, true, false)).toList();
                }
            };
            PushClient client = mock(PushClient.class);
            when(client.send(anyString(), anyString(), any())).thenAnswer(invocation -> {
                String target = invocation.getArgument(1);

                if (target.endsWith("/completion")) {
                    completionsSeen.incrementAndGet();

                    return new PushClient.Response(200, COUNTS);
                }

                return new PushClient.Response(200, "{}");
            });
            Reconciler reconciler = new Reconciler(source, client, () -> true, scheduler, Duration.ofMinutes(60), () -> SNAPSHOT);

            int rounds = 300;
            int retryAgainstRecoveryRounds = 0;
            int twoRuns = 0;

            for (int round = 0; round < rounds; round++) {
                settle(scheduler, begins);
                int completedBefore = completionsSeen.get();
                long mark = reconciler.failureBegins();
                AtomicLong latestRequestStart = new AtomicLong();
                int kind = round % 3;

                // Only request() is a trigger that needs a run enumerating after it; a recovery that finds a run begun
                // since its mark under way is covered by that run, which may have enumerated a moment earlier.
                Runnable request = () -> {
                    latestRequestStart.accumulateAndGet(System.nanoTime(), Math::max);
                    reconciler.request();
                };
                Runnable recovery = () -> reconciler.deliveryRecovered(mark);
                // Kind 0: exactly the end of an outage — the pending retry's request and the recovery, together.
                // Kind 1: a storm — a period, a retry, another retry and the recovery.
                // Kind 2: the recovery alone — no run begun since its mark, so it must run exactly one.
                List<Runnable> triggers = switch (kind) {
                    case 0 -> List.of(request, recovery);
                    case 1 -> List.of(request, request, request, recovery);
                    default -> List.of(recovery);
                };

                releaseTogether(triggers);
                settle(scheduler, begins);

                int ran = completionsSeen.get() - completedBefore;
                assertTrue(ran >= 1, "round " + round + ": every trigger was dropped — no run followed");
                assertTrue(lastEnumerationNanos.get() >= latestRequestStart.get(),
                        "round " + round + ": the last request was not followed by a run that enumerated after it");

                if (kind == 0) {
                    retryAgainstRecoveryRounds++;
                    assertTrue(ran <= 2, "round " + round + ": " + ran + " runs for a retry and a recovery");
                    twoRuns += ran == 2 ? 1 : 0;
                } else if (kind == 2) {
                    assertEquals(1, ran, "round " + round + ": a recovery with no run since its mark runs exactly one");
                }

                // No flag was left behind: one lone request makes exactly one run.
                int completedAfter = completionsSeen.get();
                reconciler.request();
                settle(scheduler, begins);
                assertEquals(1, completionsSeen.get() - completedAfter, "round " + round + ": a lone request after the burst made another number of runs");
            }

            // A measurement, not a bound: a retry's request and a recovery released at the same instant on two plain
            // threads. In the provider the retry fires on the scheduler's own thread, which a run occupies, so the
            // orders this reaches by chance are the ones ReconcilerTest pins on a virtual clock.
            System.out.println("QA re-check 2: of " + retryAgainstRecoveryRounds + " retry-against-recovery rounds released together, "
                    + twoRuns + " ran twice");
        } finally {
            noise.setLevel(before);
            scheduler.shutdownNow();
        }
    }

    private static void releaseTogether(List<Runnable> triggers) throws InterruptedException {
        CountDownLatch go = new CountDownLatch(1);
        List<Thread> threads = new ArrayList<>();

        for (Runnable trigger : triggers) {
            Thread thread = new Thread(() -> {
                try {
                    go.await();
                } catch (InterruptedException ex) {
                    Thread.currentThread().interrupt();
                    return;
                }

                trigger.run();
            });
            thread.setDaemon(true);
            thread.start();
            threads.add(thread);
        }

        go.countDown();

        for (Thread thread : threads) {
            thread.join(10_000);
            assertFalse(thread.isAlive(), "a trigger thread is stuck");
        }
    }

    /** Waits until the scheduler's queue is drained and no run has begun for a few passes in a row. */
    private static void settle(ScheduledThreadPoolExecutor scheduler, AtomicInteger begins) throws Exception {
        int stable = 0;
        int seen = -1;

        for (int pass = 0; pass < 2_000 && stable < 4; pass++) {
            scheduler.submit(() -> { }).get(10, TimeUnit.SECONDS);
            int now = begins.get();
            stable = now == seen ? stable + 1 : 0;
            seen = now;
            TimeUnit.MILLISECONDS.sleep(1);
        }

        assertEquals(4, stable, "the scheduler never settled");
    }

    // RC2-15: the factory's lifecycle around the 2 s watch: postInit starts the sender and the scheduler, close stops both
    // — no thread of the provider outlives it — and a provider started again after a close behaves the same.
    @Test
    void closingTheProvider_endsItsThreads_andItCanStartAgain() throws Exception {
        for (int start = 0; start < 2; start++) {
            Config.Scope scope = mock(Config.Scope.class);
            when(scope.get(FeedOptions.ENDPOINT)).thenReturn("http://127.0.0.1:9");
            when(scope.get(FeedOptions.PUSH_SECRET)).thenReturn(StubReceiver.SECRET);
            when(scope.getBoolean(FeedOptions.REQUIRE_HTTPS, Boolean.TRUE)).thenReturn(Boolean.FALSE);
            when(scope.getInt(FeedOptions.RECONCILE_PERIOD_MINUTES, FeedOptions.DEFAULT_RECONCILE_PERIOD_MINUTES)).thenReturn(60);
            UserProjectionListenerFactory factory = new UserProjectionListenerFactory();

            factory.init(scope);
            factory.postInit(mock(KeycloakSessionFactory.class));
            assertEquals(2, providerThreads().size(), "start " + start + ": the sender and the scheduler's thread are running");
            factory.close();

            long deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(5);

            while (!providerThreads().isEmpty() && System.nanoTime() < deadline) {
                TimeUnit.MILLISECONDS.sleep(50);
            }

            assertEquals(List.of(), providerThreads(), "start " + start + ": a thread of the provider outlived close()");
        }
    }

    private static List<String> providerThreads() {
        return Thread.getAllStackTraces().keySet().stream()
                .filter(thread -> thread.isAlive() && thread.getName().startsWith("mustask-user-projection-"))
                .map(Thread::getName)
                .sorted()
                .toList();
    }
}
