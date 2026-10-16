using AasCore.Aas3_1;
using AasxEditor.Models;
using Env = AasCore.Aas3_1.Environment;

namespace AasxEditor.Components.Pages;

public partial class Home
{
    // ===== User Tag Editor State =====
    internal bool _showUserTagEditor;
    internal string _userTagName = "";
    internal string _userTagLogLevel = "Info";
    internal string _userTagAddress = "";
    internal string _userTagValueType = "Bit";
    internal string _userTagMatchOp = "";       // 빈 문자열이면 ValueType 기반 기본값 (Bit→RisingEdge / 그 외→Changed)
    internal string _userTagMatchValue = "";
    internal int _editingUserTagIndex = -1; // -1 = new, >= 0 = editing existing
    private bool _csvImportReplace; // true = 교체, false = 추가
    internal bool _showCsvReplaceConfirm;
    internal AasTreeNode? _csvReplaceTargetNode;
    private AasTreeNode? _csvTargetNode; // CSV 작업 대상 노드 (추가/교체 공용)

    internal static readonly string[] PlcValueTypes =
        ["Bit", "Byte", "Word", "DWord", "Int16", "Int32", "Real", "String"];

    internal static readonly string[] UserTagLogLevels =
        ["Info", "Warning", "Error"];

    private static readonly string[] UserTagMatchOps =
        ["", "RisingEdge", "FallingEdge", "Changed", "Eq", "Neq", "Gt", "Gte", "Lt", "Lte"];

    /// <summary>
    /// 현재 선택된 노드가 UserTags SML인지 판별
    /// </summary>
    private bool IsUserTagsSml =>
        _selectedNode is { NodeType: "SML" } &&
        _selectedNode.Label == "UserTags";

    /// <summary>
    /// 현재 UserTags의 자식 Property 값 목록 파싱 (v2 6필드, 레거시 4필드 호환)
    /// </summary>
    private List<(string Name, string LogLevel, string Tag, string ValueType, string MatchOp, string MatchValue)> GetUserTags()
    {
        if (_selectedNode?.Children is null) return [];
        return _selectedNode.Children
            .Select(c => c.Properties.GetValueOrDefault("value") ?? "")
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(ParseUserTag)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();
    }

    private static (string Name, string LogLevel, string Tag, string ValueType, string MatchOp, string MatchValue)? ParseUserTag(string encoded)
    {
        var parts = encoded.Split('|');
        if (parts.Length >= 4)
        {
            var op = parts.Length >= 5 ? parts[4].Trim() : string.Empty;
            var mv = parts.Length >= 6 ? parts[5].Trim() : string.Empty;
            return (parts[0].Trim(), parts[1].Trim(), parts[2].Trim(), parts[3].Trim(), op, mv);
        }
        return null;
    }

    private static string FormatUserTag(string name, string logLevel, string tag, string valueType, string matchOp, string matchValue)
        => $"{name}|{logLevel}|{tag}|{valueType}|{matchOp}|{matchValue}";

    // ===== UI Handlers =====

    internal void OnAddUserTag()
    {
        _editingUserTagIndex = -1;
        _userTagName = "";
        _userTagLogLevel = "Info";
        _userTagAddress = "";
        _userTagValueType = "Bit";
        _userTagMatchOp = "";
        _userTagMatchValue = "";
        _showUserTagEditor = true;
    }

    internal void OnEditUserTag(int index)
    {
        var tags = GetUserTags();
        if (index < 0 || index >= tags.Count) return;
        var (name, level, tag, vt, op, mv) = tags[index];
        _editingUserTagIndex = index;
        _userTagName = name;
        _userTagLogLevel = level;
        _userTagAddress = tag;
        _userTagValueType = vt;
        _userTagMatchOp = op;
        _userTagMatchValue = mv;
        _showUserTagEditor = true;
    }

    internal async Task OnDeleteUserTag(int index)
    {
        if (_selectedNode is null || string.IsNullOrWhiteSpace(_currentJson)) return;

        var children = _selectedNode.Children;
        if (index < 0 || index >= children.Count) return;

        PushUndo("사용자 태그 삭제");

        var smlPath = _selectedNode.JsonPath;
        var updatedJson = RemoveSmlItem(_currentJson, smlPath, index);
        if (updatedJson is null) return;

        _currentEnv = Converter.JsonToEnvironment(updatedJson);
        await SyncJsonToEditorAsync(updatedJson);
        RebuildTree();
        RestoreUserTagNode(smlPath);

        SetStatus("사용자 태그가 삭제되었습니다", "success");
    }

    internal async Task OnSaveUserTag()
    {
        if (string.IsNullOrWhiteSpace(_userTagName) || string.IsNullOrWhiteSpace(_userTagAddress))
        {
            SetStatus("태그 이름과 태그 주소를 입력하세요", "error");
            return;
        }

        if (_selectedNode is null || string.IsNullOrWhiteSpace(_currentJson)) return;

        PushUndo(_editingUserTagIndex >= 0 ? "사용자 태그 수정" : "사용자 태그 추가");

        var encoded = FormatUserTag(
            _userTagName.Trim(),
            _userTagLogLevel,
            _userTagAddress.Trim(),
            _userTagValueType,
            _userTagMatchOp?.Trim() ?? string.Empty,
            _userTagMatchValue?.Trim() ?? string.Empty);
        var smlPath = _selectedNode.JsonPath;

        string? updatedJson;
        if (_editingUserTagIndex >= 0)
            updatedJson = UpdateSmlItem(_currentJson, smlPath, _editingUserTagIndex, encoded);
        else
            updatedJson = AddSmlItem(_currentJson, smlPath, encoded);

        if (updatedJson is null)
        {
            SetStatus("JSON 반영 실패", "error");
            return;
        }

        _currentEnv = Converter.JsonToEnvironment(updatedJson);
        await SyncJsonToEditorAsync(updatedJson);
        RebuildTree();
        RestoreUserTagNode(smlPath);

        _showUserTagEditor = false;
        SetStatus(_editingUserTagIndex >= 0 ? "사용자 태그가 수정되었습니다" : "사용자 태그가 추가되었습니다", "success");
    }

    /// <summary>
    /// RebuildTree 후 UserTags SML 노드를 다시 선택하고 Explorer 경로를 복원
    /// </summary>
    private void RestoreUserTagNode(string smlJsonPath)
    {
        var restoredNode = FindNodeByJsonPath(_treeNodes, smlJsonPath);
        if (restoredNode is null) return;
        SelectNode(restoredNode);
        var path = new List<AasTreeNode>();
        if (FindPathToNode(_treeNodes, restoredNode, path))
            _explorerPath = path;
    }

    // ===== AASX 로드 시 누락된 UserTags 자동 생성 =====

    /// <summary>
    /// SequenceLogging 서브모델의 SystemProperties SMC에 UserTags SML이 없으면 빈 SML을 추가
    /// </summary>
    private static void EnsureUserTags(Env env)
    {
        if (env.Submodels is null) return;

        foreach (var sm in env.Submodels)
        {
            if (sm.IdShort is null || !sm.IdShort.Contains("Logging", StringComparison.OrdinalIgnoreCase))
                continue;
            if (sm.SubmodelElements is null) continue;

            foreach (var elem in sm.SubmodelElements)
            {
                if (elem is not SubmodelElementCollection sysPropsSmc) continue;
                if (sysPropsSmc.IdShort is null || !sysPropsSmc.IdShort.Contains("SystemProperties")) continue;
                if (sysPropsSmc.Value is null) continue;

                foreach (var sysElem in sysPropsSmc.Value)
                {
                    if (sysElem is not SubmodelElementCollection sysSmc) continue;
                    EnsureUserTagsInSmc(sysSmc);
                }
            }
        }
    }

    private static void EnsureUserTagsInSmc(SubmodelElementCollection smc)
    {
        smc.Value ??= new List<ISubmodelElement>();

        var hasUserTags = smc.Value.Any(e => e.IdShort == "UserTags");
        if (hasUserTags) return;

        var sml = new SubmodelElementList(
            typeValueListElement: AasSubmodelElements.Property,
            valueTypeListElement: DataTypeDefXsd.String);
        sml.IdShort = "UserTags";
        sml.Value = new List<ISubmodelElement>();

        smc.Value.Add(sml);
    }
}
