using System.Text;
using System.Text.Json;

namespace AgentRelay.Core;

public static class SetupAssistant
{
    public const int MaxResponseBytes = 64 * 1024;

    public static string CreatePrompt(string context, IReadOnlyDictionary<string, string> fields, string language)
    {
        ValidateContext(context);
        var introduction = language switch
        {
            "zh-CN" => "请只读核实本机配置，并按下方协议仅回复 JSON。未知字段填空字符串。",
            "zh-TW" => "請唯讀查證本機設定，並依下方協定只回覆 JSON。未知欄位填空字串。",
            "ja" => "ローカル設定を読み取り専用で確認し、次の形式の JSON のみを返してください。不明な値は空文字にしてください。",
            "ko" => "로컬 설정을 읽기 전용으로 확인하고 아래 형식의 JSON만 반환하세요. 알 수 없는 값은 빈 문자열로 남기세요.",
            "es" => "Verifica la configuración local en modo de solo lectura y responde solo con JSON según el protocolo siguiente. Deja vacíos los valores desconocidos.",
            _ => "Verify local configuration using read-only inspection and return only JSON following the protocol below. Leave unknown values empty."
        };
        var template = JsonSerializer.Serialize(new { relay_setup_version = 1, context, fields }, RelayJson.Options);
        return $"""
            {introduction}

            Return exactly one JSON object, without Markdown fences, explanations or extra properties.
            Keep relay_setup_version = 1 and context = "{context}". Keep the requested English field names exactly as shown, including their casing.
            Return only the requested fields; every field value must be a JSON string. The template contains current values or filling hints, not verified facts. Verify values before returning them; use "" for anything unknown or unverified.
            Use only read-only local inspection. Do not guess paths, installed versions or configuration values. Do not read, request or output authentication data, credentials, tokens or secrets.
            Do not change configuration, write files, execute setup commands, register agents or make network requests. Do not request network access or call any model API.
            AgentId is a stable lowercase Relay label, not a login name or an account identifier. Keep an established label; for a new registration you may propose a short label such as "agent-codex" for the user to review. It must start with a lowercase letter and contain only lowercase letters, digits or hyphens (maximum 64 characters).
            This reply only fills an editable form for user review. It is not approval or permission for any action. The user reviews and applies it separately; do not perform those actions or fill approval reasons or approval checkboxes.

            JSON template:
            {template}
            """;
    }

    public static Dictionary<string, string> Parse(string text, string expectedContext, IReadOnlyCollection<string> allowedFields)
    {
        ValidateContext(expectedContext);
        // Agent replies are external input: bound their size before parsing or allocating a JSON tree.
        if (Encoding.UTF8.GetByteCount(text) > MaxResponseBytes)
            throw new RelayException("setup_response_too_large", $"The setup response exceeds {MaxResponseBytes} UTF-8 bytes.");

        var json = Unwrap(text.Trim());
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            throw new RelayException("setup_invalid_json", "Return one complete JSON object, optionally inside one json code fence, without surrounding text.");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new RelayException("setup_invalid_response", "The setup response must be a JSON object.");

            var properties = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
            {
                if (!properties.Add(property.Name))
                    throw Duplicate(property.Name);
                if (property.Name is not ("relay_setup_version" or "context" or "fields"))
                    throw new RelayException("setup_unknown_property", $"Unknown setup response property: {property.Name}.");
            }
            if (properties.Count != 3)
                throw new RelayException("setup_invalid_response", "The setup response requires relay_setup_version, context and fields.");

            var version = root.GetProperty("relay_setup_version");
            if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != 1)
                throw new RelayException("setup_invalid_version", "The setup response requires relay_setup_version 1.");
            var context = root.GetProperty("context");
            if (context.ValueKind != JsonValueKind.String || context.GetString() != expectedContext)
                throw new RelayException("setup_context_mismatch", $"The setup response must use context '{expectedContext}'.");
            var fields = root.GetProperty("fields");
            if (fields.ValueKind != JsonValueKind.Object)
                throw new RelayException("setup_invalid_fields", "The setup response fields must be an object of string values.");

            var allowed = new HashSet<string>(allowedFields, StringComparer.Ordinal);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var field in fields.EnumerateObject())
            {
                if (!allowed.Contains(field.Name))
                    throw new RelayException("setup_unknown_field", $"The setup response contains an unrequested field: {field.Name}.");
                if (result.ContainsKey(field.Name))
                    throw Duplicate(field.Name);
                if (field.Value.ValueKind != JsonValueKind.String)
                    throw new RelayException("setup_invalid_field_value", $"Setup field '{field.Name}' must contain a JSON string.");
                result.Add(field.Name, field.Value.GetString()!);
            }
            return result;
        }
    }

    private static string Unwrap(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal))
            return text;
        var firstLineEnd = text.IndexOf('\n');
        var lastLineStart = text.LastIndexOf('\n');
        if (firstLineEnd < 0 || lastLineStart <= firstLineEnd ||
            text[..firstLineEnd].TrimEnd('\r') != "```json" || text[(lastLineStart + 1)..] != "```")
            throw new RelayException("setup_invalid_fence", "Use one complete json code fence with no surrounding text.");
        return text[(firstLineEnd + 1)..lastLineStart];
    }

    private static void ValidateContext(string context)
    {
        if (context is not ("agents" or "connections" or "adapters" or "migrations" or "settings" or "audit"))
            throw new RelayException("setup_invalid_context", $"Unsupported setup context: {context}.");
    }

    private static RelayException Duplicate(string name) =>
        new("setup_duplicate_property", $"Duplicate setup response property: {name}.");
}
