package mustask.keycloak.userprojection;

import java.time.Duration;
import java.util.List;
import java.util.PriorityQueue;
import java.util.concurrent.AbstractExecutorService;
import java.util.concurrent.Callable;
import java.util.concurrent.Delayed;
import java.util.concurrent.Executors;
import java.util.concurrent.RejectedExecutionException;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.ScheduledFuture;
import java.util.concurrent.TimeUnit;

/**
 * A scheduled executor on a virtual clock: nothing runs until the test calls
 * {@link #advance}, and then every task due by the new time runs on the
 * test's own thread, in time order — so minutes of retries and periods run in
 * milliseconds and the same way every time. A task that throws fails the
 * test instead of vanishing into its future.
 */
final class VirtualScheduler extends AbstractExecutorService implements ScheduledExecutorService {

    private final PriorityQueue<Task<?>> queue = new PriorityQueue<>();

    private long now;

    private long sequence;

    private boolean shutdown;

    /** Moves the clock forward, running every task due by then (tasks scheduled meanwhile included). */
    void advance(Duration by) {
        long until = now + by.toMillis();

        for (Task<?> next = queue.peek(); next != null && next.at <= until; next = queue.peek()) {
            queue.poll();
            now = Math.max(now, next.at);
            next.runDue();
        }

        now = until;
    }

    /** One-shot tasks still waiting to run; a periodic task is not counted, a cancelled one neither. */
    long pendingOneShots() {
        return queue.stream().filter(task -> task.period == 0 && !task.isCancelled()).count();
    }

    @Override
    public void execute(Runnable command) {
        schedule(command, 0, TimeUnit.MILLISECONDS);
    }

    @Override
    public ScheduledFuture<?> schedule(Runnable command, long delay, TimeUnit unit) {
        return enqueue(new Task<>(Executors.callable(command), now + unit.toMillis(delay), 0));
    }

    @Override
    public <V> ScheduledFuture<V> schedule(Callable<V> callable, long delay, TimeUnit unit) {
        return enqueue(new Task<>(callable, now + unit.toMillis(delay), 0));
    }

    @Override
    public ScheduledFuture<?> scheduleAtFixedRate(Runnable command, long initialDelay, long period, TimeUnit unit) {
        return enqueue(new Task<>(Executors.callable(command), now + unit.toMillis(initialDelay), unit.toMillis(period)));
    }

    @Override
    public ScheduledFuture<?> scheduleWithFixedDelay(Runnable command, long initialDelay, long delay, TimeUnit unit) {
        return scheduleAtFixedRate(command, initialDelay, delay, unit);
    }

    private <V> Task<V> enqueue(Task<V> task) {
        if (shutdown) {
            // What a real ScheduledThreadPoolExecutor does once shut down.
            throw new RejectedExecutionException("The virtual scheduler is shut down.");
        }

        task.sequence = sequence++;
        queue.add(task);

        return task;
    }

    @Override
    public void shutdown() {
        shutdown = true;
    }

    @Override
    public List<Runnable> shutdownNow() {
        shutdown = true;
        queue.clear();

        return List.of();
    }

    @Override
    public boolean isShutdown() {
        return shutdown;
    }

    @Override
    public boolean isTerminated() {
        return shutdown;
    }

    @Override
    public boolean awaitTermination(long timeout, TimeUnit unit) {
        return shutdown;
    }

    /** One scheduled task; periodic when {@code period > 0}. */
    private final class Task<V> implements ScheduledFuture<V> {

        private final Callable<V> callable;

        private final long period;

        private long at;

        private long sequence;

        private boolean cancelled;

        private boolean done;

        private V result;

        private Task(Callable<V> callable, long at, long period) {
            this.callable = callable;
            this.at = at;
            this.period = period;
        }

        private void runDue() {
            if (cancelled) {
                return;
            }

            try {
                result = callable.call();
            } catch (Exception ex) {
                done = true;
                throw new IllegalStateException("A task on the virtual scheduler threw.", ex);
            }

            if (period > 0) {
                at += period;
                enqueue(this);
            } else {
                done = true;
            }
        }

        @Override
        public long getDelay(TimeUnit unit) {
            return unit.convert(at - now, TimeUnit.MILLISECONDS);
        }

        @Override
        public int compareTo(Delayed other) {
            if (other instanceof Task<?> task) {
                int byTime = Long.compare(at, task.at);

                return byTime != 0 ? byTime : Long.compare(sequence, task.sequence);
            }

            return Long.compare(getDelay(TimeUnit.MILLISECONDS), other.getDelay(TimeUnit.MILLISECONDS));
        }

        @Override
        public boolean cancel(boolean mayInterruptIfRunning) {
            if (done || cancelled) {
                return false;
            }

            cancelled = true;

            return true;
        }

        @Override
        public boolean isCancelled() {
            return cancelled;
        }

        @Override
        public boolean isDone() {
            return done || cancelled;
        }

        @Override
        public V get() {
            if (!done) {
                throw new IllegalStateException("Not run yet: the virtual scheduler runs a task only when the test advances time.");
            }

            return result;
        }

        @Override
        public V get(long timeout, TimeUnit unit) {
            return get();
        }
    }
}
