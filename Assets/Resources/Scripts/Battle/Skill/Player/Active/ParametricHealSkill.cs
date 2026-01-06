using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Battle/Skills/Common/Parametric Heal", fileName = "ParametricHealSkill")]
public class ParametricHealSkill : SkillAsset, ITargetMapProvider, IProjectileTileSkill
{
    // 기존 Damage와 동일한 프리셋을 그대로 사용해 재사용성 확보
    public ParametricDamageSkill.AreaPreset areaPreset = ParametricDamageSkill.AreaPreset.Single;

    [Header("Targeting")]
    public SkillTargetMode selectionMode = SkillTargetMode.Tile; // 보통 Ring은 타일 지목이 편함
    public bool useProvidedUnitTarget = true;   // Unit 타겟팅일 때 클릭한 유닛을 센터로 사용

    [Header("Heal")]
    public float powerOverride = 1f;            // 힐 배수 덮어쓰기(옵션, 없으면 SkillAsset.power 사용)

    // 투사체 설정
    [Header("Projectile Settings")]
    public ProjectileController projectilePrefab;
    public float projectileSpeed = 4f;

    // 시전 후 상태 제거 옵션
    [Header("State Consumption(상태 제거 설정)")]
    public bool consumeStateOnCast = false;
    public List<UnitStateId> statesToConsume = new List<UnitStateId>();

#if UNITY_EDITOR
    void OnValidate() { targetMode = selectionMode; }
#endif
    void OnEnable()
    {
        // 힐은 Magical로 분류하고 싶으면 school만 바꿔두면 UI/로그 등에서 일관됨
        school = DamageSchool.Magical;
        if (powerOverride > 0f) power = powerOverride;
        targetMode = selectionMode;
        costResource = SkillCostResource.MP;
    }
    public ProjectileController GetProjectilePrefab(BattleUnit caster)
    {
        return projectilePrefab;
    }

    void ConsumeStates(BattleUnit caster)
    {
        if (!consumeStateOnCast || statesToConsume == null) return;
        var usc = caster.GetComponent<UnitStateController>();
        if (usc == null) return;

        foreach (var s in statesToConsume)
        {
            if (s != UnitStateId.None)
                usc.Remove(s);
        }
    }

    public float GetProjectileSpeed(BattleUnit caster)
    {
        return projectileSpeed;
    }

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int _originCell, bool _isOddColumn)
    {
        foreach (var c in AreaShapes.GetCells(_originCell, areaPreset, false))
            yield return c;
    }

    // 미리보기/지목 타일 맵: 아군 플로어를 반환
    public Tilemap GetTargetMap(BattleManager _battlemanager, BattleUnit _caster)
    {
        var prov = Shared.battleMapManager; // 프로젝트에서 쓰는 맵 프로바이더(같은 접근 방식 사용)
        if (prov == null) return null;
        // 플레이어면 PlayerFloor, 적이면 EnemyFloor(= "그 유닛의 아군 맵")
        return (_caster != null && _caster.team == Team.Player) ? prov.PlayerFloor : prov.EnemyFloor;
    }

    int CalcHealAmount(BattleUnit _caster, BattleUnit _target)
    {
        // 마법공격력 * 배수. 필요하면 라우터/상태/장비에 따른 보정도 추가 가능
        float baseStat = Mathf.Max(1, _caster.MagicDamage);
        float mult = Mathf.Max(0f, power);
        return Mathf.Max(1, Mathf.FloorToInt(baseStat * mult));
    }

    void HealArea(BattleManager _battlemanager, BattleUnit _caster, Tilemap _map, Vector3Int _centerCell)
    {
        var area = GetAreaCells(_centerCell, SkillLibrary.IsOddColumn(_centerCell));
        var friends = _battlemanager.GetUnitsInArea(_map, area)
                        .Where(u => u != null && !u.IsDead && u.team == _caster.team)
                        .ToList();

        foreach (var u in friends)
        {
            int amount = CalcHealAmount(_caster, u);
            u.Heal(amount);

            // 최종 적대감 생성량 계산
            float hostilityGained = HostilityRules.FromHeal(amount, _caster);

            // 캐스터(플레이어)의 적대감 증가
            _caster.AddHostility(hostilityGained);
        }
    }

    public override IEnumerator ResolveOnUnit(BattleManager _battlemanager, BattleUnit _caster, BattleUnit _target)
    {
        if (!_battlemanager || !_caster) yield break;

        var center = (useProvidedUnitTarget && _target && !_target.IsDead) ? _target : _caster;

        var res = GetCostResource(_caster);
        int cost = GetEffectiveCost(_caster);
        if (cost > 0 && !_caster.TryConsumeResource(res, cost)) yield break;

        HealArea(_battlemanager, _caster, center.CurrentMap, center.Cell);
        ConsumeStates(_caster);

        yield break;
    }

    public override IEnumerator ResolveOnTile(BattleManager _battlemanager, Tilemap _map, Vector3Int _originCell, BattleUnit _caster)
    {
        if (!_battlemanager || !_caster || !_map) yield break;

        var res = GetCostResource(_caster);
        int cost = GetEffectiveCost(_caster);
        if (cost > 0 && !_caster.TryConsumeResource(res, cost)) yield break;

        HealArea(_battlemanager, _caster, _map, _originCell);
        ConsumeStates(_caster);

        yield break;


    }

    public override string GetFullDescriptionRich(BattleUnit _caster)
    {
        string baseDesc = base.GetFullDescriptionRich(_caster);

        int route = _caster != null ? _caster.GetTrainingRouteIndex(this) : -1;
        if (route < 0 || trainingRoutes == null || route >= trainingRoutes.Length)
            return baseDesc;

        var info = trainingRoutes[route];
        return SkillTooltipUtil.AppendTrainingRouteDescription(
            baseDesc,
            info.title,
            info.description
        );
    }
}
