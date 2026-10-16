using System.Text.Json;

namespace AasxEditor.Components.Pages;

public partial class Home
{
    // ===== JSON Manipulation =====

    /// <summary>
    /// SubmodelElementList에 새 Property 항목 추가
    /// </summary>
    private static string? AddSmlItem(string json, string smlPath, string value)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            using var ms = new MemoryStream();
            using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
            {
                WriteSmlWithNewItem(w, doc.RootElement, smlPath, value, "");
            }
            return System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }
        catch { return null; }
    }

    /// <summary>
    /// SubmodelElementList의 특정 인덱스 항목 값 수정
    /// </summary>
    private static string? UpdateSmlItem(string json, string smlPath, int index, string newValue)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            using var ms = new MemoryStream();
            using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
            {
                WriteSmlWithUpdatedItem(w, doc.RootElement, smlPath, index, newValue, "");
            }
            return System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }
        catch { return null; }
    }

    /// <summary>
    /// SubmodelElementList의 특정 인덱스 항목 제거
    /// </summary>
    private static string? RemoveSmlItem(string json, string smlPath, int index)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            using var ms = new MemoryStream();
            using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
            {
                WriteSmlWithRemovedItem(w, doc.RootElement, smlPath, index, "");
            }
            return System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }
        catch { return null; }
    }

    /// <summary>
    /// SubmodelElementList에 여러 Property 항목 일괄 추가
    /// </summary>
    private static string? AddSmlItems(string json, string smlPath, List<string> values)
    {
        try
        {
            var result = json;
            foreach (var v in values)
            {
                result = AddSmlItem(result, smlPath, v);
                if (result is null) return null;
            }
            return result;
        }
        catch { return null; }
    }

    /// <summary>
    /// SubmodelElementList의 기존 항목을 모두 제거하고 새 항목들로 교체
    /// </summary>
    private static string? ReplaceSmlItems(string json, string smlPath, List<string> values)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            using var ms = new MemoryStream();
            using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
            {
                WriteSmlWithReplacedItems(w, doc.RootElement, smlPath, values, "");
            }
            return System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }
        catch { return null; }
    }

    // --- JSON writers ---

    private static void WriteSmlWithNewItem(Utf8JsonWriter w, JsonElement elem, string targetPath, string value, string currentPath)
    {
        switch (elem.ValueKind)
        {
            case JsonValueKind.Object:
                w.WriteStartObject();
                foreach (var prop in elem.EnumerateObject())
                {
                    var pp = string.IsNullOrEmpty(currentPath) ? prop.Name : $"{currentPath}.{prop.Name}";
                    w.WritePropertyName(prop.Name);

                    if (pp == $"{targetPath}.value" && prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        w.WriteStartArray();
                        foreach (var item in prop.Value.EnumerateArray())
                            item.WriteTo(w);

                        w.WriteStartObject();
                        w.WriteString("modelType", "Property");
                        w.WriteString("valueType", "xs:string");
                        w.WriteString("value", value);
                        w.WriteEndObject();

                        w.WriteEndArray();
                    }
                    else
                    {
                        WriteSmlWithNewItem(w, prop.Value, targetPath, value, pp);
                    }
                }
                if (currentPath == targetPath && !elem.EnumerateObject().Any(p => p.Name == "value"))
                {
                    w.WritePropertyName("value");
                    w.WriteStartArray();
                    w.WriteStartObject();
                    w.WriteString("modelType", "Property");
                    w.WriteString("valueType", "xs:string");
                    w.WriteString("value", value);
                    w.WriteEndObject();
                    w.WriteEndArray();
                }
                w.WriteEndObject();
                break;

            case JsonValueKind.Array:
                w.WriteStartArray();
                var idx = 0;
                foreach (var item in elem.EnumerateArray())
                {
                    WriteSmlWithNewItem(w, item, targetPath, value, $"{currentPath}[{idx}]");
                    idx++;
                }
                w.WriteEndArray();
                break;

            default:
                elem.WriteTo(w);
                break;
        }
    }

    private static void WriteSmlWithUpdatedItem(Utf8JsonWriter w, JsonElement elem, string targetPath, int targetIndex, string newValue, string currentPath)
    {
        switch (elem.ValueKind)
        {
            case JsonValueKind.Object:
                w.WriteStartObject();
                foreach (var prop in elem.EnumerateObject())
                {
                    var pp = string.IsNullOrEmpty(currentPath) ? prop.Name : $"{currentPath}.{prop.Name}";
                    w.WritePropertyName(prop.Name);

                    if (pp == $"{targetPath}.value" && prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        w.WriteStartArray();
                        var i = 0;
                        foreach (var item in prop.Value.EnumerateArray())
                        {
                            if (i == targetIndex)
                            {
                                w.WriteStartObject();
                                foreach (var ip in item.EnumerateObject())
                                {
                                    w.WritePropertyName(ip.Name);
                                    if (ip.Name == "value")
                                        w.WriteStringValue(newValue);
                                    else
                                        ip.Value.WriteTo(w);
                                }
                                w.WriteEndObject();
                            }
                            else
                            {
                                item.WriteTo(w);
                            }
                            i++;
                        }
                        w.WriteEndArray();
                    }
                    else
                    {
                        WriteSmlWithUpdatedItem(w, prop.Value, targetPath, targetIndex, newValue, pp);
                    }
                }
                w.WriteEndObject();
                break;

            case JsonValueKind.Array:
                w.WriteStartArray();
                var idx = 0;
                foreach (var item in elem.EnumerateArray())
                {
                    WriteSmlWithUpdatedItem(w, item, targetPath, targetIndex, newValue, $"{currentPath}[{idx}]");
                    idx++;
                }
                w.WriteEndArray();
                break;

            default:
                elem.WriteTo(w);
                break;
        }
    }

    private static void WriteSmlWithRemovedItem(Utf8JsonWriter w, JsonElement elem, string targetPath, int targetIndex, string currentPath)
    {
        switch (elem.ValueKind)
        {
            case JsonValueKind.Object:
                w.WriteStartObject();
                foreach (var prop in elem.EnumerateObject())
                {
                    var pp = string.IsNullOrEmpty(currentPath) ? prop.Name : $"{currentPath}.{prop.Name}";
                    w.WritePropertyName(prop.Name);

                    if (pp == $"{targetPath}.value" && prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        w.WriteStartArray();
                        var i = 0;
                        foreach (var item in prop.Value.EnumerateArray())
                        {
                            if (i != targetIndex)
                                item.WriteTo(w);
                            i++;
                        }
                        w.WriteEndArray();
                    }
                    else
                    {
                        WriteSmlWithRemovedItem(w, prop.Value, targetPath, targetIndex, pp);
                    }
                }
                w.WriteEndObject();
                break;

            case JsonValueKind.Array:
                w.WriteStartArray();
                var idx = 0;
                foreach (var item in elem.EnumerateArray())
                {
                    WriteSmlWithRemovedItem(w, item, targetPath, targetIndex, $"{currentPath}[{idx}]");
                    idx++;
                }
                w.WriteEndArray();
                break;

            default:
                elem.WriteTo(w);
                break;
        }
    }

    private static void WriteSmlWithReplacedItems(Utf8JsonWriter w, JsonElement elem, string targetPath, List<string> values, string currentPath)
    {
        switch (elem.ValueKind)
        {
            case JsonValueKind.Object:
                w.WriteStartObject();
                var hasValue = false;
                foreach (var prop in elem.EnumerateObject())
                {
                    var pp = string.IsNullOrEmpty(currentPath) ? prop.Name : $"{currentPath}.{prop.Name}";
                    w.WritePropertyName(prop.Name);

                    if (pp == $"{targetPath}.value" && prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        hasValue = true;
                        w.WriteStartArray();
                        foreach (var v in values)
                        {
                            w.WriteStartObject();
                            w.WriteString("modelType", "Property");
                            w.WriteString("valueType", "xs:string");
                            w.WriteString("value", v);
                            w.WriteEndObject();
                        }
                        w.WriteEndArray();
                    }
                    else
                    {
                        WriteSmlWithReplacedItems(w, prop.Value, targetPath, values, pp);
                    }
                }
                if (currentPath == targetPath && !hasValue)
                {
                    w.WritePropertyName("value");
                    w.WriteStartArray();
                    foreach (var v in values)
                    {
                        w.WriteStartObject();
                        w.WriteString("modelType", "Property");
                        w.WriteString("valueType", "xs:string");
                        w.WriteString("value", v);
                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                }
                w.WriteEndObject();
                break;

            case JsonValueKind.Array:
                w.WriteStartArray();
                var idx = 0;
                foreach (var item in elem.EnumerateArray())
                {
                    WriteSmlWithReplacedItems(w, item, targetPath, values, $"{currentPath}[{idx}]");
                    idx++;
                }
                w.WriteEndArray();
                break;

            default:
                elem.WriteTo(w);
                break;
        }
    }
}
