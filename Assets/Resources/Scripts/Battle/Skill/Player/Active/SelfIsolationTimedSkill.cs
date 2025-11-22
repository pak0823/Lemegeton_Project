using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 자신에게 'n턴 동안' UnitState를 부여하는 스킬 (쇄국 1턴 용).
/// - 기존 SelfStateSkill은 무기한, 이건 턴 지속형.
/// </summary>
[CreateAssetMenu(
    menuName = "Battle/Skills/State/Self State Timed (Isolation)",
    fileName = "SelfIsolationTimedSkill")]
public class SelfIsolationTimedSkill : SkillAsset, ISelfCastSkill
{
    [Header("State")]
    [Tooltip("부여할 UnitState")]
    public UnitStateId stateId = UnitStateId.Isolation;

    [Tooltip("지속 턴 수 (예: 1턴)")]
    [Min(1)] public int durationTurns = 1;

    [Header("Training")]
    [Header("아군 방어 중첩 적용")]
    [Tooltip("모든 아군에게 방어 중첩을 부여할지 여부")]
    public bool trainingApplyDefenseStacks = false;
    [Tooltip("방어 중첩을 적용할 훈련 루트(-1이면 비활성)")]
    [Range(-1, 2)] public int routeForDefenseStacks = -1;
    [Tooltip("부여할 방어 상태 ID")]
    public StatusId trainingDefenseStatusId = StatusId.None;
    [Min(1)] public int trainingDefenseStacks = 3;
    [Min(1)] public int trainingDefenseDurationTurns = 1;

    [Header("강제 이동 방지")]
    [Tooltip("이 스킬로 부여된 상태가 강제 이동을 막게 할지 여부")]
    public bool trainingPreventForcedMove = false;
    [Tooltip("강제 이동 방지 효과를 적용할 훈련 루트(-1이면 비활성)")]
    [Range(-1, 2)] public int routeForPreventForcedMove = -1;

    [Header("적의 증가 적용")]
    [Tooltip("자신의 적의를 배수로 증가시킬지 여부")]
    public bool trainingMultiplyHostility = false;
    [Tooltip("적의 배수 적용 훈련 루트(-1이면 비활성)")]
    [Range(-1, 2)] public int routeForHostilityMultiplier = -1;
    [Tooltip("현재 적의에 곱할 배수")]
    public float trainingHostilityMultiplier = 5.0f;


    public bool SelfCastOnSelect => true;

#if UNITY_EDITOR
    void OnValidate() { targetMode = SkillTargetMode.Unit; }
#endif
    void OnEnable() { targetMode = SkillTargetMode.Unit; }

    int GetRoute(BattleUnit caster)
    {
        if (!caster) return -1;
        return caster.GetTrainingRouteIndex(this);
    }

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddRow)
    {
        // 자기 자신 타겟이라 프리뷰 필요 없음
        yield break;
    }

    public override IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
    {
        if (!caster) yield break;
        if (!bm) yield break;

        // 실제 캐스터는 항상 자기 자신
        target = caster;

        // MP 비용 계산 (훈련 반영)
        int cost = mpCost;
        if (cost > 0 && !caster.TryConsumeMP(cost))
        {
            Debug.Log($"[SelfIsolationTimedSkill] MP 부족: {displayName} (필요 {cost})");
            yield break;
        }

        var usc = caster.GetComponent<UnitStateController>();
        if (usc == null)
            usc = caster.gameObject.AddComponent<UnitStateController>();

        // Isolation n턴 부여
        int turns = Mathf.Max(1, durationTurns);
        usc.ApplyForTurns(stateId, turns);

        int route = GetRoute(caster);

        // 모든 아군에게 방어 중첩 부여
        if (trainingApplyDefenseStacks &&
            routeForDefenseStacks >= 0 &&
            route == routeForDefenseStacks &&
            trainingDefenseStatusId != StatusId.None)
        {
            foreach (var ally in bm.GetLivingAlliesOf(caster))
            {
                var sc = ally.GetComponent<StatusController>();
                if (sc != null)
                {
                    sc.ApplyWithTurnContext(
                        trainingDefenseStatusId,
                        Mathf.Max(1, trainingDefenseStacks),
                        Mathf.Max(1, trainingDefenseDurationTurns)
                    );
                }
            }
        }

        // 강제 이동 방지 플래그 부여 (아래 2.에서 설명할 UnitStateController 확장 사용)
        if (trainingPreventForcedMove &&
            routeForPreventForcedMove >= 0 &&
            route == routeForPreventForcedMove)
        {
            usc.ApplyForcedMoveImmunityForTurns(turns);
        }

        // 자신의 적의 xN
        if (trainingMultiplyHostility &&
            routeForHostilityMultiplier >= 0 &&
            route == routeForHostilityMultiplier &&
            trainingHostilityMultiplier > 0f)
        {
            float current = Mathf.Max(0f, caster.Hostility);
            float targetHost = current * trainingHostilityMultiplier;
            float delta = targetHost - current;
            if (delta > 0f)
                caster.AddHostility(delta);
        }

        yield break;
    }

    public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        yield break;
    }

    public override string GetFullDescriptionRich(BattleUnit caster)
    {
        int cost = GetEffectiveMpCost(caster);
        string mpColor = "#00A2FF";
        string baseDesc;

        if (!string.IsNullOrEmpty(description))
        {
            if (cost > 0)
                baseDesc = $"{description}<size=20%><color=#808080>(MP:<color={mpColor}>{cost}</color>)</color></size>";
            else
                baseDesc = description;
        }
        else
        {
            baseDesc = base.GetFullDescriptionRich(caster);
        }

        int route = caster != null ? caster.GetTrainingRouteIndex(this) : -1;
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
