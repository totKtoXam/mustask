package mustask.keycloak.userprojection;

import java.nio.charset.StandardCharsets;
import java.security.GeneralSecurityException;
import java.security.MessageDigest;
import java.util.HexFormat;
import java.util.Locale;
import javax.crypto.Mac;
import javax.crypto.spec.SecretKeySpec;

/**
 * The push channel's signature (mod-014 api-contract.md B.2): HMAC-SHA256,
 * keyed by the secret's UTF-8 bytes, over the canonical string — five lines
 * joined by LF, no trailing newline:
 * {@code v1}, the timestamp header value, the method in upper case, the
 * target below {@code /api/user-directory/feed/}, and the lowercase hex
 * SHA-256 of the body. The same test vectors check this class and MUSTASK's
 * verifier (push-signature-vectors.json).
 */
public final class PushSigner {

    public static final String TIMESTAMP_HEADER = "Mustask-Push-Timestamp";

    public static final String SIGNATURE_HEADER = "Mustask-Push-Signature";

    public static final String VERSION = "v1";

    private static final HexFormat HEX = HexFormat.of();

    private PushSigner() {
    }

    public static String canonical(String timestamp, String method, String target, byte[] body) {
        return String.join("\n", VERSION, timestamp, method.toUpperCase(Locale.ROOT), target, sha256Hex(body));
    }

    /** The {@link #SIGNATURE_HEADER} value: {@code v1=} and 64 lowercase hex digits. */
    public static String sign(String secret, String canonical) {
        try {
            Mac mac = Mac.getInstance("HmacSHA256");
            mac.init(new SecretKeySpec(secret.getBytes(StandardCharsets.UTF_8), "HmacSHA256"));

            return VERSION + "=" + HEX.formatHex(mac.doFinal(canonical.getBytes(StandardCharsets.UTF_8)));
        } catch (GeneralSecurityException ex) {
            throw new IllegalStateException("HmacSHA256 is not available", ex);
        }
    }

    public static String sha256Hex(byte[] body) {
        try {
            return HEX.formatHex(MessageDigest.getInstance("SHA-256").digest(body));
        } catch (GeneralSecurityException ex) {
            throw new IllegalStateException("SHA-256 is not available", ex);
        }
    }
}
