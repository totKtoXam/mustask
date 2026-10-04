package mustask.keycloak.userprojection;

import org.keycloak.models.UserModel;

/**
 * Reads a user's projected fields (BR-UP-2) — and nothing else of the user:
 * no attributes, credentials, roles or groups (BR-UP-13; ReadOnlyGuardTest).
 *
 * <p>Every text it hands on is well-formed (see {@link #wellFormed}): Keycloak
 * accepts what MUSTASK's JSON reader cannot take and its validation refuses
 * (api-contract.md B.3: "the provider never sends what MUSTASK refuses"), so
 * the one place the event, the
 * removal and the reconciliation paths share cleans it before it is queued.
 */
public final class ProjectedUserReader {

    /** What an unpaired UTF-16 surrogate or a Unicode noncharacter becomes. */
    static final char REPLACEMENT = '�';

    private ProjectedUserReader() {
    }

    /** A service account's user is never projected (BR-UP-2). */
    public static boolean isServiceAccount(UserModel user) {
        return user.getServiceAccountClientLink() != null;
    }

    public static UserState read(UserModel user) {
        return new UserState(
                wellFormed(user.getId()),
                wellFormed(user.getUsername()),
                wellFormed(user.getFirstName()),
                wellFormed(user.getLastName()),
                wellFormed(user.getEmail()),
                user.isEnabled());
    }

    /**
     * {@code text} as MUSTASK can take it: an unpaired UTF-16 surrogate becomes U+FFFD (it has no UTF-8 form, and
     * its JSON escape is refused by MUSTASK's reader with a 400 — a user who puts one in their own name through
     * Keycloak's account REST would otherwise make every batch and every reconciliation page carrying them fail),
     * a Unicode noncharacter becomes U+FFFD too (MUSTASK refuses it as {@code value_invalid}, a 400 that drops the
     * batch, and its normaliser threw on U+FFFE — the last name, first name or e-mail of any user can hold one,
     * Keycloak accepts it), and U+0000 is dropped (PostgreSQL text cannot hold it). A pair of surrogates that is a
     * valid character beyond the basic plane — an emoji, an ideograph — and {@code null} are kept as they are; so is
     * any text that needs no change, as the same instance.
     */
    static String wellFormed(String text) {
        if (text == null) {
            return null;
        }

        StringBuilder clean = null;

        for (int i = 0; i < text.length(); i++) {
            char c = text.charAt(i);

            if (Character.isHighSurrogate(c) && i + 1 < text.length() && Character.isLowSurrogate(text.charAt(i + 1))) {
                char low = text.charAt(i + 1);

                if (isNoncharacter(Character.toCodePoint(c, low))) {
                    clean = startedAt(clean, text, i).append(REPLACEMENT);
                } else if (clean != null) {
                    clean.append(c).append(low);
                }

                i++;
            } else if (c == '\0') {
                clean = startedAt(clean, text, i);
            } else if (Character.isSurrogate(c) || isNoncharacter(c)) {
                clean = startedAt(clean, text, i).append(REPLACEMENT);
            } else if (clean != null) {
                clean.append(c);
            }
        }

        return clean == null ? text : clean.toString();
    }

    /**
     * A Unicode noncharacter: U+FDD0..U+FDEF and the last two code points of every plane (U+FFFE, U+FFFF,
     * U+1FFFE, … U+10FFFF) — sixty-six values the standard reserves for a program's internal use. MUSTASK's
     * {@code DirectorySearchText.IsNoncharacter} is the same set.
     */
    static boolean isNoncharacter(int codePoint) {
        return (codePoint >= 0xFDD0 && codePoint <= 0xFDEF) || (codePoint & 0xFFFE) == 0xFFFE;
    }

    /** The builder holding {@code text} up to {@code index} — created at the first change, so clean text is not copied. */
    private static StringBuilder startedAt(StringBuilder clean, String text, int index) {
        return clean != null ? clean : new StringBuilder(text.length()).append(text, 0, index);
    }
}
