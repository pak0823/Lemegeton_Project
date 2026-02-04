using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(
    menuName = "Battle/Skills/Common/Fear On Bleed",
    fileName = "FearOnBleedSkill")]
public class FearOnBleedSkill : SkillAsset
{
    [Header("기본 공포 효과")]
    [Tooltip("부여할 공포 상태의 지속 턴 수 (기본 1턴)")]
    public int fearDurationTurns = 1;

    [Header("조건: 출혈이 있는 대상에게만 공포 부여")]
    [Tooltip("출혈 상태 ID (보통 Bleed)")]
    public StatusId bleedStatusId = StatusId.Bleeding;

    [Tooltip("공포 유닛 상태 ID (보통 UnitStateId.Fear)")]
    public UnitStateId fearStateId = UnitStateId.Fear;

    [Header("나약 중첩 부여 훈련")]
    [Tooltip("이 루트일 때, 대상에게 나약 중첩을 부여합니다.")]
    public bool trainingApplyWeakness = true;
    [Range(-1, 2)] public int routeForWeakness = 0;
    [Tooltip("나약에 해당하는 StatusId (인스펙터에서 지정)")]
    public StatusId weaknessStatusId = StatusId.None;
    [Tooltip("부여할 나약 중첩 수 (기본 3)")]
    public int weaknessStacks = 3;

    [Header("자원 절약 훈련")]
    [Tooltip("훈련에서 자원 비용을 덮어쓸지 여부")]
    public bool trainingUseCostOverride = false;
    [Tooltip("자원 감소가 적용될 훈련 루트 인덱스 (-1이면 비활성, 0 = 1번 루트)")]
    [Range(-1, 2)]
    public int routeForCostOverride = -1;
    [Tooltip("해당 루트에서 실제로 사용할 자원 비용")]
    public int trainingCostRoute = 0;

    [Header("대상 지정 불가 상태 제거 훈련")]
    [Tooltip("이 루트일 때, 타겟의 '대상 지정 불가' 관련 상태를 제거합니다.")]
    public bool trainingRemoveUntargetable = true;
    [Range(-1, 2)] public int routeForRemoveUntargetable = 2;

    [Tooltip("제거할 '대상 지정 불가' 상태 목록 (예: Ambush, Isolation 등)")]
    public UnitStateId[] untargetableStatesToClear;

    // 이 스킬은 선택 시 바로 발동(타일/유닛 지정 없음)
    public bool SelfCastOnSelect => true;

#if UNITY_EDITOR
    void OnValidate() { targetMode = SkillTargetMode.Unit; }
#endif
    void OnEnable() 
    { 
        targetMode = SkillTargetMode.Unit;
        costResource = SkillCostResource.Rage;
    }

    int GetRoute(BattleUnit _caster)
    {
        if (!_caster) return -1;
        return _caster.GetTrainingRouteIndex(this);
    }

    // 프리뷰 범위 없음 (전 적군 대상으로 동작)
    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int _originCell, bool _isOddRow)
    {
        yield break;
    }

    public override IEnumerator Execute(BattleManager bm, BattleUnit caster, BattleUnit targetUnit, Tilemap targetMap, Vector3Int targetCell)
    {
        // 타겟이 없으면 실행 불가
        if (targetUnit == null) yield break;

        // 공통 유닛 스킬 흐름 (접근 -> 애니 -> 효과)
        yield return bm.PerformStandardUnitSkillFlow(this, caster, targetUnit);
    }

    /// <summary>
    /// - 플레이어: 선택하자마자 SelfCastOnSelect 경로로 ResolveOnUnit(this, caster, caster) 호출
    /// - 적 AI: EnemyTurnRoutine에서 ResolveOnUnit(this, enemy, 랜덤타겟) 호출하지만
    ///   여기서는 target 파라미터를 무시하고 "caster의 모든 생존 적"을 기준으로 처리
    /// </summary>
    public override IEnumerator ResolveOnUnit(BattleManager _battlemanager, BattleUnit _caster, BattleUnit _target)
    {
        if (!_battlemanager || !_caster) yield break;

        // MP 비용 계산 및 차감 (훈련 반영)
        var res = GetCostResource(_caster);
        int cost = GetEffectiveCost(_caster);
        if (cost > 0 && !_caster.TryConsumeResource(res, cost)) yield break;

        int route = GetRoute(_caster);

        // === 1) 생존한 적 유닛 전체 조회 ===
        var enemies = _battlemanager.GetLivingEnemiesOf(_caster); // 이미 BattleManager에 구현되어 있음:contentReference[oaicite:1]{index=1}

        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;

            var sc = enemy.GetComponent<StatusController>();
            var usc = enemy.GetComponent<UnitStateController>();

            if (sc == null || usc == null) continue;

            // 출혈이 없는 적은 스킵
            if (bleedStatusId == StatusId.None || !sc.Has(bleedStatusId))
            {
                continue;
            }

            // === 2) 공포 상태 부여 ===
            if (fearStateId != UnitStateId.None)
            {
                int duration = Mathf.Max(1, fearDurationTurns);
                usc.ApplyForTurns(fearStateId, duration);   // 기존에 쓰는 턴지속 상태 부여 메서드와 동일 패턴:contentReference[oaicite:2]{index=2}

                Debug.Log($"[FearOnBleed] {_caster.name} → {enemy.name} 공포 {duration}턴 부여 (출혈 보유).");
            }

            // === 훈련 1: 나약 중첩 3, 1턴 ===
            if (trainingApplyWeakness &&
                routeForWeakness >= 0 &&
                route == routeForWeakness &&
                weaknessStatusId != StatusId.None &&
                weaknessStacks > 0)
            {
                sc.ApplyWithTurnContext(
                    weaknessStatusId,
                    weaknessStacks,
                    1
                );
                Debug.Log($"[FearOnBleed][Route1] {enemy.name} 나약 {weaknessStacks}중첩 (1턴) 부여.");
            }

            // === 훈련 3: '대상 지정 불가' 상태 제거 ===
            if (trainingRemoveUntargetable &&
                routeForRemoveUntargetable >= 0 &&
                route == routeForRemoveUntargetable &&
                untargetableStatesToClear != null)
            {
                foreach (var st in untargetableStatesToClear)
                {
                    if (st == UnitStateId.None) continue;
                    usc.Remove(st);
                }

                Debug.Log($"[FearOnBleed][Route3] {enemy.name} 의 대상 지정 불가 상태 제거.");
            }
        }

        yield break;
    }

    // 타일 대상 스킬이 아니므로 아무 것도 안 함
    public override IEnumerator ResolveOnTile(BattleManager _battlemanager, Tilemap _map, Vector3Int _originCell, BattleUnit _caster)
    {
        yield break;
    }

    public override int GetEffectiveCost(BattleUnit _caster)
    {
        int baseCost = base.GetEffectiveCost(_caster);
        if (!_caster) return baseCost;

        int route = GetRoute(_caster);
        if (trainingUseCostOverride && routeForCostOverride >= 0 && route == routeForCostOverride)
            return Mathf.Max(0, trainingCostRoute);

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
