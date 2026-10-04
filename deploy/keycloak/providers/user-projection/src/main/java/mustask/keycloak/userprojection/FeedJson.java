package mustask.keycloak.userprojection;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ArrayNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * The JSON of the push channel's bodies (api-contract.md B.3, B.4), with the
 * Jackson Keycloak ships. Property names camelCase; {@code deleted} written
 * only on a deletion.
 */
public final class FeedJson {

    private static final ObjectMapper MAPPER = new ObjectMapper();

    private FeedJson() {
    }

    /** B-1: {@code {"changes": [...]}}. */
    public static byte[] changes(List<UserEntry> entries) {
        return write(entriesBody("changes", entries));
    }

    /** B-3: {@code {"entries": [...]}}. */
    public static byte[] page(List<UserEntry> entries) {
        return write(entriesBody("entries", entries));
    }

    /** B-2: {@code {"enumerationStart": E}}. */
    public static byte[] begin(long enumerationStart) {
        ObjectNode body = MAPPER.createObjectNode();
        body.put("enumerationStart", enumerationStart);

        return write(body);
    }

    /** B-4: {@code {"pageCount": n, "entryCount": m}}. */
    public static byte[] completion(int pageCount, int entryCount) {
        ObjectNode body = MAPPER.createObjectNode();
        body.put("pageCount", pageCount);
        body.put("entryCount", entryCount);

        return write(body);
    }

    /** The numeric members of a counts answer (B.4); an unreadable body gives an empty map. */
    public static Map<String, Long> counts(String body) {
        Map<String, Long> counts = new LinkedHashMap<>();
        JsonNode root = read(body);

        if (root != null && root.isObject()) {
            for (Map.Entry<String, JsonNode> field : root.properties()) {
                if (field.getValue().isIntegralNumber()) {
                    counts.put(field.getKey(), field.getValue().asLong());
                }
            }
        }

        return counts;
    }

    /** The {@code errorCode} and the field reasons' codes of a problem answer — codes only, never values. */
    public static List<String> errorCodes(String body) {
        List<String> codes = new ArrayList<>();
        JsonNode root = read(body);

        if (root == null || !root.isObject()) {
            return codes;
        }

        if (root.path("errorCode").isTextual()) {
            codes.add(root.get("errorCode").asText());
        }

        JsonNode errors = root.path("errors");

        if (errors.isObject()) {
            for (Map.Entry<String, JsonNode> field : errors.properties()) {
                for (JsonNode reason : field.getValue()) {
                    if (reason.path("code").isTextual()) {
                        codes.add(field.getKey() + ": " + reason.get("code").asText());
                    }
                }
            }
        }

        return codes;
    }

    private static ObjectNode entriesBody(String name, List<UserEntry> entries) {
        ObjectNode body = MAPPER.createObjectNode();
        ArrayNode array = body.putArray(name);

        for (UserEntry entry : entries) {
            ObjectNode item = array.addObject();
            item.put("id", entry.id());
            item.put("moment", entry.moment());
            item.put("username", entry.username());
            item.put("firstName", entry.firstName());
            item.put("lastName", entry.lastName());
            item.put("email", entry.email());
            item.put("enabled", entry.enabled());

            if (entry.deleted()) {
                item.put("deleted", true);
            }
        }

        return body;
    }

    private static byte[] write(JsonNode body) {
        try {
            return MAPPER.writeValueAsBytes(body);
        } catch (JsonProcessingException ex) {
            throw new IllegalStateException("A push body could not be written", ex);
        }
    }

    private static JsonNode read(String body) {
        if (body == null || body.isBlank()) {
            return null;
        }

        try {
            return MAPPER.readTree(body);
        } catch (JsonProcessingException ex) {
            return null;
        }
    }
}
