package mustask.keycloak.userprojection;

import java.util.function.Function;
import org.keycloak.models.KeycloakSession;
import org.keycloak.models.KeycloakSessionFactory;

/**
 * Runs a read in a short Keycloak transaction of its own — the provider's
 * threads have no request session. {@link KeycloakTransactions} is the real
 * one; tests hand in a mocked session.
 */
public interface TransactionRunner {

    <T> T call(KeycloakSessionFactory factory, Function<KeycloakSession, T> task);
}
