package mustask.keycloak.userprojection;

import com.tngtech.archunit.core.domain.JavaAccess;
import com.tngtech.archunit.core.domain.JavaClass;
import com.tngtech.archunit.core.domain.JavaClasses;
import com.tngtech.archunit.core.domain.JavaFieldAccess;
import java.util.ArrayList;
import java.util.List;
import java.util.Set;

/**
 * The provider's read-only guard (BR-UP-13; D-7; ADR-0025 § 8): every call —
 * and every method reference — from the provider's classes into an
 * {@code org.keycloak} type must be on this named allowlist of reads.
 * A setter, {@code credentialManager()}, {@code getAttributes()}, a realm or
 * model write, a realm enumeration — anything not listed — is a violation
 * naming the calling class and the called member. One setter is listed, and
 * only for one class: {@link ProjectedRealm} may call
 * {@code KeycloakContext#setRealm} (reached through the listed accessor
 * {@code KeycloakSession#getContext}) to bind the provider's own background
 * session to realm {@code mustask}, the realm it has just looked up by name.
 * That is session state — Keycloak's user lookups need a context realm —
 * and writes nothing to Keycloak's model; the same call from any other class
 * fails (fixture {@code BindsARealmElsewhere}). It sees compiled calls, not
 * text, so formatting, aliases and imports do not hide one; like any static
 * rule it is a net for the innocent addition, not a defence against
 * reflection — review stays the first line.
 */
final class ReadOnlyGuard {

    /** {@code owner#member} pairs the provider may call. */
    static final Set<String> ALLOWED = Set.of(
            "org.keycloak.Config$Scope#get",
            "org.keycloak.Config$Scope#getInt",
            "org.keycloak.Config$Scope#getBoolean",
            "org.keycloak.models.KeycloakSession#users",
            "org.keycloak.models.KeycloakSession#realms",
            "org.keycloak.models.KeycloakSession#getTransactionManager",
            "org.keycloak.models.KeycloakSession#getKeycloakSessionFactory",
            "org.keycloak.models.KeycloakSession#getContext",
            "org.keycloak.models.KeycloakContext#setRealm",
            "org.keycloak.models.KeycloakSessionFactory#register",
            "org.keycloak.models.KeycloakTransactionManager#enlistAfterCompletion",
            "org.keycloak.models.AbstractKeycloakTransaction#<init>",
            "org.keycloak.models.utils.KeycloakModelUtils#runJobInTransaction",
            "org.keycloak.models.RealmProvider#getRealmByName",
            "org.keycloak.models.RealmModel#getId",
            "org.keycloak.models.RealmModel#getName",
            "org.keycloak.models.RealmModel#getEventsListenersStream",
            "org.keycloak.models.UserProvider#getUserById",
            "org.keycloak.models.UserProvider#searchForUserStream",
            "org.keycloak.models.UserModel#getId",
            "org.keycloak.models.UserModel#getUsername",
            "org.keycloak.models.UserModel#getFirstName",
            "org.keycloak.models.UserModel#getLastName",
            "org.keycloak.models.UserModel#getEmail",
            "org.keycloak.models.UserModel#isEnabled",
            "org.keycloak.models.UserModel#getServiceAccountClientLink",
            "org.keycloak.models.UserModel$UserRemovedEvent#getRealm",
            "org.keycloak.models.UserModel$UserRemovedEvent#getUser",
            "org.keycloak.models.UserModel$UserRemovedEvent#getKeycloakSession",
            "org.keycloak.events.Event#getType",
            "org.keycloak.events.Event#getRealmId",
            "org.keycloak.events.Event#getUserId",
            "org.keycloak.events.admin.AdminEvent#getResourceType",
            "org.keycloak.events.admin.AdminEvent#getOperationType",
            "org.keycloak.events.admin.AdminEvent#getResourcePath",
            "org.keycloak.events.admin.AdminEvent#getRealmId");

    /** The enums whose own accessors (a switch's {@code values()}, {@code ordinal()}) are reads. */
    static final Set<String> ENUMS = Set.of(
            "org.keycloak.events.EventType",
            "org.keycloak.events.admin.ResourceType",
            "org.keycloak.events.admin.OperationType");

    static final Set<String> ENUM_ACCESSORS = Set.of("values", "valueOf", "ordinal", "name");

    /** The only realm lookup, allowed to one class (BR-UP-13: realm {@code mustask} only). */
    static final String REALM_LOOKUP = "org.keycloak.models.RealmProvider#getRealmByName";

    /** Binding a background session to a realm — session state, allowed to the same one class, for realm {@code mustask} only. */
    static final String REALM_BINDING = "org.keycloak.models.KeycloakContext#setRealm";

    private ReadOnlyGuard() {
    }

    /** Every violation in {@code classes}, as {@code caller -> owner#member}. */
    static List<String> violations(JavaClasses classes) {
        List<String> violations = new ArrayList<>();

        for (JavaClass caller : classes) {
            for (JavaAccess<?> access : caller.getAccessesFromSelf()) {
                if (access instanceof JavaFieldAccess) {
                    continue;
                }

                String owner = access.getTargetOwner().getName();

                if (!owner.startsWith("org.keycloak.")) {
                    continue;
                }

                String member = owner + "#" + access.getName();
                boolean allowed = ALLOWED.contains(member) || (ENUMS.contains(owner) && ENUM_ACCESSORS.contains(access.getName()));

                if (allowed && (REALM_LOOKUP.equals(member) || REALM_BINDING.equals(member))
                        && !access.getOriginOwner().getName().equals(ProjectedRealm.class.getName())) {
                    allowed = false;
                }

                if (!allowed) {
                    violations.add(access.getOriginOwner().getName() + " -> " + member);
                }
            }
        }

        return violations;
    }
}
