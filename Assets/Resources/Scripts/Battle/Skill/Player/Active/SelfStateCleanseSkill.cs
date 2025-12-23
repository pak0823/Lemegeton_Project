using System.Collections.Generic;
using System.Text;
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

    [Header("Training Overrides (Route 0 MP Cost)")]
    public bool trainingUseReducedMp;
    public bool trainingFreeActionOnRoute2; // Route 2 무료턴 여부
    public int trainingReducedMp = 2;   // 예: 기본 3MP → 훈련 시 2MP

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

    public override System.Collections.IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
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

    public override System.Collections.IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        yield break;
    }

    public override int GetEffectiveCost(BattleUnit caster)
    {
        int cost = base.GetEffectiveCost(caster);
        if (!trainingUseReducedMp || caster == null) return cost;

        int route = caster.GetTrainingRouteIndex(this);
        if (route == 0)
        {
            cost = Mathf.Max(0, trainingReducedMp);
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
