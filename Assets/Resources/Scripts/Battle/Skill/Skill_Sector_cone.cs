using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Battle/Skills/Skill_Sector_cone (Unit)", fileName = "Skill_Cone_Fixed3_Axial")]
public class Skill_Sector_cone : SkillAsset
{
    // Axial(q,r) 델타: center + W + NW
    // (참고) 6방향: E(1,0), NE(1,-1), NW(0,-1), W(-1,0), SW(-1,1), SE(0,1)
    [SerializeField]
    private Vector2Int[] axialDeltas = new[]
    {
        new Vector2Int(0, 0),   // center
        new Vector2Int(-1, 0),  // W
        new Vector2Int(0, -1),  // NW
    };

    private void OnEnable()
    {
        targetMode = SkillTargetMode.Unit;
        legacyId = SkillId.Skill1; // 1/2만 이동 트리거 → 이동 없음
    }

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddRow)
    {
        var originAx = SkillLibrary.OffsetToAxial(originCell);
        foreach (var d in axialDeltas)
        {
            var ax = new Vector2Int(originAx.x + d.x, originAx.y + d.y);
            yield return SkillLibrary.AxialToOffset(ax);
        }
    }

    //public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
    //{
    //    var cells = GetAreaCells(originCell, SkillLibrary.IsOddColumn(originCell));
    //    var victims = bm.GetUnitsInArea(map, cells);
    //    bm.ExecuteSkillDamage(caster, victims, new SkillDefinition { id = legacyId });
    //    yield break;
    //}
    public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {yield break;}

    // 유닛 지목
    public override IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
    {
        var originCell = target.Cell;
        var map = target.CurrentMap;
        var cells = GetAreaCells(originCell, SkillLibrary.IsOddColumn(originCell));
        var victims = bm.GetUnitsInArea(map, cells);
        bm.ExecuteSkillDamage(caster, victims, new SkillDefinition { id = legacyId });
        yield break;
    }
}
