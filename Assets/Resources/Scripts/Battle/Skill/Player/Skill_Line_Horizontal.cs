using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Battle/Skills/Player/Line Horizontal (Unit)", fileName = "Skill_Line_Horizontal")]
public class Skill_Line_Horizontal : SkillAsset
{
    private void OnEnable() { targetMode = SkillTargetMode.Unit; legacyId = SkillId.Skill1; }

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddColumn)
    {
        // {W, SELF, E}
        var ax = SkillLibrary.OffsetToAxial(originCell);
        var deltas = new[] { new Vector2Int(-1, 0), new Vector2Int(0, 0), new Vector2Int(1, 0) };
        foreach (var d in deltas)
            yield return SkillLibrary.AxialToOffset(new Vector2Int(ax.x + d.x, ax.y + d.y));
    }

    public override IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
    {
        // 기존 해결 루틴과 동일하게: area → victims → 피해
        var map = target.CurrentMap;
        var area = GetAreaCells(target.Cell, SkillLibrary.IsOddColumn(target.Cell));
        var victims = bm.GetUnitsInArea(map, area);
        bm.ExecuteSkillDamage(caster, victims, this, map, target.Cell);
        yield break;
    }

    public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        yield break; // Unit형이므로 사용 안함
    }
}
