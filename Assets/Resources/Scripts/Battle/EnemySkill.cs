using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Battle/Skills/Enemy/Template", fileName = "SKILL_Template")]
public class EnemySkill : SkillAsset
{
    // 인스펙터에서 설정:
    // - displayName, icon
    // - school, attribute, power
    // - targetMode (Unit / Tile)
    // - legacyId  : 범위 미리보기/판정에 기존 도형을 재사용하려면 지정
    //   (주의: Skill1/Skill2로 두면 '시전 후 이동' 로직이 켜짐)

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddRow)
    {
        // 1) 레거시 범위 그대로 재사용 (추천)
        var def = SkillLibrary.Get(legacyId); // SkillDefinition
        return (def.GetAreaCells != null)
            ? def.GetAreaCells(originCell, isOddRow)
            : new[] { originCell };

        // 2) (선택) 직접 커스텀 도형을 만들고 싶다면 위 return을 주석 처리하고 아래 예시를 사용
        // var originAx = SkillLibrary.OffsetToAxial(originCell);
        // // 중심 + 이웃 6칸 (R=1)
        // Vector2Int[] offsets = {
        //     new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1,-1),
        //     new Vector2Int(0,-1), new Vector2Int(-1,0), new Vector2Int(-1,1),
        //     new Vector2Int(0, 1)
        // };
        // foreach (var d in offsets)
        //     yield return SkillLibrary.AxialToOffset(originAx + d);
    }

    public override IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
    {
        if (bm == null || caster == null || target == null) yield break;

        var map = target.CurrentMap;
        var origin = target.Cell; // 대상 유닛 좌표를 원점으로 범위 계산
        var area = GetAreaCells(origin, SkillLibrary.IsOddColumn(origin));
        var victims = bm.GetUnitsInArea(map, area);
        bm.ExecuteSkillDamage(caster, victims, this, map, origin); // SO 경로 전용
        yield return null;
    }

    public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        if (bm == null || map == null || caster == null) yield break;

        var area = GetAreaCells(originCell, SkillLibrary.IsOddColumn(originCell));
        var victims = bm.GetUnitsInArea(map, area);
        bm.ExecuteSkillDamage(caster, victims, this, map, originCell);
        yield return null;
    }

    // (선택) 커스텀 대미지 공식을 쓰고 싶을 때만 override
    // public override int ComputeDamage(BattleUnit caster, BattleUnit target, in SkillRuntime ctx)
    // {
    //     int baseDmg = base.ComputeDamage(caster, target, ctx); // school/attribute/power 반영
    //     // TODO: 필요시 추가 보정
    //     return baseDmg;
    // }
}
