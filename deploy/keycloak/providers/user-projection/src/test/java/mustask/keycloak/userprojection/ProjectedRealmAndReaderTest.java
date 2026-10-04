package mustask.keycloak.userprojection;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertSame;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.Mockito.verify;

import java.nio.ByteBuffer;
import java.nio.charset.CharacterCodingException;
import java.nio.charset.CodingErrorAction;
import java.nio.charset.StandardCharsets;
import java.util.List;
import java.util.regex.Pattern;
import java.util.stream.Stream;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.Arguments;
import org.junit.jupiter.params.provider.MethodSource;
import org.keycloak.models.UserModel;

/** BR-UP-2, BR-UP-13: realm {@code mustask} only; the projected fields only; no service account. */
class ProjectedRealmAndReaderTest {

    private final KeycloakFixture keycloak = new KeycloakFixture();

    @Test
    void theOnlyRealmLookedUp_isMustask() {
        assertSame(keycloak.realm, ProjectedRealm.find(keycloak.session));
        verify(keycloak.realms).getRealmByName("mustask");
    }

    @Test
    void isProjected_comparesTheEventsRealmIdWithMustasks() {
        assertTrue(ProjectedRealm.isProjected(keycloak.session, KeycloakFixture.REALM_ID));
        assertFalse(ProjectedRealm.isProjected(keycloak.session, KeycloakFixture.OTHER_REALM_ID));
        assertFalse(ProjectedRealm.isProjected(keycloak.session, null));
    }

    // BR-UP-2: id, username, first and last name, e-mail, enabled — nothing else is read.
    @Test
    void theReader_takesExactlyTheProjectedFields() {
        UserModel user = keycloak.user("u-1", "a.smirnova", "Анна", "Смирнова", "anna@example.test", false);

        assertEquals(new UserState("u-1", "a.smirnova", "Анна", "Смирнова", "anna@example.test", false), ProjectedUserReader.read(user));
    }

    // Security pass S1: Keycloak accepts a lone UTF-16 surrogate in a name (account REST, 204), and MUSTASK's reader
    // refuses the escape it becomes in JSON (400) — so every projected string is made well-formed here, the one place
    // the event, removal and reconciliation paths share: a lone surrogate becomes U+FFFD, U+0000 goes.
    static Stream<Arguments> malformedStrings() {
        return Stream.of(
                Arguments.of("\uD800", "�"),
                Arguments.of("\uDC00", "�"),
                Arguments.of("Сам\uD800", "Сам�"),
                Arguments.of("\uDBFFСам", "�Сам"),
                Arguments.of("a\uDC00\uD800b", "a��b"),
                Arguments.of("\uD800𐀀", "�𐀀"),
                Arguments.of("a\u0000b", "ab"),
                Arguments.of("\u0000", ""),
                Arguments.of("\u0000\uD800\u0000", "�"),
                // D-5: a noncharacter becomes U+FFFD too — MUSTASK refuses it (400) and its normaliser threw on U+FFFE.
                Arguments.of("￾", "�"),
                Arguments.of("Сам￾", "Сам�"),
                Arguments.of("￿Сам", "�Сам"),
                Arguments.of("a﷐b﷯c", "a�b�c"),
                Arguments.of("🿾", "�"),
                Arguments.of("😀￾😀", "😀�😀"),
                Arguments.of("􏿿x􏿾", "�x�"),
                Arguments.of("\u0000￾\uD800", "��"));
    }

    @ParameterizedTest
    @MethodSource("malformedStrings")
    void everyProjectedString_isMadeWellFormed_inEachOfTheFourFields(String malformed, String expected) {
        UserModel username = keycloak.user("u-1", malformed, "Анна", "Смирнова", "anna@example.test", true);
        UserModel firstName = keycloak.user("u-2", "anna", malformed, "Смирнова", "anna@example.test", true);
        UserModel lastName = keycloak.user("u-3", "anna", "Анна", malformed, "anna@example.test", true);
        UserModel email = keycloak.user("u-4", "anna", "Анна", "Смирнова", malformed, true);

        assertEquals(new UserState("u-1", expected, "Анна", "Смирнова", "anna@example.test", true), ProjectedUserReader.read(username));
        assertEquals(new UserState("u-2", "anna", expected, "Смирнова", "anna@example.test", true), ProjectedUserReader.read(firstName));
        assertEquals(new UserState("u-3", "anna", "Анна", expected, "anna@example.test", true), ProjectedUserReader.read(lastName));
        assertEquals(new UserState("u-4", "anna", "Анна", "Смирнова", expected, true), ProjectedUserReader.read(email));
    }

    @Test
    void theId_isMadeWellFormedToo() {
        assertEquals("u-�", ProjectedUserReader.read(keycloak.user("u-\uD800", "anna", null, null, null, true)).id());
        assertEquals("u-�", ProjectedUserReader.read(keycloak.user("u-￾", "anna", null, null, null, true)).id());
    }

    // D-5: the characters beside the noncharacters are ordinary, and a valid pair — an emoji, an ideograph, the
    // last assigned-or-private code points of a plane — is never touched, in each of the four fields.
    static Stream<Arguments> validNeighboursOfNoncharacters() {
        return Stream.of(
                Arguments.of("﷏"),
                Arguments.of("ﷰ"),
                Arguments.of("�"),
                Arguments.of("𐀀"),
                Arguments.of("🿽"),
                Arguments.of("􏿽"),
                Arguments.of("󰀀"),
                Arguments.of("😀"),
                Arguments.of("𠀀"));
    }

    @ParameterizedTest
    @MethodSource("validNeighboursOfNoncharacters")
    void aValidCharacterNextToTheNoncharacters_isKept_inEachOfTheFourFields(String valid) {
        String text = "a" + valid + "b";

        assertEquals(new UserState("u-1", text, "Анна", "Смирнова", "anna@example.test", true),
                ProjectedUserReader.read(keycloak.user("u-1", text, "Анна", "Смирнова", "anna@example.test", true)));
        assertEquals(new UserState("u-2", "anna", text, "Смирнова", "anna@example.test", true),
                ProjectedUserReader.read(keycloak.user("u-2", "anna", text, "Смирнова", "anna@example.test", true)));
        assertEquals(new UserState("u-3", "anna", "Анна", text, "anna@example.test", true),
                ProjectedUserReader.read(keycloak.user("u-3", "anna", "Анна", text, "anna@example.test", true)));
        assertEquals(new UserState("u-4", "anna", "Анна", "Смирнова", text, true),
                ProjectedUserReader.read(keycloak.user("u-4", "anna", "Анна", "Смирнова", text, true)));
        assertSame(text, ProjectedUserReader.wellFormed(text));
    }

    // D-5, exhaustively: every code point, alone between two letters. A lone surrogate and a noncharacter become U+FFFD,
    // U+0000 goes, every other one stays — the expectation is written from the standard's definition of a
    // noncharacter, not from the reader's.
    @Test
    void everyCodePoint_isCleanedAsDocumented() {
        int noncharacters = 0;

        for (int codePoint = 0; codePoint <= Character.MAX_CODE_POINT; codePoint++) {
            final int at = codePoint;
            String text = "a" + new String(Character.toChars(codePoint)) + "b";
            boolean loneSurrogate = codePoint >= 0xD800 && codePoint <= 0xDFFF;
            boolean nonCharacter = (codePoint >= 0xFDD0 && codePoint <= 0xFDEF) || codePoint % 0x10000 >= 0xFFFE;
            String expected;

            if (codePoint == 0) {
                expected = "ab";
            } else if (loneSurrogate || nonCharacter) {
                expected = "a�b";
            } else {
                expected = text;
            }

            assertEquals(expected, ProjectedUserReader.wellFormed(text), () -> String.format("U+%04X", at));
            assertEquals(nonCharacter, ProjectedUserReader.isNoncharacter(codePoint), () -> String.format("U+%04X", at));

            if (nonCharacter) {
                noncharacters++;
            }
        }

        assertEquals(66, noncharacters);
    }

    // D-5, on the wire: the body of a user with noncharacters in the name and the e-mail is valid UTF-8 and holds
    // none of them — only U+FFFD where they were — so MUSTASK's validation never refuses what the provider sent.
    @Test
    void theBodyOfAUserWithNoncharacters_carriesOnlyTheReplacement() throws CharacterCodingException {
        UserModel user = keycloak.user("u-1", "anna", "Сам￾", "﷐Смир🿿нов", "a￿@example.test", true);

        byte[] body = FeedJson.changes(List.of(ProjectedUserReader.read(user).at(42, false)));
        String text = StandardCharsets.UTF_8.newDecoder()
                .onMalformedInput(CodingErrorAction.REPORT)
                .onUnmappableCharacter(CodingErrorAction.REPORT)
                .decode(ByteBuffer.wrap(body))
                .toString();

        assertTrue(text.codePoints().noneMatch(codePoint -> ProjectedUserReader.isNoncharacter(codePoint)), text);
        assertFalse(Pattern.compile("\\\\u[dD][89a-fA-F]").matcher(text).find(), text);
        assertTrue(text.contains("Сам�"), text);
        assertTrue(text.contains("�Смир�нов"), text);
        assertTrue(text.contains("a�@example.test"), text);
    }

    @Test
    void wellFormedText_isKeptAsItIs_aPairOfSurrogatesAndAnAbsentValueIncluded() {
        UserModel user = keycloak.user("u-1", "anna", "Анна 😀 «Ё»", null, "anna@example.test", true);

        UserState state = ProjectedUserReader.read(user);

        assertEquals("Анна 😀 «Ё»", state.firstName());
        assertNull(state.lastName());
    }

    // The effect that matters: the push body of such a user is valid UTF-8 and carries no escaped lone surrogate,
    // which MUSTASK's System.Text.Json refuses (a 400 that drops the batch and stops every reconciliation).
    @Test
    void theBodyOfAUserWithLoneSurrogates_hasNoEscapedSurrogate_andIsValidUtf8() throws CharacterCodingException {
        UserModel user = keycloak.user("u-1", "\uD800sam", "Сам\uD800", "\uDC00", "a\uD800@example.test", true);

        byte[] body = FeedJson.changes(List.of(ProjectedUserReader.read(user).at(42, false)));
        String text = StandardCharsets.UTF_8.newDecoder()
                .onMalformedInput(CodingErrorAction.REPORT)
                .onUnmappableCharacter(CodingErrorAction.REPORT)
                .decode(ByteBuffer.wrap(body))
                .toString();

        assertFalse(Pattern.compile("\\\\u[dD][89a-fA-F]").matcher(text).find(), text);
        assertTrue(text.contains("Сам�"), text);
    }

    // US-2 "A service-account user is never projected".
    @Test
    void aServiceAccount_isRecognisedByItsClientLink() {
        assertTrue(ProjectedUserReader.isServiceAccount(keycloak.serviceAccount("sa-1")));
        assertFalse(ProjectedUserReader.isServiceAccount(keycloak.user("u-1", "anna", null, null, null, true)));
    }

    @Test
    void aDeletion_carriesTheLastStateAndIsNeverEnabled() {
        UserEntry entry = new UserState("u-1", "anna", "Анна", null, null, true).at(42, true);

        assertTrue(entry.deleted());
        assertFalse(entry.enabled());
        assertEquals(42, entry.moment());
    }
}
