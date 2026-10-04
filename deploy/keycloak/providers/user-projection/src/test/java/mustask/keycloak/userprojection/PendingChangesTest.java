package mustask.keycloak.userprojection;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.time.Duration;
import java.util.List;
import java.util.concurrent.atomic.AtomicInteger;
import org.junit.jupiter.api.Test;

/** ADR-0025 § 4: one entry per user, the newest, a deletion dominating; bounded. */
class PendingChangesTest {

    private final AtomicInteger overflows = new AtomicInteger();

    private final PendingChanges pending = new PendingChanges(3, overflows::incrementAndGet);

    private static UserEntry state(String id, long moment, String lastName) {
        return new UserEntry(id, moment, "u-" + id, "Анна", lastName, null, true, false);
    }

    private static UserEntry deletion(String id, long moment) {
        return new UserEntry(id, moment, "u-" + id, "Анна", "Удалённая", null, false, true);
    }

    @Test
    void theNewestStateOfAUser_wins() {
        pending.put(state("a", 2, "Новая"));
        pending.put(state("a", 1, "Старая"));

        assertEquals(List.of(state("a", 2, "Новая")), pending.take(10));
    }

    @Test
    void aDeletion_dominatesAnyState_whateverItsMarker() {
        pending.put(deletion("a", 1));
        pending.put(state("a", 5, "Позже"));

        assertEquals(List.of(deletion("a", 1)), pending.take(10));

        pending.put(state("b", 5, "Раньше"));
        pending.put(deletion("b", 1));

        assertEquals(List.of(deletion("b", 1)), pending.take(10));
    }

    @Test
    void take_givesAtMostTheBatch_oldestQueuedFirst() {
        pending.put(state("a", 1, "А"));
        pending.put(state("b", 2, "Б"));
        pending.put(state("c", 3, "В"));

        assertEquals(List.of("a", "b"), pending.take(2).stream().map(UserEntry::id).toList());
        assertEquals(1, pending.size());
    }

    @Test
    void aRequeuedBatch_losesToANewerStateQueuedMeanwhile() {
        pending.put(state("a", 1, "Отправленная"));
        List<UserEntry> batch = pending.take(10);
        pending.put(state("a", 2, "Новее"));

        pending.requeue(batch);

        assertEquals(List.of(state("a", 2, "Новее")), pending.take(10));
    }

    @Test
    void overTheCap_theQueueIsDroppedAndAReconciliationRequested() {
        pending.put(state("a", 1, "А"));
        pending.put(state("b", 1, "Б"));
        pending.put(state("c", 1, "В"));
        pending.put(state("a", 2, "А2"));

        assertEquals(0, overflows.get(), "a known user does not grow the queue");

        pending.put(state("d", 1, "Г"));

        assertEquals(1, overflows.get());
        assertEquals(0, pending.size());
    }

    @Test
    void awaitSize_returnsOnceEnoughAreQueued_orAtTheTimeout() throws InterruptedException {
        assertEquals(0, pending.awaitSize(1, Duration.ofMillis(20)));

        Thread producer = new Thread(() -> pending.put(state("a", 1, "А")));
        producer.start();

        assertTrue(pending.awaitSize(1, Duration.ofSeconds(5)) >= 1);
        producer.join();
    }
}
