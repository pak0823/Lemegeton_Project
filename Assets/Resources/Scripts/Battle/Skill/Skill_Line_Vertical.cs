using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Battle/Skills/Line Vertical (Unit)", fileName = "Skill_Line_Vertical")]
public class Skill_Line_Vertical : SkillAsset
{
    [Min(1)] public int length = 1; // 1이면 {위, 자신, 아래} 3칸

    private void OnEnable() { targetMode = SkillTargetMode.Unit; legacyId = SkillId.Skill1; }// 시전 후 이동 

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddRow)
    {
        // axial deltas: (0,±k)
        var ax = SkillLibrary.OffsetToAxial(originCell);
        yield return originCell;
        for (int k = 1; k <= length; k++)
        {
            yield return SkillLibrary.AxialToOffset(new Vector2Int(ax.x, ax.y + k)); // 아래
            yield return SkillLibrary.AxialToOffset(new Vector2Int(ax.x, ax.y - k)); // 위
        }
    }

    public override IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
    {
        var map = target.CurrentMap;
        var area = GetAreaCells(target.Cell, SkillLibrary.IsOddColumn(target.Cell));
        var hits = bm.GetUnitsInArea(map, area);
        bm.ExecuteSkillDamage(caster, hits, new SkillDefinition { id = legacyId });
        yield break;
    }

    public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
    { yield break; }
}
