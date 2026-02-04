using System.Collections.Generic;
using System.Text;

public static class SkillTooltipUtil
{
    const string TrainingHeaderColor = "#80FF80";
    const string TrainingSizeTagOpen = "<size=20%>";
    const string TrainingSizeTagClose = "</size>";

    /// <summary>
    /// baseDesc 뒤에 [훈련 효과] 블록을 붙여 반환.
    /// lines가 비어 있으면 baseDesc 그대로 반환.
    /// </summary>
    //public static string AppendTrainingBlock(string baseDesc, List<string> lines)
    //{
    //    if (lines == null || lines.Count == 0) return baseDesc ?? "";

    //    var sb = new StringBuilder();
    //    sb.Append(baseDesc ?? "");
    //    sb.Append("\n");
    //    sb.Append(TrainingSizeTagOpen);
    //    sb.Append($"<color={TrainingHeaderColor}>[훈련 효과]</color>");

    //    foreach (var line in lines)
    //    {
    //        sb.Append("\n? ");
    //        sb.Append(line);
    //    }

    //    sb.Append(TrainingSizeTagClose);
    //    return sb.ToString();
    //}

    /// <summary>
    /// baseDesc 뒤에 [훈련 Title] + 설명 블록을 붙여서 반환.
    /// title/desc가 비어 있으면 baseDesc 그대로 반환.
    /// </summary>
    public static string AppendTrainingRouteDescription(string _baseDesc, string _routeTitle, string _routeDescription)
    {
        if (string.IsNullOrEmpty(_routeTitle) && string.IsNullOrEmpty(_routeDescription))
            return _baseDesc ?? "";

        var sb = new StringBuilder();
        sb.Append(_baseDesc ?? "");
        sb.Append("\n");
        sb.Append(TrainingSizeTagOpen);

        // Title이 없으면 그냥 "훈련"이라고만 표시
        string header = string.IsNullOrEmpty(_routeTitle) ? "훈련" : _routeTitle;
        sb.Append($"<color={TrainingHeaderColor}>[{header}]</color>");

        if (!string.IsNullOrEmpty(_routeDescription))
        {
            sb.Append("\n");
            sb.Append(_routeDescription);
        }

        sb.Append(TrainingSizeTagClose);
        return sb.ToString();
    }

    /// <summary>
    /// StatusId/UnitStateBuffId를 받아서 DB에서 이름을 가져오는 헬퍼.
    /// </summary>
    public static string GetStatusLabel(StatusId _statusid)
    {
        var db = StatusDescriptionDB.Instance;
        if (db == null) return _statusid.ToString();
        var label = db.GetDisplayName(_statusid);
        return string.IsNullOrEmpty(label) ? _statusid.ToString() : label;
    }

    public static string GetBuffLabel(UnitStateBuffId _unitstatebuffid)
    {
        var db = StatusDescriptionDB.Instance;
        if (db == null) return _unitstatebuffid.ToString();
        var label = db.GetDisplayName(_unitstatebuffid);
        return string.IsNullOrEmpty(label) ? _unitstatebuffid.ToString() : label;
    }
}
