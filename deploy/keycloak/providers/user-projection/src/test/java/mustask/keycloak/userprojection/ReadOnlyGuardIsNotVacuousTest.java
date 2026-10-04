package mustask.keycloak.userprojection;

import static org.junit.jupiter.api.Assertions.assertEquals;

import com.tngtech.archunit.core.importer.ClassFileImporter;
import java.util.List;
import java.util.Set;
import java.util.TreeSet;
import org.junit.jupiter.api.Test;

/**
 * mod-014 US-6 "The read-only guard catches a write": the same rule over
 * fixture classes that write a user, read its credentials, enumerate realms
 * and look up another realm fails on each, naming the caller and the member
 * — so the guard cannot pass vacuously.
 */
class ReadOnlyGuardIsNotVacuousTest {

    @Test
    void theGuard_namesEveryForbiddenCallOfTheFixtures() {
        List<String> violations = ReadOnlyGuard.violations(new ClassFileImporter().importPackages("mustask.keycloak.guardfixtures"));

        assertEquals(
                Set.of(
                        "mustask.keycloak.guardfixtures.WritesAUser -> org.keycloak.models.UserModel#setEnabled",
                        "mustask.keycloak.guardfixtures.ReadsCredentials -> org.keycloak.models.UserModel#credentialManager",
                        "mustask.keycloak.guardfixtures.EnumeratesRealms -> org.keycloak.models.RealmProvider#getRealmsStream",
                        "mustask.keycloak.guardfixtures.LooksUpAnotherRealm -> org.keycloak.models.RealmProvider#getRealmByName",
                        "mustask.keycloak.guardfixtures.BindsARealmElsewhere -> org.keycloak.models.KeycloakContext#setRealm"),
                new TreeSet<>(violations));
    }
}
