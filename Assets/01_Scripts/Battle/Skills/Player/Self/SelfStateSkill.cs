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

    // 은신(Hidden) 훈련 추가
    [Header("사고 예방 훈련")]
    [Tooltip("은신 상태(Hidden) 부여 활성화 - 타겟팅 불가")]
    public bool trainingApplyStealth = false;
    [Range(-1, 2)] public int routeForStealth = -1;
    public UnitStateId trainingHiddenStateId = UnitStateId.Hidden;
    [Min(1)] public int trainingStealthDuration = 1; // 1턴 유지

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

        // 은신 부여
        if (trainingApplyStealth && routeForStealth >= 0 && route == routeForStealth && trainingHiddenStateId != UnitStateId.None)
        {
            // 은신 버프 부여 (지속시간 적용)
            usc.ApplyForTurns(trainingHiddenStateId, trainingStealthDuration);
            Debug.Log($"[SelfStateSkill] Stealth Applied: {_caster.name}, Duration={trainingStealthDuration}");
        }

        // 스킬 사용 완료 알림 (패시브가 이를 감지하여 스택 처리)
        _caster.NotifySkillUsed(this);

        yield break;
    }

    public override IEnumerator ResolveOnTile(BattleManager _battlemanager, Tilemap _map, Vector3Int _originCell, BattleUnit _caster)
    {
        yield break; // 사용 안 함
    }

    public override int GetEffectiveCost(BattleUnit _caster)
    {
        // 기본 훈련에 의한 비용 오버라이드 계산
        int finalCost = base.GetEffectiveCost(_caster);
        if (_caster != null)
        {
            int route = _caster.GetTrainingRouteIndex(this);
            if (trainingUseCostOverride && routeForCostOverride >= 0 && route == routeForCostOverride)
            {
                finalCost = Mathf.Max(0, trainingCostOverride);
            }
        }

        // 연구(Research) 중첩 3이면 비용 0 처리
        if (_caster != null)
        {
            var status = _caster.GetComponent<StatusController>();
            if (status != null)
            {
                // 연구 중첩이 3 이상이면 비용 무료
                if (status.GetStacks(StatusId.Research) >= 3)
                {
                    return 0;
                }
            }
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
