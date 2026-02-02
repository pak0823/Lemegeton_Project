using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Battle/Skills/Common/Parametric Heal", fileName = "ParametricHealSkill")]
public class ParametricHealSkill : SkillAsset, ITargetMapProvider, IProjectileTileSkill
{
    // 기존 Damage와 동일한 프리셋을 그대로 사용해 재사용성 확보
    public AreaPreset areaPreset = AreaPreset.Single;

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

    [Header("Training")]
    [Header("범위 확대 훈련")]
    public bool trainingUseAreaOverride = false;
    [Range(-1, 2)] public int routeForAreaOverride = -1;
    public AreaPreset trainingAreaPreset = AreaPreset.Single;
    public bool trainingDiagUseNEAxis = true;

    [Header("적의 감소 훈련")]
    [Tooltip("훈련 시 적의 생성량을 감소시킬지 여부")]
    public bool trainingReduceHostility = false;
    [Tooltip("적의 감소를 활성화시키는 훈련 루트 인덱스 (-1이면 비활성)")]
    [Range(-1, 2)] public int routeForReduceHostility = -1;
    [Tooltip("적용될 적의 생성 배율 (예: 0.5 = 50%만 생성)")]
    public float trainingHostilityMultiplier = 0.5f;

    [Header("자원 절약 훈련")]
    [Tooltip("소모 비용 덮어쓰기 활성화")]
    public bool trainingUseCostOverride = false;
    [Range(-1, 2)] public int routeForCostOverride = -1; // 유연한 루트 지정
    public int trainingCostOverride = -1;

    [Header("총명 강화 훈련")]
    [Tooltip("총명(Magic Damage) 강화 버프 부여 활성화")]
    public bool trainingApplyClarityBuff = false;
    [Range(-1, 2)] public int routeForClarityBuff = -1;
    public UnitStateBuffId trainingClarityBuffId = UnitStateBuffId.ClarityUp;
    [Min(1)] public int trainingClarityDuration = 1;

#if UNITY_EDITOR
    void OnValidate() { targetMode = selectionMode; }
#endif
    void OnEnable()
    {
        if (powerOverride > 0f) power = powerOverride;
        targetMode = selectionMode;
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
    int GetRoute(BattleUnit _caster)
    {
        if (_caster == null) return -1;
        return _caster.GetTrainingRouteIndex(this);
    }

    public float GetProjectileSpeed(BattleUnit caster)
    {
        return projectileSpeed;
    }

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int _originCell, bool _isOddColumn)
    {
        // 현재 턴의 시전자(플레이어든 적이든)
        var bm = BattleManager.Instance;
        BattleUnit caster = bm != null ? bm.ActingUnit : null;

        int route = GetRoute(caster);
        bool useOverride = trainingUseAreaOverride
                   && routeForAreaOverride >= 0
                   && route == routeForAreaOverride;

        // 기본/훈련 프리셋 선택
        var preset = useOverride ? trainingAreaPreset : areaPreset;
        // HealSkill에는 diagUseNEAxis 필드가 없었으므로 기본값 false 혹은 training 변수 사용
        // 여기서는 trainingDiagUseNEAxis 혹은 기본 로직(false/true)을 따라야 함.
        // ParametricHealSkill에 diagUseNEAxis 필드가 없다면 false로 가정하거나 추가해야 함.
        // *편의상 override일 때만 trainingDiagUseNEAxis를 쓰고, 아닐 땐 false(기본) 처리
        bool useDiag = useOverride ? trainingDiagUseNEAxis : false;

        foreach (var c in AreaShapes.GetCells(_originCell, preset, useDiag))
            yield return c;
    }

    // 미리보기/지목 타일 맵: 아군 플로어를 반환
    public Tilemap GetTargetMap(BattleManager _battlemanager, BattleUnit _caster)
    {
        var prov = BattleMapManager.Instance; // 프로젝트에서 쓰는 맵 프로바이더(같은 접근 방식 사용)
        if (prov == null) return null;
        // 플레이어면 PlayerFloor, 적이면 EnemyFloor(= "그 유닛의 아군 맵")
        return (_caster != null && _caster.data.team == Team.Player) ? prov.PlayerFloor : prov.EnemyFloor;
    }

    int CalcHealAmount(BattleUnit _caster, BattleUnit _target)
    {
        // 마법공격력 * 배수. 필요하면 라우터/상태/장비에 따른 보정도 추가 가능
        float baseStat = Mathf.Max(1, _caster.CLV);
        float mult = Mathf.Max(0f, power);
        return Mathf.Max(1, Mathf.FloorToInt(baseStat * mult));
    }

    void HealArea(BattleManager _battlemanager, BattleUnit _caster, Tilemap _map, Vector3Int _centerCell)
    {
        var area = GetAreaCells(_centerCell, SkillLibrary.IsOddColumn(_centerCell));
        var friends = _battlemanager.Grid.GetUnitsInArea(_map, area)
                        .Where(u => u != null && !u.IsDead && u.data.team == _caster.data.team)
                        .ToList();

        int route = GetRoute(_caster);

        foreach (var u in friends)
        {
            int amount = CalcHealAmount(_caster, u);
            u.Heal(amount);

            // 최종 적대감 생성량 계산
            float hostilityGained = HostilityRules.FromHeal(amount, _caster);

            // 적의 감소 적용
            if (trainingReduceHostility && routeForReduceHostility >= 0 && route == routeForReduceHostility)
            {
                hostilityGained *= trainingHostilityMultiplier;
                Debug.Log($"[Training] Heal Hostility Reduced: {hostilityGained} (x{trainingHostilityMultiplier})");
            }

            // 캐스터(플레이어)의 적대감 증가
            _caster.AddHostility(hostilityGained);
        }

        // 총명(Clarity) 강화 버프 적용
        if (trainingApplyClarityBuff && routeForClarityBuff >= 0 && route == routeForClarityBuff && trainingClarityBuffId != UnitStateBuffId.None)
        {
            var usc = _caster.GetComponent<UnitStateController>();
            if (usc != null)
            {
                // 현재 턴 소모 보정을 위해 +1
                usc.ApplyBuffForTurns(trainingClarityBuffId, trainingClarityDuration + 1);
                Debug.Log($"[ParametricHeal] Clarity Enhanced: {_caster.name}, Duration={trainingClarityDuration}");
            }
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

    public override int GetEffectiveCost(BattleUnit caster)
    {
        int finalCost = base.GetEffectiveCost(caster);

        if (caster == null) return finalCost;

        int route = caster.GetTrainingRouteIndex(this);

        if (trainingUseCostOverride && routeForCostOverride >= 0 && route == routeForCostOverride)
        {
            finalCost -= trainingCostOverride;
            finalCost = Mathf.Max(0, finalCost);
        }

        return finalCost;
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
