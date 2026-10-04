package mustask.keycloak.userprojection;

/**
 * What the sender tells the reconciler about an outage of delivery (ADR-0025
 * § 4–5): when delivery starts failing the sender takes a mark, and when
 * delivery succeeds again it hands the mark back. The reconciler then runs a
 * reconciliation only if no run begun since the mark has completed or is
 * under way — the retry of a failed run may already have healed the outage.
 */
public interface DeliveryRecovery {

    /** A mark for "now", taken when delivery starts failing. */
    long failureBegins();

    /** Delivery succeeded again after failing since {@code mark}. */
    void deliveryRecovered(long mark);
}
