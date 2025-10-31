using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Battle/Skills/Common/Parametric Heal", fileName = "ParametricHealSkill")]
public class ParametricHealSkill : SkillAsset, ITargetMapProvider
{
    // 기존 Damage와 동일한 프리셋을 그대로 사용해 재사용성 확보
    public ParametricDamageSkill.AreaPreset areaPreset = ParametricDamageSkill.AreaPreset.Single;

    [Header("Targeting")]
    public SkillTargetMode selectionMode = SkillTargetMode.Tile; // 보통 Ring은 타일 지목이 편함
    public bool useProvidedUnitTarget = true;   // Unit 타겟팅일 때 클릭한 유닛을 센터로 사용

    [Header("Heal")]
    public float powerOverride = 1f;            // 힐 배수 덮어쓰기(옵션, 없으면 SkillAsset.power 사용)
    public bool consumeSupportAfterCast = false; // 시전 후 Support 상태 해제할지

#if UNITY_EDITOR
    void OnValidate() { targetMode = selectionMode; }
#endif
    void OnEnable()
    {
        // 힐은 Magical로 분류하고 싶으면 school만 바꿔두면 UI/로그 등에서 일관됨
        school = DamageSchool.Magical;
        if (powerOverride > 0f) power = powerOverride;
        targetMode = selectionMode;
    }
    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddColumn)
    {
        foreach (var c in AreaShapes.GetCells(originCell, areaPreset, false))
            yield return c;
    }

    // 미리보기/지목 타일 맵: 아군 플로어를 반환
    public Tilemap GetTargetMap(BattleManager bm, BattleUnit caster)
    {
        var prov = Shared.battleMapManager; // 프로젝트에서 쓰는 맵 프로바이더(같은 접근 방식 사용)
        if (prov == null) return null;
        // 플레이어면 PlayerFloor, 적이면 EnemyFloor(= "그 유닛의 아군 맵")
        return (caster != null && caster.team == Team.Player) ? prov.PlayerFloor : prov.EnemyFloor;
    }

    int CalcHealAmount(BattleUnit caster, BattleUnit target)
    {
        // 마법공격력 * 배수. 필요하면 라우터/상태/장비에 따른 보정도 추가 가능
        int baseStat = Mathf.Max(1, caster.MagicDamage);
        float mult = Mathf.Max(0f, power);
        return Mathf.Max(1, Mathf.FloorToInt(baseStat * mult));
    }

    void HealArea(BattleManager bm, BattleUnit caster, Tilemap map, Vector3Int centerCell)
    {
        var area = GetAreaCells(centerCell, SkillLibrary.IsOddColumn(centerCell));
        var friends = bm.GetUnitsInArea(map, area)
                        .Where(u => u != null && !u.IsDead && u.team == caster.team)
                        .ToList();

        foreach (var u in friends)
        {
            int amount = CalcHealAmount(caster, u);
            u.Heal(amount);

            // 최종 적대감 생성량 계산
            float hostilityGained = HostilityRules.FromHeal(amount, caster);

            // 캐스터(플레이어)의 적대감 증가
            caster.AddHostility(hostilityGained);
        }
    }

    public override IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
    {
        if (!bm || !caster) yield break;

        var center = (useProvidedUnitTarget && target && !target.IsDead) ? target : caster;
        HealArea(bm, caster, center.CurrentMap, center.Cell);
        if (consumeSupportAfterCast)
            caster.GetComponent<UnitStateController>()?.Remove(UnitStateId.Support);
        yield break;


    }

    public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        if (!bm || !caster || !map) yield break;

        HealArea(bm, caster, map, originCell);
        if (consumeSupportAfterCast)
            caster.GetComponent<UnitStateController>()?.Remove(UnitStateId.Support);
        yield break;


    }
}
