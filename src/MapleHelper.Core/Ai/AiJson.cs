using System.Text.Json;
using System.Text.Json.Nodes;

namespace MapleHelper.Core.Ai;

/// <summary>
/// Model hay bọc JSON trong ```json ... ``` hoặc thêm câu dẫn. Lấy phần JSON đầu tiên trong text.
/// </summary>
public static class AiJson
{
    public static JsonNode? TryParse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var start = text.IndexOfAny(['{', '[']);
        if (start < 0)
        {
            return null;
        }
        var close = text[start] == '{' ? '}' : ']';
        var end = text.LastIndexOf(close);
        if (end <= start)
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(text[start..(end + 1)]);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? GetString(this JsonNode? node, string property)
    {
        var value = node?[property];
        return value is JsonValue v && v.TryGetValue<string>(out var s) ? s : value?.ToString();
    }
}
