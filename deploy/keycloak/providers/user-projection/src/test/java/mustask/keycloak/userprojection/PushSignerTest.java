package mustask.keycloak.userprojection;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import org.junit.jupiter.api.Test;

/**
 * api-contract.md B.2: the provider signs exactly as MUSTASK verifies — both
 * sides read the same vectors (Mustask.Contracts.Tests' PushSignatureVectorsTests
 * is the other half).
 */
class PushSignerTest {

    private static JsonNode vectors() throws IOException {
        try (InputStream in = PushSignerTest.class.getResourceAsStream("/push-signature-vectors.json")) {
            return new ObjectMapper().readTree(in).get("vectors");
        }
    }

    @Test
    void everySharedVector_isSignedAsMustaskVerifiesIt() throws IOException {
        JsonNode vectors = vectors();

        assertTrue(vectors.size() >= 1, "no vectors read");

        for (JsonNode vector : vectors) {
            byte[] body = vector.get("body").asText().getBytes(StandardCharsets.UTF_8);
            String canonical = PushSigner.canonical(
                    vector.get("timestamp").asText(), vector.get("method").asText(), vector.get("target").asText(), body);

            assertEquals(vector.get("bodySha256").asText(), PushSigner.sha256Hex(body), vector.get("name").asText());
            assertEquals(vector.get("canonical").asText(), canonical, vector.get("name").asText());
            assertEquals(vector.get("signature").asText(), PushSigner.sign(vector.get("secret").asText(), canonical), vector.get("name").asText());
        }
    }

    @Test
    void theWorkedExampleOfTheContract_isTheFirstVector() throws IOException {
        JsonNode first = vectors().get(0);

        assertEquals("v1=d4f1bd083b09951e40e80e0d65f0b7348dff08c2e89a23022e27cb9606155dce", first.get("signature").asText());
    }

    @Test
    void theMethod_isUpperCasedInTheCanonicalString() {
        assertEquals(
                PushSigner.canonical("1", "PUT", "x", new byte[0]),
                PushSigner.canonical("1", "put", "x", new byte[0]));
    }
}
