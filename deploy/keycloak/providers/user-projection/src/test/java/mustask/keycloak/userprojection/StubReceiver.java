package mustask.keycloak.userprojection;

import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpServer;
import java.io.IOException;
import java.io.OutputStream;
import java.net.InetAddress;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.Executors;
import java.util.function.Function;

/**
 * A stand-in for MUSTASK's push channel on a free local port (the JDK's own
 * HTTP server): records every request, checks its signature the way MUSTASK
 * does (api-contract.md B.2), and answers what the test tells it to.
 */
final class StubReceiver implements AutoCloseable {

    static final String SECRET = "stub-receiver-secret-not-real-0123456789";

    static final Clock CLOCK = Clock.fixed(Instant.parse("2026-10-02T10:00:00Z"), ZoneOffset.UTC);

    private final HttpServer server;

    private final List<Request> requests = new ArrayList<>();

    private volatile Function<Request, Answer> answers = request -> Answer.ok("{}");

    StubReceiver() throws IOException {
        server = HttpServer.create(new InetSocketAddress(InetAddress.getLoopbackAddress(), 0), 0);
        server.setExecutor(Executors.newCachedThreadPool());
        server.createContext("/", this::handle);
        server.start();
    }

    /** Options pointing at this receiver, over plain HTTP as on the stand. */
    FeedOptions options() {
        return FeedOptions.of("http://127.0.0.1:" + server.getAddress().getPort(), SECRET, false, 60);
    }

    PushClient client() {
        return new PushClient(options(), CLOCK);
    }

    void answer(Function<Request, Answer> answers) {
        this.answers = answers;
    }

    synchronized List<Request> requests() {
        return List.copyOf(requests);
    }

    private boolean closed;

    @Override
    public synchronized void close() {
        if (!closed) {
            closed = true;
            server.stop(0);
        }
    }

    private void handle(HttpExchange exchange) throws IOException {
        byte[] body = exchange.getRequestBody().readAllBytes();
        String path = exchange.getRequestURI().getRawPath();
        String target = path.startsWith(PushClient.FEED_PATH) ? path.substring(PushClient.FEED_PATH.length()) : path;
        String timestamp = exchange.getRequestHeaders().getFirst(PushSigner.TIMESTAMP_HEADER);
        String signature = exchange.getRequestHeaders().getFirst(PushSigner.SIGNATURE_HEADER);
        boolean signedRight = timestamp != null && signature != null
                && signature.equals(PushSigner.sign(SECRET, PushSigner.canonical(timestamp, exchange.getRequestMethod(), target, body)));

        Request request = new Request(exchange.getRequestMethod(), target, new String(body, StandardCharsets.UTF_8), signedRight);

        synchronized (this) {
            requests.add(request);
        }

        Answer answer = signedRight ? answers.apply(request) : new Answer(401, "{\"errorCode\":\"user_directory.push_unauthorized\"}", 0);

        if (answer.delayMillis() > 0) {
            try {
                Thread.sleep(answer.delayMillis());
            } catch (InterruptedException ex) {
                Thread.currentThread().interrupt();
            }
        }

        byte[] response = answer.body().getBytes(StandardCharsets.UTF_8);
        exchange.getResponseHeaders().add("Content-Type", "application/json");
        exchange.sendResponseHeaders(answer.status(), response.length == 0 ? -1 : response.length);

        try (OutputStream out = exchange.getResponseBody()) {
            out.write(response);
        }
    }

    /** One request as the receiver saw it. */
    record Request(String method, String target, String body, boolean signedRight) {
    }

    /** What the receiver answers. */
    record Answer(int status, String body, long delayMillis) {

        static Answer ok(String body) {
            return new Answer(200, body, 0);
        }

        static Answer status(int status) {
            return new Answer(status, "{}", 0);
        }
    }
}
