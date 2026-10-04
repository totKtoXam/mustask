package mustask.keycloak.userprojection;

import java.util.concurrent.atomic.AtomicReference;
import java.util.function.Function;
import org.keycloak.models.KeycloakSession;
import org.keycloak.models.KeycloakSessionFactory;
import org.keycloak.models.utils.KeycloakModelUtils;

/** {@link TransactionRunner} through {@code KeycloakModelUtils.runJobInTransaction}. */
public final class KeycloakTransactions implements TransactionRunner {

    @Override
    public <T> T call(KeycloakSessionFactory factory, Function<KeycloakSession, T> task) {
        AtomicReference<T> result = new AtomicReference<>();

        KeycloakModelUtils.runJobInTransaction(factory, session -> result.set(task.apply(session)));

        return result.get();
    }
}
