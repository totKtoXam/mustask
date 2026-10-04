package mustask.keycloak.userprojection;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.io.IOException;
import java.time.Duration;
import java.util.ArrayList;
import java.util.List;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.Test;

/**
 * ADR-0025 § 4, delivery; mod-014 US-5 (outages) and US-6 (the credential),
 * the provider half — against a stub receiver that checks signatures as
 * MUSTASK does.
 */
class FeedSenderTest {

    private final StubReceiver receiver = new StubReceiver();

    private final PendingChanges pending = new PendingChanges(PendingChanges.DEFAULT_CAPACITY, () -> { });

    /** The reconciler's side, recorded: each recovery with the mark it handed back. */
    private final List<Long> recoveries = new ArrayList<>();

    /** What {@link DeliveryRecovery#failureBegins()} answers now — the reconciler's count of runs begun. */
    private long runsBegun = 7;

    private final DeliveryRecovery recovery = new DeliveryRecovery() {
        @Override
        public long failureBegins() {
            return runsBegun;
        }

        @Override
        public void deliveryRecovered(long mark) {
            recoveries.add(mark);
        }
    };

    private boolean enabled = true;

    private final ListenerEnablementWatch watch =
            new ListenerEnablementWatch(() -> ListenerEnablementWatch.Sighting.of(enabled), () -> { }, () -> { });

    private final List<Duration> sleeps = new ArrayList<>();

    private FeedSender sender;

    FeedSenderTest() throws IOException {
        watch.check();
        sender = new FeedSender(pending, receiver.client(), watch, recovery, sleeps::add);
    }

    @AfterEach
    void stopReceiver() {
        receiver.close();
    }

    private static UserEntry entry(int i) {
        return new UserEntry("id-" + i, 1_790_000_000_000_000L + i, "user" + i, "Имя" + i, "Фамилия" + i, "user" + i + "@example.test", true, false);
    }

    @Test
    void aBatch_isSentSigned_toTheChangesRoute() throws InterruptedException {
        pending.put(entry(1));

        assertEquals(FeedSender.Outcome.SENT, sender.drainOnce());

        StubReceiver.Request request = receiver.requests().get(0);
        assertEquals("POST", request.method());
        assertEquals("changes", request.target());
        assertTrue(request.signedRight(), "MUSTASK's check of the signature");
        assertTrue(request.body().contains("\"id\":\"id-1\""));
        assertFalse(request.body().contains("\"deleted\""), "deleted is written only on a deletion");
    }

    @Test
    void batches_holdAtMostFiveHundredEntries() throws InterruptedException {
        for (int i = 0; i < 501; i++) {
            pending.put(entry(i));
        }

        sender.drainOnce();

        assertEquals(1, pending.size());
        assertEquals(500, receiver.requests().get(0).body().split("\"moment\"").length - 1);
    }

    // US-5 "Keycloak does not wait for a stopped MUSTASK; the failure is logged by id" — the batch is kept, no name or e-mail logged.
    @Test
    void aStoppedMustask_keepsTheBatchAndLogsTheFailureByIdOnly() throws InterruptedException {
        receiver.close();
        pending.put(entry(7));

        try (LogCapture log = LogCapture.of(FeedSender.class)) {
            assertEquals(FeedSender.Outcome.FAILED, sender.drainOnce());

            assertEquals(1, pending.size());
            String text = log.text();
            assertTrue(text.contains("id-7"), text);
            assertFalse(text.contains("Фамилия7"), text);
            assertFalse(text.contains("user7@example.test"), text);
            assertFalse(text.contains(StubReceiver.SECRET), text);
        }
    }

    // US-5 "Keycloak does not wait for a slow MUSTASK": the sender's own request times out and keeps the batch.
    @Test
    void aSlowMustask_timesOutOnTheSendersThread_andKeepsTheBatch() throws InterruptedException {
        receiver.answer(request -> new StubReceiver.Answer(200, "{}", 3_000));
        sender = new FeedSender(
                pending, new PushClient(receiver.options(), StubReceiver.CLOCK, Duration.ofMillis(300)), watch, recovery, sleeps::add);
        pending.put(entry(6));

        assertEquals(FeedSender.Outcome.FAILED, sender.drainOnce());
        assertEquals(1, pending.size());
    }

    // US-5 "The projection catches up after MUSTASK comes back": the first success after a failure tells the
    // reconciler once.
    @Test
    void theFirstSuccessAfterAFailure_tellsTheReconcilerOnce() throws InterruptedException {
        receiver.answer(request -> StubReceiver.Answer.status(503));
        pending.put(entry(1));
        assertEquals(FeedSender.Outcome.FAILED, sender.drainOnce());
        assertEquals(List.of(), recoveries);

        receiver.answer(request -> StubReceiver.Answer.ok("{}"));
        assertEquals(FeedSender.Outcome.SENT, sender.drainOnce());
        pending.put(entry(2));
        assertEquals(FeedSender.Outcome.SENT, sender.drainOnce());

        assertEquals(1, recoveries.size());
    }

    // ADR-0025 § 5 (code review F1): the mark handed back is the one taken when the outage's first failure
    // happened — runs begun after it, such as the retry's, cover the outage — not one from a later failure.
    @Test
    void theRecovery_handsBackTheMarkOfTheOutagesFirstFailure() throws InterruptedException {
        receiver.answer(request -> StubReceiver.Answer.status(503));
        pending.put(entry(1));

        assertEquals(FeedSender.Outcome.FAILED, sender.drainOnce());
        runsBegun = 9;
        assertEquals(FeedSender.Outcome.FAILED, sender.drainOnce());

        receiver.answer(request -> StubReceiver.Answer.ok("{}"));
        assertEquals(FeedSender.Outcome.SENT, sender.drainOnce());

        assertEquals(List.of(7L), recoveries);

        runsBegun = 12;
        receiver.answer(request -> StubReceiver.Answer.status(503));
        pending.put(entry(2));
        assertEquals(FeedSender.Outcome.FAILED, sender.drainOnce());
        receiver.answer(request -> StubReceiver.Answer.ok("{}"));
        assertEquals(FeedSender.Outcome.SENT, sender.drainOnce());

        assertEquals(List.of(7L, 12L), recoveries, "the next outage takes a mark of its own");
    }

    // ADR-0025 § 4: back off 1 s, doubling to 60 s.
    @Test
    void failures_backOffDoublingToAMinute() {
        receiver.answer(request -> StubReceiver.Answer.status(500));
        pending.put(entry(1));
        sender = new FeedSender(pending, receiver.client(), watch, recovery, duration -> {
            sleeps.add(duration);

            if (sleeps.size() == 8) {
                sender.stop();
            }
        });

        sender.run();

        assertEquals(
                List.of(1L, 2L, 4L, 8L, 16L, 32L, 60L, 60L),
                sleeps.stream().map(Duration::toSeconds).toList());
    }

    // US-6 "A provider with the wrong credential fails loudly and recovers".
    @Test
    void aRefusedCredential_isLoggedAtErrorAndRetried_neverPrintingTheSecret() throws InterruptedException {
        sender = new FeedSender(
                pending,
                new PushClient(FeedOptions.of(receiver.options().endpoint().toString(), "a-wrong-secret-not-real-0123456789abcdef", false, 60), StubReceiver.CLOCK),
                watch,
                recovery,
                sleeps::add);
        pending.put(entry(3));

        try (LogCapture log = LogCapture.of(FeedSender.class)) {
            assertEquals(FeedSender.Outcome.FAILED, sender.drainOnce());

            List<String> errors = log.messagesAtLeast(LogCapture.ERROR);
            assertEquals(1, errors.size(), log.text());
            assertTrue(errors.get(0).contains("push-secret"));
            assertFalse(log.text().contains("a-wrong-secret-not-real-0123456789abcdef"));
            assertEquals(1, pending.size(), "retried, not dropped");
        }
    }

    // ADR-0025 § 4: a 400 is a contract bug, or input MUSTASK cannot parse (S1) — dropped and logged at ERROR with MUSTASK's codes.
    @Test
    void aMalformedRefusal_dropsTheBatchAndLogsTheCodes() throws InterruptedException {
        receiver.answer(request -> new StubReceiver.Answer(400,
                "{\"errorCode\":\"user_directory.validation_failed\",\"errors\":{\"changes[0].moment\":[{\"code\":\"user_directory.value_invalid\"}]}}", 0));
        pending.put(entry(4));

        try (LogCapture log = LogCapture.of(FeedSender.class)) {
            assertEquals(FeedSender.Outcome.DROPPED, sender.drainOnce());

            assertEquals(0, pending.size());
            assertTrue(log.text().contains("changes[0].moment: user_directory.value_invalid"), log.text());
        }
    }

    // US-5 "A disabled provider freezes the projection, not the reads": nothing is sent while disabled.
    @Test
    void whileTheListenerIsDisabled_nothingIsSent() throws InterruptedException {
        enabled = false;
        watch.check();
        pending.put(entry(5));

        assertEquals(FeedSender.Outcome.HELD, sender.drainOnce());

        assertTrue(receiver.requests().isEmpty());
        assertEquals(1, pending.size());
    }

    // Code review F13 (NFR Freshness, 10 s): a change captured as Keycloak starts — the realm still being imported, the
    // listener's state unknown — is held only until the quick checks see realm mustask, not for a minute-long poll.
    @Test
    void aChangeCapturedAtTheStart_isSentOnceAQuickCheckSeesTheRealm() throws InterruptedException {
        ListenerEnablementWatch.Sighting[] realm = {ListenerEnablementWatch.Sighting.NO_REALM};
        ListenerEnablementWatch starting = new ListenerEnablementWatch(() -> realm[0], () -> { }, () -> { });
        sender = new FeedSender(pending, receiver.client(), starting, recovery, sleeps::add);
        VirtualScheduler clock = new VirtualScheduler();
        starting.scheduleOn(clock);
        pending.put(entry(8));

        clock.advance(Duration.ofSeconds(4));
        assertEquals(FeedSender.Outcome.HELD, sender.drainOnce(), "no realm yet at 2 s and 4 s: held, not dropped");

        realm[0] = ListenerEnablementWatch.Sighting.ENABLED;
        clock.advance(Duration.ofSeconds(2));

        assertEquals(FeedSender.Outcome.SENT, sender.drainOnce(), "seen enabled at 6 s, 2 s after the realm arrived");
        assertTrue(receiver.requests().get(0).body().contains("\"id\":\"id-8\""));
    }

    @Test
    void anEmptyQueue_sendsNothing() throws InterruptedException {
        assertEquals(FeedSender.Outcome.IDLE, sender.drainOnce());
        assertTrue(receiver.requests().isEmpty());
    }

    @Test
    void aDeletion_isSentWithDeletedTrue() throws InterruptedException {
        pending.put(new UserEntry("gone", 5, "gone.user", null, null, null, false, true));

        sender.drainOnce();

        assertTrue(receiver.requests().get(0).body().contains("\"deleted\":true"));
    }
}
