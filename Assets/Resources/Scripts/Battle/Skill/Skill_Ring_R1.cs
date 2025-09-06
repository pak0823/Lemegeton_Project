using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Battle/Skills/Ring R1 (Tile)", fileName = "Skill_Ring_R1")]
public class Skill_Ring_R1 : SkillAsset
{
    private void OnEnable() { targetMode = SkillTargetMode.Tile; legacyId = SkillId.Skill4; }

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddColumn)
    {
        yield return originCell; // center
        var ax = SkillLibrary.OffsetToAxial(originCell);
        var deltas = new[]{
        new Vector2Int( 1, 0), new Vector2Int( 1,-1), new Vector2Int( 0,-1),
        new Vector2Int(-1, 0), new Vector2Int(-1, 1), new Vector2Int( 0, 1),
    };
        foreach (var d in deltas)
            yield return SkillLibrary.AxialToOffset(new Vector2Int(ax.x + d.x, ax.y + d.y));
    }

    public override IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
    { yield break; } // TileÇü

    public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        var area = GetAreaCells(originCell, SkillLibrary.IsOddColumn(originCell));
        var victims = bm.GetUnitsInArea(map, area);
        bm.ExecuteSkillDamage(caster, victims, this, map, originCell);
        yield break;
    }
}
