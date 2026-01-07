using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 선택 즉시 자기 자신에게 상태를 부여하는 스킬.
/// 지속시간 없음(무기한). 해제는 별도 '해제 스킬'로 처리.
/// </summary>
[CreateAssetMenu(menuName = "Battle/Skills/State/Self State (Permanent)", fileName = "SelfStateSkill")]
public class SelfStateSkill : SkillAsset, ISelfCastSkill
{
    [Header("State")]
    public UnitStateId stateId = UnitStateId.Support;

    [Header("Training")]
    [Header("자원 절약 훈련")]
    [Tooltip("소모 비용 덮어쓰기 활성화")]
    public bool trainingUseCostOverride = false;
    [Range(-1, 2)] public int routeForCostOverride = -1; // 유연한 루트 지정
    public int trainingCostOverride = -1;

    [Header("추가 버프 부여 설정")]
    [Tooltip("추가 버프 부여 활성화")]
    public bool trainingApplyMagicBuff = false;
    [Range(-1, 2)] public int routeForMagicBuff = -1; // 유연한 루트 지정
    public UnitStateBuffId trainingMagicBuffId = UnitStateBuffId.Smoke_MagicUp;

    [Header("총명 강화 훈련")]
    [Tooltip("총명 강화 버프 부여 활성화")]
    public bool trainingApplyClarityBuff = false;
    [Tooltip("이 훈련을 활성화할 루트 인덱스")]
    [Range(-1, 2)] public int routeForClarityBuff = -1;
    [Tooltip("적용할 버프 ID (DB에서 배율 설정)")]
    public UnitStateBuffId trainingClarityBuffId = UnitStateBuffId.ClarityUp;

    [Header("연속 행동 훈련")]
    [Tooltip("무료 행동(턴 미소모) 활성화")]
    public bool trainingUseFreeAction = false;
    [Range(-1, 2)] public int routeForFreeAction = -1; // 유연한 루트 지정

    public bool SelfCastOnSelect => true;

#if UNITY_EDITOR
    void OnValidate() { targetMode = SkillTargetMode.Unit; } // 기존 파이프 재사용
#endif
    void OnEnable() 
    { 
        targetMode = SkillTargetMode.Unit;
        costResource = SkillCostResource.MP;
    }

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int _originCell, bool _isOddRow)
    {
        yield break; // 프리뷰 불필요
    }

    public override IEnumerator Execute(BattleManager bm, BattleUnit caster, BattleUnit targetUnit, Tilemap targetMap, Vector3Int targetCell)
    {
        // 타겟팅 과정 없이, 즉시 자기 자신(caster)을 대상으로 실행 흐름 진입
        // PerformStandardUnitSkillFlow가 애니메이션 -> ResolveOnUnit 호출을 다 해줌
        yield return bm.PerformStandardUnitSkillFlow(this, caster, caster);
    }

    public override IEnumerator ResolveOnUnit(BattleManager _battlemanager, BattleUnit _caster, BattleUnit _target)
    {
        if (!_caster) yield break;

        var usc = _caster.GetComponent<UnitStateController>();
        if (usc == null) usc = _caster.gameObject.AddComponent<UnitStateController>();

        var res = GetCostResource(_caster);
        int cost = GetEffectiveCost(_caster);
        if (cost > 0 && !_caster.TryConsumeResource(res, cost))
        {
            Debug.Log($"[SelfStateSkill] 자원 부족: {displayName} (필요 {cost}, {res})");
            yield break;
        }

        // ==== 상태 부여 ====
        usc.Apply(stateId);

        // ==== 훈련 루트별 추가 효과 ====
        int route = _caster.GetTrainingRouteIndex(this);

        // Buff 부여
        if (trainingApplyMagicBuff && routeForMagicBuff >= 0 && route == routeForMagicBuff && trainingMagicBuffId != UnitStateBuffId.None)
        {
            usc.ApplyBuff(trainingMagicBuffId);
            Debug.Log($"[SelfStateSkill] Training Buff applied: {_caster.name}, Buff={trainingMagicBuffId}");
        }

        // 총명 강화
        if (trainingApplyClarityBuff && routeForClarityBuff >= 0 && route == routeForClarityBuff && trainingClarityBuffId != UnitStateBuffId.None)
        {
            // UnitStateBuffId를 통해 버프 부여
            usc.ApplyBuff(trainingClarityBuffId);
            Debug.Log($"[SelfStateSkill] Clarity Enhanced: {_caster.name}, Buff={trainingClarityBuffId}");
        }

        yield break;
    }

    public override IEnumerator ResolveOnTile(BattleManager _battlemanager, Tilemap _map, Vector3Int _originCell, BattleUnit _caster)
    {
        yield break; // 사용 안 함
    }

    public override int GetEffectiveCost(BattleUnit _caster)
    {
        int baseCost = base.GetEffectiveCost(_caster);
        if (_caster == null) return baseCost;

        int route = _caster.GetTrainingRouteIndex(this);

        if (trainingUseCostOverride && routeForCostOverride >= 0 && route == routeForCostOverride)
        {
            return Mathf.Max(0, trainingCostOverride);
        }

        return baseCost;
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
