using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 자신에게 1턴 동안 잠복 상태를 부여하는 스킬.
/// - 적은 이 유닛을 타겟으로 지정할 수 없음
/// - AGI 0.40, INS 4.00 (StateStatModifierDB에서 설정)
/// - 노을(이 스킬 보유 유닛)이 공격하면 잠복 해제
/// 
/// 훈련:
///  0번: 자원 소모 감소(전용 MP 코스트 override) -> SkillAsset Base 처리
///  1번: 상태가 민첩을 약화하지 않음(AGI 페널티 상쇄 버프 부여)
///  2번: 스킬 사용 시 자신 적의 감소(0.40배)
///  + 상태 유지 중 자신의 차례 시작 시 생명 회복
/// </summary>
[CreateAssetMenu(menuName = "Battle/Skills/Player/Self Ambush", fileName = "SelfAmbushSkill")]
public class SelfAmbushSkill : SkillAsset, ISelfCastSkill
{
    public bool SelfCastOnSelect => true;

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int _originCell, bool _isOddColumn)
    {
        // Self target - return just the origin
        yield return _originCell;
    }
    [Header("Turn Start Heal")]
    public bool trainingHealOnTurnStart = false;
    [Range(-1, 2)] public int routeForHealOnTurnStart = -1;
    public float healPerClv = 4.0f; // Default multiplier

    [Header("No Agi Penalty")]
    public bool trainingNoAgiPenalty = false;
    [Range(-1, 2)] public int routeForNoAgiPenalty = -1;

    [Header("Reduce Hostility")]
    public bool trainingReduceHostility = false; // Importer maps to trainingHostilityDown?
    // Importer checks "HostilityDown" AND "ReduceHostility". 
    // ParametricDamage used "trainingReduceHostility".
    // I will use "trainingReduceHostility" for consistency.
    [Range(-1, 2)] public int routeForReduceHostility = -1; // routeForHostilityDown / routeForReduceHostility
    public float hostilityMultiplier = 0.4f;

    // Compatible fields for Importer (if needed)
    // Importer lines 273-276: try "trainingHostilityDown", "routeForHostilityDown".
    // Then "trainingReduceHostility", "routeForReduceHostility".
    // I'll provide standard names.
    public bool trainingHostilityDown => trainingReduceHostility; 
    public int routeForHostilityDown => routeForReduceHostility;
    
    // Also "trainingHostilityMultiplier" used in Importer line 278, but here I named it hostilityMultiplier.
    // Importer line 277 sets "hostilityMultiplier".
    public float trainingHostilityMultiplier = 0.4f; // Just in case

    public int ComputeTurnStartHeal(BattleUnit caster)
    {
        if (!caster) return 0;
        // CLV * 4
        int amount = Mathf.Max(1, Mathf.FloorToInt(caster.CLV * healPerClv));
        return amount;
    }

    void RegisterBreakOnAttack(BattleUnit caster)
    {
        if (!caster) return;

        System.Action<BattleUnit, BattleUnit, int, SkillAsset> handler = null;
        handler = (dealer, victim, damage, source) =>
        {
            if (dealer != caster) return;

            var usc = caster.GetComponent<UnitStateController>();
            if (usc == null)
            {
                caster.OnDealtDamage -= handler;
                return;
            }

            if (usc.Has(UnitStateId.Ambush))
            {
                usc.Remove(UnitStateId.Ambush);
                Debug.Log($"[Ambush] {caster.name}가 공격하여 잠복 상태가 해제되었습니다.");
            }

            caster.OnDealtDamage -= handler;
        };

        caster.OnDealtDamage += handler;
    }

    public override IEnumerator Execute(BattleManager bm, BattleUnit caster, BattleUnit targetUnit, Tilemap targetMap, Vector3Int targetCell)
    {
        yield return bm.PerformStandardUnitSkillFlow(this, caster, caster);
    }

    public override IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
    {
        if (!caster || !bm) yield break;

        target = caster;

        var res = GetCostResource(caster);
        int cost = GetEffectiveCost(caster);
        if (cost > 0 && !caster.TryConsumeResource(res, cost))
            yield break;

        var usc = caster.GetComponent<UnitStateController>();
        if (usc == null)
            usc = caster.gameObject.AddComponent<UnitStateController>();

        bool added = usc.Apply(UnitStateId.Ambush);
        if (added)
        {
            Debug.Log($"[Ambush] {caster.name}에게 잠복 상태 부여");
        }

        int route = caster.GetTrainingRouteIndex(this);

        // No Agi Penalty
        if (trainingNoAgiPenalty && routeForNoAgiPenalty >= 0 && route == routeForNoAgiPenalty)
        {
            if (usc.ApplyBuff(UnitStateBuffId.AmbushAgiCancel))
            {
                Debug.Log($"[Ambush] Agi Penalty Cancel Applied");
            }
        }

        // Reduce Hostility
        if (trainingReduceHostility && routeForReduceHostility >= 0 && route == routeForReduceHostility)
        {
            float before = caster.Hostility;
            float targetHost = Mathf.Max(0f, before * hostilityMultiplier);
            float delta = targetHost - before;
            caster.AddHostility(delta); // delta is negative
            Debug.Log($"[Ambush] Hostility Reduced: {before} -> {targetHost}");
        }

        RegisterBreakOnAttack(caster);
        yield break;
    }

    public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        yield break;
    }

    public override string GetFullDescriptionRich(BattleUnit _caster)
    {
        string baseDesc = base.GetFullDescriptionRich(_caster);
        int route = _caster != null ? _caster.GetTrainingRouteIndex(this) : -1;
        if (route < 0 || trainingRoutes == null || route >= trainingRoutes.Length)
            return baseDesc;

        var info = trainingRoutes[route];
        return SkillTooltipUtil.AppendTrainingRouteDescription(baseDesc, info.title, info.description);
    }
}
