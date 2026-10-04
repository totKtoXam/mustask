package mustask.keycloak.userprojection;

import java.time.Clock;
import java.util.UUID;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.ThreadFactory;
import java.util.concurrent.atomic.AtomicReference;
import org.jboss.logging.Logger;
import org.keycloak.Config;
import org.keycloak.events.Event;
import org.keycloak.events.EventListenerProvider;
import org.keycloak.events.EventListenerProviderFactory;
import org.keycloak.events.admin.AdminEvent;
import org.keycloak.models.KeycloakSession;
import org.keycloak.models.KeycloakSessionFactory;
import org.keycloak.models.RealmModel;

/**
 * The event-listener provider {@code mustask-user-projection} (mod-014;
 * ADR-0025). Reads the options at {@code init}; at {@code postInit} registers
 * the removal listener and starts the sender, the reconciler and the
 * enablement watch on two daemon threads of its own, stopped at
 * {@code close}. Missing or invalid options make it log an error and stay
 * inert — a misconfigured feed never stops Keycloak from starting.
 */
public final class UserProjectionListenerFactory implements EventListenerProviderFactory {

    /** The provider id, as realm {@code mustask}'s event listeners name it. */
    public static final String ID = "mustask-user-projection";

    private static final Logger LOG = Logger.getLogger(UserProjectionListenerFactory.class);

    private FeedOptions options;

    private volatile ChangeCapture capture;

    private FeedSender sender;

    private Thread senderThread;

    private ScheduledExecutorService scheduler;

    @Override
    public EventListenerProvider create(KeycloakSession session) {
        ChangeCapture current = capture;

        return current == null ? Inert.INSTANCE : new UserProjectionListener(session, current);
    }

    @Override
    public void init(Config.Scope config) {
        try {
            options = FeedOptions.from(config);
            LOG.infof("mustask-user-projection: configured, %s.", options);
        } catch (IllegalArgumentException ex) {
            options = null;
            LOG.errorf(
                    "mustask-user-projection is inert: %s. Its options come from the Keycloak process's environment "
                            + "(KC_SPI_EVENTS_LISTENER__MUSTASK_USER_PROJECTION__<OPTION>); nothing is pushed to MUSTASK until they are set.",
                    ex.getMessage());
        }
    }

    @Override
    public void postInit(KeycloakSessionFactory factory) {
        if (options == null) {
            return;
        }

        Clock clock = Clock.systemUTC();
        MomentClock moments = new MomentClock(clock);
        TransactionRunner transactions = new KeycloakTransactions();
        PushClient client = new PushClient(options, clock);
        scheduler = Executors.newSingleThreadScheduledExecutor(daemon("mustask-user-projection-reconciler"));

        AtomicReference<Reconciler> reconciler = new AtomicReference<>();
        Runnable requestReconciliation = () -> reconciler.get().request();

        ListenerEnablementWatch watch = new ListenerEnablementWatch(
                () -> transactions.call(factory, session -> sightingIn(ProjectedRealm.find(session))),
                requestReconciliation,
                () -> reconciler.get().requestIfSkipped());

        reconciler.set(new Reconciler(
                new KeycloakRealmSource(factory, moments, transactions),
                client,
                watch::isEnabled,
                scheduler,
                options.reconcilePeriod(),
                UUID::randomUUID));

        PendingChanges pending = new PendingChanges(PendingChanges.DEFAULT_CAPACITY, () -> {
            LOG.errorf("mustask-user-projection: more than %d changes pending; the queue was dropped and a reconciliation follows.",
                    PendingChanges.DEFAULT_CAPACITY);
            requestReconciliation.run();
        });

        ChangeCapture changeCapture = new ChangeCapture(moments, pending);
        factory.register(new UserRemovalListener(changeCapture, watch));

        sender = new FeedSender(pending, client, watch, reconciler.get(), Sleeper.THREAD);
        senderThread = daemon("mustask-user-projection-sender").newThread(sender);
        senderThread.start();

        watch.scheduleOn(scheduler);
        reconciler.get().start();

        capture = changeCapture;
    }

    @Override
    public void close() {
        if (sender != null) {
            sender.stop();
        }

        if (senderThread != null) {
            senderThread.interrupt();
        }

        if (scheduler != null) {
            scheduler.shutdownNow();
        }
    }

    @Override
    public String getId() {
        return ID;
    }

    /** Whether this provider is among {@code realm}'s event listeners; {@code NO_REALM} for a realm that does not exist (yet). */
    static ListenerEnablementWatch.Sighting sightingIn(RealmModel realm) {
        if (realm == null) {
            return ListenerEnablementWatch.Sighting.NO_REALM;
        }

        return ListenerEnablementWatch.Sighting.of(realm.getEventsListenersStream().anyMatch(ID::equals));
    }

    private static ThreadFactory daemon(String name) {
        return runnable -> {
            Thread thread = new Thread(runnable, name);
            thread.setDaemon(true);
            return thread;
        };
    }

    /** The listener of an unconfigured provider: does nothing. */
    private static final class Inert implements EventListenerProvider {

        private static final Inert INSTANCE = new Inert();

        @Override
        public void onEvent(Event event) {
            // Inert: the options are missing or invalid (logged at init).
        }

        @Override
        public void onEvent(AdminEvent event, boolean includeRepresentation) {
            // Inert.
        }

        @Override
        public void close() {
            // Nothing to close.
        }
    }
}
