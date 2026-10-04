package mustask.keycloak.userprojection;

import java.util.ArrayList;
import java.util.List;
import java.util.logging.Handler;
import java.util.logging.Level;
import java.util.logging.LogRecord;
import java.util.logging.Logger;

/**
 * Captures what the provider logs (JBoss Logging writes to JUL in these tests;
 * see the surefire configuration in pom.xml).
 */
final class LogCapture extends Handler implements AutoCloseable {

    /** JBoss Logging's ERROR on JUL. */
    static final int ERROR = 1000;

    /** JBoss Logging's WARN on JUL. */
    static final int WARN = 900;

    /** JBoss Logging's INFO on JUL. */
    static final int INFO = 800;

    private final List<Logger> loggers = new ArrayList<>();

    private final List<LogRecord> records = new ArrayList<>();

    private LogCapture() {
    }

    static LogCapture of(Class<?>... sources) {
        LogCapture capture = new LogCapture();

        for (Class<?> source : sources) {
            Logger logger = Logger.getLogger(source.getName());
            logger.setLevel(Level.ALL);
            logger.addHandler(capture);
            capture.loggers.add(logger);
        }

        return capture;
    }

    @Override
    public synchronized void publish(LogRecord record) {
        records.add(record);
    }

    @Override
    public void flush() {
        // Nothing buffered.
    }

    @Override
    public void close() {
        for (Logger logger : loggers) {
            logger.removeHandler(this);
        }
    }

    synchronized String text() {
        StringBuilder text = new StringBuilder();

        for (LogRecord record : records) {
            text.append(record.getLevel().intValue()).append(' ').append(record.getMessage()).append('\n');
        }

        return text.toString();
    }

    synchronized List<String> messagesAtLeast(int level) {
        List<String> messages = new ArrayList<>();

        for (LogRecord record : records) {
            if (record.getLevel().intValue() >= level) {
                messages.add(record.getMessage());
            }
        }

        return messages;
    }
}
