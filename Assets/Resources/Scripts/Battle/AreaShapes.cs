using System.Collections.Generic;
using UnityEngine;

public static class AreaShapes
{
    // Pointed-top axial ring (center 포함)
    private static readonly Vector2Int[] AXIAL_RING_WITH_CENTER = new[]
    {
        new Vector2Int( 0, 0),  // Center
        new Vector2Int( 1, 0),  // E
        new Vector2Int( 1,-1),  // NE
        new Vector2Int( 0,-1),  // NW
        new Vector2Int(-1, 0),  // W
        new Vector2Int(-1, 1),  // SW
        new Vector2Int( 0, 1),  // SE
    };

    /// <summary>
    /// ParametricDamageSkill.AreaPreset 기반 범위 반환.
    /// 지원: Single / LineHorizontal(3) / LineDiagU3 / LineDiagU7 / Ring(반경1, center 포함)
    /// </summary>
    public static IEnumerable<Vector3Int> GetCells(Vector3Int originCell, ParametricDamageSkill.AreaPreset preset, bool diagUseNEAxis)
    {
        if (preset == ParametricDamageSkill.AreaPreset.Single)
        {
            yield return originCell;
            yield break;
        }

        var ax = SkillLibrary.OffsetToAxial(originCell);

        if (preset == ParametricDamageSkill.AreaPreset.LineHorizontal)
        {
            yield return SkillLibrary.AxialToOffset(new Vector2Int(ax.x - 1, ax.y));
            yield return originCell;
            yield return SkillLibrary.AxialToOffset(new Vector2Int(ax.x + 1, ax.y));
            yield break;
        }

        if (preset == ParametricDamageSkill.AreaPreset.LineDiagU3 ||
            preset == ParametricDamageSkill.AreaPreset.LineDiagU7)
        {
            // 대각선 축: (1,-1)=NE축 또는 (0,-1)=NW축
            var dir = diagUseNEAxis ? new Vector2Int(1, -1) : new Vector2Int(0, -1);
            int radius = (preset == ParametricDamageSkill.AreaPreset.LineDiagU3) ? 1 : 3;
            for (int i = -radius; i <= radius; i++)
            {
                var p = new Vector2Int(ax.x + dir.x * i, ax.y + dir.y * i);
                yield return SkillLibrary.AxialToOffset(p);
            }
            yield break;
        }

        if (preset == ParametricDamageSkill.AreaPreset.Ring)
        {
            foreach (var d in AXIAL_RING_WITH_CENTER)
                yield return SkillLibrary.AxialToOffset(new Vector2Int(ax.x + d.x, ax.y + d.y));
            yield break;
        }

        // Fallback
        yield return originCell;
    }

    // 1) 대각선 3칸 (Vertical line of 3)
    public static IEnumerable<Vector3Int> LineVertical3(Vector3Int originCell)
    {
        var ax = SkillLibrary.OffsetToAxial(originCell);
        // axial (0,-1), (0,0), (0,1)
        yield return SkillLibrary.AxialToOffset(new Vector2Int(ax.x, ax.y - 1));
        yield return originCell;
        yield return SkillLibrary.AxialToOffset(new Vector2Int(ax.x, ax.y + 1));
    }

    // 2) 도넛(반경 1, 중심 제외 = 이웃 6칸)
    public static IEnumerable<Vector3Int> DonutRadius1(Vector3Int originCell)
    {
        var ax = SkillLibrary.OffsetToAxial(originCell);
        // 이웃 6방향(중심 제외)
        // E(1,0), NE(1,-1), NW(0,-1), W(-1,0), SW(-1,1), SE(0,1)
        var N6 = new[]
        {
        new Vector2Int( 1, 0), new Vector2Int( 1,-1), new Vector2Int( 0,-1),
        new Vector2Int(-1, 0), new Vector2Int(-1, 1), new Vector2Int( 0, 1),
    };
        foreach (var d in N6)
            yield return SkillLibrary.AxialToOffset(new Vector2Int(ax.x + d.x, ax.y + d.y));
    }

    // 3) 전방 부채꼴(반경1, 정면+좌우 3칸)
    //  - facingDirAxial: 정면 방향(축 좌표상의 6방 중 하나) 예: (1,0) / (1,-1) / (0,-1) / (-1,0) / (-1,1) / (0,1)
    public static IEnumerable<Vector3Int> FanForwardR1(Vector3Int originCell, Vector2Int facingDirAxial)
    {
        // 6방 배열로 인접 좌우 인덱스를 구해 3칸 부채꼴 구성
        var dirs = new[]
        {
        new Vector2Int( 1, 0), new Vector2Int( 1,-1), new Vector2Int( 0,-1),
        new Vector2Int(-1, 0), new Vector2Int(-1, 1), new Vector2Int( 0, 1),
    };

        int idx = -1;
        for (int i = 0; i < dirs.Length; i++)
            if (dirs[i] == facingDirAxial) { idx = i; break; }

        if (idx < 0) // 잘못된 입력 시 중심만
        {
            yield return originCell;
            yield break;
        }

        int left = (idx + 5) % 6;   // 좌측 이웃
        int right = (idx + 1) % 6;  // 우측 이웃

        var ax = SkillLibrary.OffsetToAxial(originCell);
        foreach (var d in new[] { dirs[left], dirs[idx], dirs[right] })
            yield return SkillLibrary.AxialToOffset(new Vector2Int(ax.x + d.x, ax.y + d.y));
    }
}
