using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Battle/Skill/Enemy/Template", fileName = "SKILL_Template")]
public class EnemySkill : SkillAsset
{
    // AreaShapes 기반 프리셋 (레거시에 대응)
    public enum AreaPresetEnemy
    {
        Single,
        Horizontal3,           // 가로 3칸
        Vertical3,             // 세로 3칸
        Ring1_WithCenter,      // 반경1(중심 포함)
        Donut1_NoCenter,       // 반경1(중심 제외: 이웃 6칸)
        DiagU3_NE,             // 대각 3칸(NE축)
        DiagU3_NW,             // 대각 3칸(NW축)
        DiagU7_NE,             // 대각 7칸(NE축)
        DiagU7_NW,             // 대각 7칸(NW축)
        FanForwardR1           // 전방 부채꼴(반경1, 정면+좌우 3칸)
    }

    [Header("Area Preset (AreaShapes)")]
    public AreaPresetEnemy areaPreset = AreaPresetEnemy.Single;

    [Tooltip("Diag 계열에서 NE축을 쓸지 여부")]
    public bool diagUseNEAxis = true;

    [Header("Targeting Preference")]
    public SkillTargetPreference targetPreference = SkillTargetPreference.HighestHostility;

    [Header("Casting Suppression")]
    [Range(0, 3)] public int suppressionRequired = 0;  // 0이면 기존처럼 즉시 캔슬

    /// <summary>
    /// 미리보기/판정용 범위 셀 반환(프리셋에 따라 AreaShapes로 위임)
    /// - 주의: FanForwardR1은 정면이 필요하므로 여기선 원형(혹은 중심)로 단순화하고,
    ///   실제 Resolve에서 정면을 계산해 정확한 범위를 사용한다.
    /// </summary>
    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddRow)
    {
        switch (areaPreset)
        {
            case AreaPresetEnemy.Single:
                yield return originCell; yield break;

            case AreaPresetEnemy.Horizontal3:
                foreach (var c in AreaShapes.GetCells(originCell, AreaPreset.LineHorizontal, true))
                    yield return c;
                yield break;

            case AreaPresetEnemy.Vertical3:
                foreach (var c in AreaShapes.LineVertical3(originCell))
                    yield return c;
                yield break;

            case AreaPresetEnemy.Ring1_WithCenter:
                foreach (var c in AreaShapes.GetCells(originCell, AreaPreset.Ring, false))
                    yield return c;
                yield break;

            case AreaPresetEnemy.Donut1_NoCenter:
                foreach (var c in AreaShapes.DonutRadius1(originCell))
                    yield return c;
                yield break;

            case AreaPresetEnemy.DiagU3_NE:
                foreach (var c in AreaShapes.GetCells(originCell, AreaPreset.LineDiagU3, true))
                    yield return c;
                yield break;

            case AreaPresetEnemy.DiagU3_NW:
                foreach (var c in AreaShapes.GetCells(originCell, AreaPreset.LineDiagU3, false))
                    yield return c;
                yield break;

            case AreaPresetEnemy.DiagU7_NE:
                foreach (var c in AreaShapes.GetCells(originCell, AreaPreset.LineDiagU7, true))
                    yield return c;
                yield break;

            case AreaPresetEnemy.DiagU7_NW:
                foreach (var c in AreaShapes.GetCells(originCell, AreaPreset.LineDiagU7, false))
                    yield return c;
                yield break;

            case AreaPresetEnemy.FanForwardR1:
                // 프리뷰 단계에선 정면 정보가 없을 수 있으므로 안전하게 중심만, 또는 링1 등으로 간단 표시
                // 필요하면 링1 표시가 더 친절함:
                foreach (var c in AreaShapes.GetCells(originCell, AreaPreset.Ring, false))
                    yield return c;
                yield break;
        }
    }

    public override IEnumerator ResolveOnUnit(BattleManager _battlemanager, BattleUnit _caster, BattleUnit _target)
    {
        if (_battlemanager == null || _caster == null || _target == null) yield break;

        var map = _target.CurrentMap;
        var origin = _target.Cell;

        var area = GetAreaCellsForContext(map, origin, _caster, _target);
        var victims = _battlemanager.Grid.GetUnitsInArea(map, area);
        _battlemanager.ExecuteSkillDamage(_caster, victims, this, map, origin); // 중앙 경로(피해+적대감)
        yield return null;
    }

    public override IEnumerator ResolveOnTile(BattleManager _battlemanager, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        if (_battlemanager == null || map == null || caster == null) yield break;

        // 타일 지정형은 정면을 caster→origin으로 계산
        var area = GetAreaCellsForContext(map, originCell, caster, null);
        var victims = _battlemanager.Grid.GetUnitsInArea(map, area);
        _battlemanager.ExecuteSkillDamage(caster, victims, this, map, originCell);
        yield return null;
    }

    // --------- helpers ---------

    IEnumerable<Vector3Int> GetAreaCellsForContext(Tilemap map, Vector3Int originCell, BattleUnit caster, BattleUnit targetOrNull)
    {
        if (areaPreset != AreaPresetEnemy.FanForwardR1)
            return GetAreaCells(originCell, SkillLibrary.IsOddColumn(originCell)); // 위 프리셋 위임 사용

        // FanForwardR1: 정면 추출 후 AreaShapes.FanForwardR1 사용
        // 정면 = caster→target(유닛형) 또는 caster→origin(타일형)
        Vector3 casterW = caster != null ? caster.transform.position : map.GetCellCenterWorld(originCell);
        Vector3 aimW = (targetOrNull != null)
            ? targetOrNull.transform.position
            : map.GetCellCenterWorld(originCell);

        Vector2 aim = (aimW - casterW);
        if (aim.sqrMagnitude < 1e-6f) aim = Vector2.right;
        aim.Normalize();

        // 월드에서 가장 가까운 axial 6방을 고름
        var axialDirs = new[]
        {
            new Vector2Int( 1, 0), new Vector2Int( 1,-1), new Vector2Int( 0,-1),
            new Vector2Int(-1, 0), new Vector2Int(-1, 1), new Vector2Int( 0, 1),
        };

        // Grid 회전 고려(필요시 map.layoutGrid.transform.right/up 사용하는 것도 가능)
        Vector2 right = Vector2.right;
        Vector2 up = Vector2.up;

        int best = 0; float bestDot = float.NegativeInfinity;
        for (int i = 0; i < axialDirs.Length; i++)
        {
            Vector2 dirW = axialDirs[i].x * right + axialDirs[i].y * up;
            float d = Vector2.Dot(aim, dirW.normalized);
            if (d > bestDot) { bestDot = d; best = i; }
        }

        return AreaShapes.FanForwardR1(originCell, axialDirs[best]);
    }
}
