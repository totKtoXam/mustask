package mustask.keycloak.userprojection;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertSame;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.Mockito.mock;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.io.IOException;
import java.nio.ByteBuffer;
import java.nio.charset.CharacterCodingException;
import java.nio.charset.CodingErrorAction;
import java.nio.charset.StandardCharsets;
import java.util.List;
import java.util.Random;
import java.util.function.Function;
import java.util.regex.Pattern;
import java.util.stream.Stream;
import org.junit.jupiter.api.Test;
import org.keycloak.events.Event;
import org.keycloak.events.EventType;
import org.keycloak.models.KeycloakSession;
import org.keycloak.models.KeycloakSessionFactory;
import org.keycloak.models.UserModel;

/**
 * QA re-check 3 (RC3-1, RC3-2, RC3-5, RC3-6, RC3-11): what the provider sends is well-formed text, whatever a user
 * puts in their own profile — against an oracle written independently of {@link ProjectedUserReader} (a walk by
 * code points), on all three paths that read a user (an event, a removal, a page of the reconciliation), and the
 * JSON that results is what MUSTASK's reader can take. The developer's tests pick nine shapes; this one draws
 * thousands from the alphabet of units that matter.
 */
class ProjectedTextQaTest {

    private static final ObjectMapper JSON = new ObjectMapper();

    private static final Pattern ESCAPED_SURROGATE = Pattern.compile("\\\\u[dD][89a-fA-F][0-9a-fA-F]{2}");

    /** A high surrogate escape immediately followed by a low one: the JSON form of one character beyond the basic plane. */
    private static final Pattern PAIR_OF_ESCAPES =
            Pattern.compile("\\\\u[dD][89abAB][0-9a-fA-F]{2}\\\\u[dD][c-fC-F][0-9a-fA-F]{2}");

    /**
     * The units a profile field can be made of that matter here: lone halves, a valid pair, NUL, noncharacters (U+FFFF
     * is the literal below; U+FFFE, U+FDD0, U+FDEF and the astral U+1FFFE and U+10FFFF follow, with the valid neighbours
     * U+FDCF, U+FDF0 and U+1FFFD), plain text.
     */
    private static final String[] ALPHABET = {
        "a", "Ы", " ", "\u0000", "\uD800", "\uDBFF", "\uDC00", "\uDFFF", "\uD83D", "\uDE00", "😀", "�", "\u0001", "￿",
        "𠀀", "￾", "﷐", "﷯", "🿾", "􏿿", "﷏", "ﷰ", "🿽",
    };

    private final KeycloakFixture keycloak = new KeycloakFixture();

    private final TransactionRunner inTheFixturesSession = new TransactionRunner() {
        @Override
        public <T> T call(KeycloakSessionFactory factory, Function<KeycloakSession, T> task) {
            return task.apply(keycloak.session);
        }
    };

    private final KeycloakRealmSource source =
            new KeycloakRealmSource(mock(KeycloakSessionFactory.class), keycloak.clock, inTheFixturesSession);

    /**
     * The expected text, by code points: a lone surrogate is U+FFFD, so is a noncharacter (D-5), U+0000 goes, a valid
     * pair and the rest stay. The noncharacter test is written differently from the reader's, on purpose.
     */
    private static String oracle(String text) {
        if (text == null) {
            return null;
        }

        StringBuilder expected = new StringBuilder();
        text.codePoints().forEach(codePoint -> {
            if (codePoint == 0) {
                return;
            }

            if ((codePoint >= 0xD800 && codePoint <= 0xDFFF) || isNoncharacterByDefinition(codePoint)) {
                expected.append('�');
            } else {
                expected.appendCodePoint(codePoint);
            }
        });

        return expected.toString();
    }

    /** U+FDD0..U+FDEF, and the last two code points of each of the 17 planes — the standard's definition, not the reader's mask. */
    private static boolean isNoncharacterByDefinition(int codePoint) {
        return (codePoint >= 0xFDD0 && codePoint <= 0xFDEF) || codePoint % 0x10000 >= 0xFFFE;
    }

    private static boolean hasNoncharacter(String text) {
        return text.codePoints().anyMatch(ProjectedTextQaTest::isNoncharacterByDefinition);
    }

    private static String randomText(Random random) {
        int length = random.nextInt(8);
        StringBuilder text = new StringBuilder();

        for (int i = 0; i < length; i++) {
            text.append(ALPHABET[random.nextInt(ALPHABET.length)]);
        }

        return text.toString();
    }

    private static String strictUtf8(byte[] body) throws CharacterCodingException {
        return StandardCharsets.UTF_8.newDecoder()
                .onMalformedInput(CodingErrorAction.REPORT)
                .onUnmappableCharacter(CodingErrorAction.REPORT)
                .decode(ByteBuffer.wrap(body))
                .toString();
    }

    private static boolean hasLoneSurrogateOrNul(String text) {
        for (int i = 0; i < text.length(); i++) {
            char c = text.charAt(i);

            if (c == 0) {
                return true;
            }

            if (Character.isHighSurrogate(c) && i + 1 < text.length() && Character.isLowSurrogate(text.charAt(i + 1))) {
                i++;
            } else if (Character.isSurrogate(c)) {
                return true;
            }
        }

        return false;
    }

    // RC3-1/RC3-2: 200 000 random texts against the oracle; the result is well-formed, free of U+0000 and a fixed point
    // of the cleaning (the same text every time it is read — so an event and a later reconciliation of the same user
    // never disagree).
    @Test
    void theCleaning_agreesWithAnIndependentOracle_onTwoHundredThousandRandomTexts() {
        Random random = new Random(20261004L);

        for (int round = 0; round < 200_000; round++) {
            String text = randomText(random);
            String cleaned = ProjectedUserReader.wellFormed(text);

            assertEquals(oracle(text), cleaned, "text " + escape(text));
            assertFalse(hasLoneSurrogateOrNul(cleaned), "left in: " + escape(cleaned));
            assertFalse(hasNoncharacter(cleaned), "a noncharacter left in: " + escape(cleaned));
            assertEquals(cleaned, ProjectedUserReader.wellFormed(cleaned), "not a fixed point: " + escape(cleaned));
        }
    }

    // RC3-1/RC3-2: through the reader itself — every one of the five fields (a null stays null) — for a few hundred
    // random profiles; the mocks are slow, the cleaning's own coverage is the test above.
    @Test
    void theReader_cleansEveryField_onRandomProfiles() {
        Random random = new Random(20261005L);

        for (int round = 0; round < 300; round++) {
            String id = "u-" + randomText(random);
            String username = randomText(random);
            String first = random.nextInt(5) == 0 ? null : randomText(random);
            String last = random.nextInt(5) == 0 ? null : randomText(random);
            String email = random.nextInt(5) == 0 ? null : randomText(random);

            UserState state = ProjectedUserReader.read(keycloak.user(id, username, first, last, email, true));

            assertEquals(oracle(id), state.id(), "id of " + escape(id));
            assertEquals(oracle(username), state.username(), "username of " + escape(username));
            assertEquals(oracle(first), state.firstName(), "first name of " + escape(first));
            assertEquals(oracle(last), state.lastName(), "last name of " + escape(last));
            assertEquals(oracle(email), state.email(), "e-mail of " + escape(email));

            for (String text : new String[] {state.id(), state.username(), state.firstName(), state.lastName(), state.email()}) {
                if (text == null) {
                    continue;
                }

                assertFalse(hasLoneSurrogateOrNul(text), "left in: " + escape(text));
                assertFalse(hasNoncharacter(text), "a noncharacter left in: " + escape(text));
                assertEquals(text, ProjectedUserReader.wellFormed(text), "not a fixed point: " + escape(text));
            }
        }
    }

    // RC3-1, RC3-4, RC3-5: the three paths clean alike, and the JSON each one produces is strict UTF-8, carries no
    // escaped surrogate and reads back, as a JSON parser reads it, as the text the oracle gives.
    @Test
    void theEventTheRemovalAndThePage_carryTheSameCleanText_inJsonMustaskCanRead() throws IOException {
        Random random = new Random(7L);

        for (int round = 0; round < 100; round++) {
            String first = randomText(random);
            String last = randomText(random);
            String email = randomText(random);
            String username = "x" + randomText(random);
            String id = "u-" + round;
            keycloak.user(id, username, first, last, email, true);

            keycloak.capture.capture(keycloak.session, keycloak.users.getUserById(keycloak.realm, id), false);
            keycloak.capture.capture(keycloak.session, keycloak.users.getUserById(keycloak.realm, id), true);
            keycloak.commit();
        }

        // One per user: the deletion supersedes the state queued before it (ADR-0025 § 4).
        List<UserEntry> queued = keycloak.pending.take(10_000);
        assertEquals(100, queued.size());
        assertTrue(queued.stream().allMatch(UserEntry::deleted));

        // The page path, for the same users.
        List<String> ids = Stream.iterate(0, i -> i + 1).limit(100).map(i -> "u-" + i).toList();
        List<UserEntry> page = source.read(ids);

        for (UserEntry fromPage : page) {
            UserEntry fromEvent = queued.stream().filter(entry -> entry.id().equals(fromPage.id())).findFirst().orElseThrow();

            assertEquals(fromEvent.username(), fromPage.username(), fromPage.id());
            assertEquals(fromEvent.firstName(), fromPage.firstName(), fromPage.id());
            assertEquals(fromEvent.lastName(), fromPage.lastName(), fromPage.id());
            assertEquals(fromEvent.email(), fromPage.email(), fromPage.id());
        }

        assertWireIsWellFormed(page, FeedJson.page(page), "entries");
        assertWireIsWellFormed(queued, FeedJson.changes(queued), "changes");
    }

    private static void assertWireIsWellFormed(List<UserEntry> sent, byte[] body, String arrayName) throws IOException {
        String text = strictUtf8(body);

        // Jackson writes a valid pair as two escapes (a finding of this re-check: not as 4 UTF-8 bytes). That is valid JSON
        // MUSTASK's reader joins into one character; what it refuses is an escaped half with no other half beside it.
        assertFalse(ESCAPED_SURROGATE.matcher(PAIR_OF_ESCAPES.matcher(text).replaceAll("")).find(),
                "an escaped surrogate with no partner in the body of " + sent.size() + " entries");
        assertFalse(text.contains("\\u0000"), "a U+0000 escape in the body");

        JsonNode entries = JSON.readTree(body).get(arrayName);
        assertEquals(sent.size(), entries.size());

        for (int i = 0; i < sent.size(); i++) {
            UserEntry entry = sent.get(i);
            JsonNode node = entries.get(i);

            assertEquals(entry.id(), node.get("id").asText());
            assertEquals(entry.username(), node.get("username").asText());

            if (entry.firstName() == null) {
                assertTrue(node.get("firstName").isNull());
            } else {
                assertEquals(entry.firstName(), node.get("firstName").asText());
            }
        }
    }

    // RC3-6: valid pairs are not touched on any path — an emoji, a character of the supplementary ideographic plane,
    // a flag (two regional indicators), a pair beside a lone half — and they travel as raw UTF-8, not as escapes.
    @Test
    void aValidPair_isKeptUnchanged_onEveryPath_andTravelsAsUtf8() throws IOException {
        String emoji = "Ann 😀";
        String ideograph = "𠀀𠀁";
        String flag = "🇰🇿";
        keycloak.user("u-emoji", "anna", emoji, ideograph, "anna@example.test", true);
        keycloak.user("u-flag", "flag" + flag, flag, "😀\uD800", "\uD800😀", true);
        keycloak.user("u-split", "split", "\uD83D😀", "\uDE00\uD83D", null, true);

        keycloak.capture.capture(keycloak.session, keycloak.users.getUserById(keycloak.realm, "u-emoji"), false);
        keycloak.capture.capture(keycloak.session, keycloak.users.getUserById(keycloak.realm, "u-flag"), true);
        keycloak.commit();
        List<UserEntry> queued = keycloak.pending.take(10);
        List<UserEntry> page = source.read(List.of("u-emoji", "u-flag", "u-split"));

        UserEntry viaEvent = queued.stream().filter(entry -> entry.id().equals("u-emoji")).findFirst().orElseThrow();
        UserEntry viaRemoval = queued.stream().filter(entry -> entry.id().equals("u-flag")).findFirst().orElseThrow();

        assertEquals(emoji, viaEvent.firstName());
        assertEquals(ideograph, viaEvent.lastName());
        assertEquals(emoji, page.get(0).firstName());
        assertEquals(ideograph, page.get(0).lastName());

        assertTrue(viaRemoval.deleted());
        assertEquals("flag" + flag, viaRemoval.username());
        assertEquals(flag, viaRemoval.firstName());
        assertEquals("😀�", viaRemoval.lastName(), "a pair, then a lone half");
        assertEquals("�😀", viaRemoval.email(), "a lone half, then a pair");

        assertEquals("�😀", page.get(2).firstName(), "a high half before a pair: only the half goes");
        assertEquals("��", page.get(2).lastName(), "a reversed pair is two lone halves");

        // On the wire: strict UTF-8, only whole pairs escaped, and a JSON parser reads back exactly the text sent.
        assertWireIsWellFormed(page, FeedJson.page(page), "entries");
        assertEquals(emoji, JSON.readTree(FeedJson.page(page)).get("entries").get(0).get("firstName").asText());
    }

    // A text that needs no change is the same instance (no copy per read of a clean realm), and a null stays null.
    @Test
    void cleanText_isReturnedAsTheSameInstance_andNullStaysNull() {
        String clean = new String("Анна 😀 Смирнова");

        assertSame(clean, ProjectedUserReader.wellFormed(clean));
        assertNull(ProjectedUserReader.wellFormed(null));
        assertEquals("", ProjectedUserReader.wellFormed(""));
    }

    // RC3-11: the event listener's own-profile path, end to end through the listener class and Jackson.
    @Test
    void anOwnProfileUpdate_throughTheListener_isQueuedAndSerialisedClean() throws IOException {
        UserProjectionListener listener = new UserProjectionListener(keycloak.session, keycloak.capture);
        keycloak.user("u-1", "a.smirnova", "Сам\uD800", "Смир\u0000нов\uDC00", "anna@example.test", true);
        Event event = new Event();
        event.setType(EventType.UPDATE_PROFILE);
        event.setUserId("u-1");
        event.setRealmId(KeycloakFixture.REALM_ID);

        listener.onEvent(event);
        keycloak.commit();

        List<UserEntry> queued = keycloak.pending.take(10);
        assertEquals(1, queued.size());
        assertEquals("Сам�", queued.get(0).firstName());
        assertEquals("Смирнов�", queued.get(0).lastName());
        assertWireIsWellFormed(queued, FeedJson.changes(queued), "changes");
    }

    private static String escape(String text) {
        if (text == null) {
            return "null";
        }

        StringBuilder out = new StringBuilder();

        for (char c : text.toCharArray()) {
            out.append(c < 0x20 || (c >= 0xD800 && c <= 0xDFFF) || c == 0xFFFD || c == 0xFFFF ? String.format("\\u%04X", (int) c) : String.valueOf(c));
        }

        return out.toString();
    }
}
