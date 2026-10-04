package mustask.keycloak.userprojection;

import java.time.Duration;
import java.util.ArrayList;
import java.util.Collection;
import java.util.Iterator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Objects;

/**
 * The in-memory queue of captured changes (ADR-0025 § 4), coalesced by user
 * id: it holds at most one entry per user — the newest, a deletion dominating
 * any state — so it never holds more entries than the realm has users. A hard
 * cap guards against a bug: on overflow the queue is dropped and
 * {@code onOverflow} runs (it schedules a reconciliation, which heals what
 * the drop lost).
 */
public final class PendingChanges {

    /** The hard cap of ADR-0025 § 4. */
    public static final int DEFAULT_CAPACITY = 50_000;

    private final int capacity;

    private final Runnable onOverflow;

    private final Map<String, UserEntry> entries = new LinkedHashMap<>();

    public PendingChanges(int capacity, Runnable onOverflow) {
        if (capacity < 1) {
            throw new IllegalArgumentException("capacity must be positive");
        }

        this.capacity = capacity;
        this.onOverflow = Objects.requireNonNull(onOverflow, "onOverflow");
    }

    /** Queues an entry, merged with any entry already queued for the same user. */
    public void put(UserEntry entry) {
        Objects.requireNonNull(entry, "entry");

        boolean overflowed;

        synchronized (this) {
            overflowed = merge(entry);
            notifyAll();
        }

        if (overflowed) {
            onOverflow.run();
        }
    }

    /** Puts a batch that failed to send back; newer states queued meanwhile win the merge. */
    public void requeue(Collection<UserEntry> batch) {
        Objects.requireNonNull(batch, "batch");

        boolean overflowed = false;

        synchronized (this) {
            for (UserEntry entry : batch) {
                overflowed |= merge(entry);
            }

            notifyAll();
        }

        if (overflowed) {
            onOverflow.run();
        }
    }

    /** Takes up to {@code max} entries, oldest queued first. */
    public synchronized List<UserEntry> take(int max) {
        List<UserEntry> batch = new ArrayList<>(Math.min(max, entries.size()));
        Iterator<UserEntry> iterator = entries.values().iterator();

        while (iterator.hasNext() && batch.size() < max) {
            batch.add(iterator.next());
            iterator.remove();
        }

        return batch;
    }

    public synchronized int size() {
        return entries.size();
    }

    /** Waits until at least {@code count} entries are queued or {@code timeout} passes; returns the size then. */
    public synchronized int awaitSize(int count, Duration timeout) throws InterruptedException {
        long deadline = System.nanoTime() + timeout.toNanos();

        while (entries.size() < count) {
            long remaining = deadline - System.nanoTime();

            if (remaining <= 0) {
                break;
            }

            long millis = Math.max(1, remaining / 1_000_000L);
            wait(millis);
        }

        return entries.size();
    }

    /** Returns true when the cap was exceeded and the queue dropped. */
    private boolean merge(UserEntry entry) {
        UserEntry held = entries.get(entry.id());

        if (held != null) {
            if (entry.supersedes(held)) {
                entries.put(entry.id(), entry);
            }

            return false;
        }

        if (entries.size() >= capacity) {
            entries.clear();
            return true;
        }

        entries.put(entry.id(), entry);
        return false;
    }
}
