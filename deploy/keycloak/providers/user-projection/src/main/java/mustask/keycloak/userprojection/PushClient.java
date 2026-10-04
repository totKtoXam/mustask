package mustask.keycloak.userprojection;

import java.io.IOException;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.time.Clock;
import java.time.Duration;
import java.util.Objects;

/**
 * Sends one signed request of the push channel (api-contract.md B.2, B.3)
 * with the timeouts of ADR-0025 § 4: 2 s to connect, 10 s for the request.
 * Only ever called from the provider's own threads, never from a Keycloak
 * request thread (BR-UP-7).
 */
public final class PushClient {

    /** The routes' base below the endpoint; a signature's target is the path below it. */
    public static final String FEED_PATH = "/api/user-directory/feed/";

    public static final Duration CONNECT_TIMEOUT = Duration.ofSeconds(2);

    public static final Duration REQUEST_TIMEOUT = Duration.ofSeconds(10);

    private final URI feedBase;

    private final String secret;

    private final Clock clock;

    private final HttpClient http;

    private final Duration requestTimeout;

    public PushClient(FeedOptions options, Clock clock) {
        this(options, clock, REQUEST_TIMEOUT);
    }

    /** With another request timeout — for tests of a slow receiver. */
    PushClient(FeedOptions options, Clock clock, Duration requestTimeout) {
        Objects.requireNonNull(options, "options");
        String base = options.endpoint().toString();
        this.feedBase = URI.create((base.endsWith("/") ? base.substring(0, base.length() - 1) : base) + FEED_PATH);
        this.secret = options.pushSecret();
        this.clock = Objects.requireNonNull(clock, "clock");
        this.requestTimeout = Objects.requireNonNull(requestTimeout, "requestTimeout");
        this.http = HttpClient.newBuilder()
                .connectTimeout(CONNECT_TIMEOUT)
                .followRedirects(HttpClient.Redirect.NEVER)
                .build();
    }

    /** The answer: status and body. Throws on a connection failure or a timeout. */
    public Response send(String method, String target, byte[] body) throws IOException, InterruptedException {
        String timestamp = Long.toString(clock.instant().getEpochSecond());
        String signature = PushSigner.sign(secret, PushSigner.canonical(timestamp, method, target, body));

        HttpRequest request = HttpRequest.newBuilder(feedBase.resolve(target))
                .timeout(requestTimeout)
                .header("Content-Type", "application/json")
                .header(PushSigner.TIMESTAMP_HEADER, timestamp)
                .header(PushSigner.SIGNATURE_HEADER, signature)
                .method(method, HttpRequest.BodyPublishers.ofByteArray(body))
                .build();

        HttpResponse<String> response = http.send(request, HttpResponse.BodyHandlers.ofString());

        return new Response(response.statusCode(), response.body());
    }

    /** A response of MUSTASK. */
    public record Response(int status, String body) {

        public boolean isSuccess() {
            return status >= 200 && status < 300;
        }
    }
}
