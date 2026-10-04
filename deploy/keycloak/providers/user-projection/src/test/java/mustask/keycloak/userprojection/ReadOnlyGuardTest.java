package mustask.keycloak.userprojection;

import static org.junit.jupiter.api.Assertions.assertTrue;

import com.tngtech.archunit.core.domain.JavaClasses;
import com.tngtech.archunit.core.importer.ClassFileImporter;
import com.tngtech.archunit.core.importer.ImportOption;
import java.util.List;
import org.junit.jupiter.api.Test;

/** mod-014 US-6 "The provider's read-only guard passes on its sources" (BR-UP-13; ADR-0025 § 8). */
class ReadOnlyGuardTest {

    private static JavaClasses providerClasses() {
        return new ClassFileImporter()
                .withImportOption(ImportOption.Predefined.DO_NOT_INCLUDE_TESTS)
                .importPackages("mustask.keycloak.userprojection");
    }

    @Test
    void everyCallIntoKeycloak_isOnTheAllowlistOfReads() {
        JavaClasses classes = providerClasses();

        assertTrue(classes.contain(UserProjectionListenerFactory.class), "the provider's classes were not imported — the guard would check nothing");
        assertTrue(classes.contain(ChangeCapture.class));

        List<String> violations = ReadOnlyGuard.violations(classes);

        assertTrue(violations.isEmpty(),
                "The provider calls Keycloak beyond its allowlist of reads (BR-UP-13):\n" + String.join("\n", violations));
    }

    @Test
    void theProviderReallyCallsKeycloak_soTheRuleIsNotVacuousOnItsSources() {
        long keycloakCalls = providerClasses().stream()
                .flatMap(type -> type.getAccessesFromSelf().stream())
                .filter(access -> access.getTargetOwner().getName().startsWith("org.keycloak."))
                .count();

        assertTrue(keycloakCalls > 10, "only " + keycloakCalls + " accesses into Keycloak found");
    }
}
