using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 자기 자신에게 걸린 UnitState를 해제하는 스킬.
/// - removeAll = true: 전부 제거
/// - removeAll = false: 지정된 stateIds만 제거
/// </summary>
[CreateAssetMenu(menuName = "Battle/Skills/State/Self State Cleanse", fileName = "SelfStateCleanseSkill")]
public class SelfStateCleanseSkill : SkillAsset, ISelfCastSkill
{
    [Header("Cleanse Options")]
    public bool removeAll = true;
    public List<UnitStateId> stateIds = new(); // removeAll=false일 때만 사용

    [Header("Training")]
    [Header("자원 절약 훈련")]
    public bool trainingUseReducedCost;
    [Range(-1, 2)] public int routeForReducedCost = -1; // 루트 지정
    public int trainingReducedCost = 2;

    [Header("연속 행동 훈련")]
    public bool trainingUseFreeAction;
    [Range(-1, 2)] public int routeForFreeAction = -1; // 루트 지정

    public bool SelfCastOnSelect => true;

#if UNITY_EDITOR
    void OnValidate() { targetMode = SkillTargetMode.Unit; }
#endif
    void OnEnable() 
    { 
        targetMode = SkillTargetMode.Unit;
        costResource = SkillCostResource.MP;
    }

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddRow)
    {
        yield break;
    }

    public override IEnumerator Execute(BattleManager bm, BattleUnit caster, BattleUnit targetUnit, Tilemap targetMap, Vector3Int targetCell)
    {
        // 타겟팅 과정 없이, 즉시 자기 자신(caster)을 대상으로 실행 흐름 진입
        // PerformStandardUnitSkillFlow가 애니메이션 -> ResolveOnUnit 호출을 다 해줌
        yield return bm.PerformStandardUnitSkillFlow(this, caster, caster);
    }

    public override IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
    {
        if (!caster) yield break;

        // 훈련까지 반영된 실제 MP 비용
        var res = GetCostResource(caster);
        int cost = GetEffectiveCost(caster);
        if (cost > 0 && !caster.TryConsumeResource(res, cost)) yield break;

        var usc = caster.GetComponent<UnitStateController>();
        if (usc == null) yield break;

        if (removeAll)
        {
            usc.RemoveAll();
        }
        else
        {
            foreach (var id in stateIds)
                usc.Remove(id);
        }
        yield break;
    }

    public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        yield break;
    }

    public override int GetEffectiveCost(BattleUnit caster)
    {
        int cost = base.GetEffectiveCost(caster);
        if (!trainingUseReducedCost || caster == null) return cost;

        int route = caster.GetTrainingRouteIndex(this);

        // MP 감소
        if (routeForReducedCost >= 0 && route == routeForReducedCost)
        {
            cost = Mathf.Max(0, trainingReducedCost);
        }
        return cost;
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
