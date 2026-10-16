using System.Text;
using AasxEditor.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace AasxEditor.Components.Pages;

public partial class Home
{
    // ===== CSV Export / Import =====

    internal async Task OnExportUserTagCsv(AasTreeNode tagNode)
    {
        var sb = new StringBuilder();
        sb.AppendLine("이름,로그 레벨,태그 주소,값 타입,매칭 조건,기준값");

        foreach (var child in tagNode.Children)
        {
            var val = child.Properties.GetValueOrDefault("value") ?? "";
            var parsed = ParseUserTag(val);
            if (parsed is null) continue;
            var (name, level, tag, vt, op, mv) = parsed.Value;
            sb.AppendLine($"{CsvEscape(name)},{CsvEscape(level)},{CsvEscape(tag)},{CsvEscape(vt)},{CsvEscape(op)},{CsvEscape(mv)}");
        }

        var base64 = Convert.ToBase64String(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray());
        await JS.InvokeVoidAsync("MonacoInterop.downloadFile", "UserTags.csv", base64);
        SetStatus("CSV 내보내기 완료", "success");
    }

    private static string CsvEscape(string value)
        => value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private void OnImportUserTagCsv(bool replace, AasTreeNode tagNode)
    {
        _csvTargetNode = tagNode;
        _csvImportReplace = replace;
    }

    internal Task OnCsvAddFileSelected(InputFileChangeEventArgs e)
    {
        _csvTargetNode = _selectedNode;
        _csvImportReplace = false;
        return OnCsvFileSelected(e);
    }

    internal Task OnCsvAddForNode(InputFileChangeEventArgs e, AasTreeNode node)
    {
        _csvTargetNode = node;
        _csvImportReplace = false;
        return OnCsvFileSelected(e);
    }

    internal Task OnCsvReplaceFileSelected(InputFileChangeEventArgs e)
    {
        _csvImportReplace = true;
        _showCsvReplaceConfirm = false;
        return OnCsvFileSelected(e);
    }

    private async Task OnCsvFileSelected(InputFileChangeEventArgs e)
    {
        try
        {
            var file = e.File;
            if (file is null) return;

            _showCsvReplaceConfirm = false;

            var targetNode = _csvTargetNode ?? _selectedNode;
            if (targetNode is null || string.IsNullOrWhiteSpace(_currentJson)) return;
            SelectNode(targetNode);

            using var reader = new StreamReader(file.OpenReadStream(maxAllowedSize: 5 * 1024 * 1024));
            var content = await reader.ReadToEndAsync();
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.TrimEnd('\r'))
                .ToList();

            // 헤더 행 건너뛰기
            var startIdx = 0;
            if (lines.Count > 0 && (lines[0].Contains("이름") || lines[0].StartsWith("Name", StringComparison.OrdinalIgnoreCase)))
                startIdx = 1;

            var entries = new List<(string Name, string LogLevel, string Tag, string ValueType, string MatchOp, string MatchValue)>();
            for (var i = startIdx; i < lines.Count; i++)
            {
                var parts = CsvParseLine(lines[i]);
                if (parts.Count < 4) continue;
                var name = parts[0].Trim();
                var level = parts[1].Trim();
                var tag = parts[2].Trim();
                var vt = parts[3].Trim();
                var op = parts.Count >= 5 ? parts[4].Trim() : string.Empty;
                var mv = parts.Count >= 6 ? parts[5].Trim() : string.Empty;
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(tag)) continue;
                if (string.IsNullOrWhiteSpace(level)) level = "Info";
                if (string.IsNullOrWhiteSpace(vt)) vt = "Bit";
                entries.Add((name, level, tag, vt, op, mv));
            }

            if (entries.Count == 0)
            {
                SetStatus("CSV에 유효한 데이터가 없습니다", "error");
                return;
            }

            var csvAction = _csvImportReplace ? "교체" : "추가";
            PushUndo($"CSV {csvAction} ({entries.Count}건)");

            var smlPath = targetNode.JsonPath;

            string? updatedJson;
            if (_csvImportReplace)
                updatedJson = ReplaceSmlItems(_currentJson, smlPath, entries.Select(e2 => FormatUserTag(e2.Name, e2.LogLevel, e2.Tag, e2.ValueType, e2.MatchOp, e2.MatchValue)).ToList());
            else
                updatedJson = AddSmlItems(_currentJson, smlPath, entries.Select(e2 => FormatUserTag(e2.Name, e2.LogLevel, e2.Tag, e2.ValueType, e2.MatchOp, e2.MatchValue)).ToList());

            if (updatedJson is null)
            {
                SetStatus("JSON 반영 실패", "error");
                return;
            }

            _currentEnv = Converter.JsonToEnvironment(updatedJson);
            await SyncJsonToEditorAsync(updatedJson);
            RebuildTree();
            RestoreUserTagNode(smlPath);

            _csvTargetNode = null;
            SetStatus($"CSV {csvAction} 완료: {entries.Count}건", "success");
        }
        catch (Exception ex) { SetStatus($"CSV 가져오기 오류: {ex.Message}", "error"); }
    }

    private static List<string> CsvParseLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    { current.Append('"'); i++; }
                    else
                        inQuotes = false;
                }
                else current.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { result.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }
}
