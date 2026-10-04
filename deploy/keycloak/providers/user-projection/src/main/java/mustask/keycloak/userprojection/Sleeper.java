package mustask.keycloak.userprojection;

import java.time.Duration;

/** How the provider's threads wait — the real clock in Keycloak, a recording fake in tests. */
@FunctionalInterface
public interface Sleeper {

    Sleeper THREAD = duration -> Thread.sleep(duration.toMillis());

    void sleep(Duration duration) throws InterruptedException;
}
