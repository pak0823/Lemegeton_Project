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
    [Header("기본 방어 중첩 효과")]
    [Tooltip("기본 방어 상태 ID")]
    public StatusId baseDefenseStatusId = StatusId.GuardStack;
    [Min(1)] public int baseDefenseStacks = 3;
    [Min(1)] public int baseDefenseDurationTurns = 1;

    [Header("Training")]

    [Header("자원 절약 효과")]
    [Tooltip("훈련에서 MP 비용을 덮어쓸지 여부")]
    public bool trainingUseMpOverride = false;
    [Tooltip("MP 감소가 적용될 훈련 루트 인덱스 (-1이면 비활성, 보통 0 = 1번 루트)")]
    [Range(-1, 2)] public int routeForMpOverride = 0;
    [Tooltip("해당 루트에서 실제로 사용할 MP 비용")]
    public int trainingMpCostRoute0 = 2;

    [Header("강제 이동 방지")]
    [Tooltip("이 스킬로 부여된 상태가 강제 이동을 막게 할지 여부")]
    public bool trainingPreventForcedMove = false;
    [Tooltip("강제 이동 방지 효과를 적용할 훈련 루트(-1이면 비활성)")]
    [Range(-1, 2)] public int routeForPreventForcedMove = -1;
    [Tooltip("강제 이동을 막는 데 사용할 상태 ID (예: MoveResist)")]
    public StatusId moveResistStatusId = StatusId.MoveResist;
    [Tooltip("이동 저항 상태의 지속 턴수")]
    [Min(1)] public int moveResistDurationTurns = 1;

    [Header("방어 중첩 지속턴 증가")]
    [Tooltip("훈련으로 방어 중첩 지속 턴을 늘릴지 여부")]
    public bool trainingExtendDefenseDuration = false;
    [Tooltip("방어 중첩 지속 턴 증가가 적용될 훈련 루트 인덱스 (-1이면 비활성)")]
    [Range(-1, 2)] public int routeForExtendDefenseDuration = -1;
    [Tooltip("지속 턴 증가 시 사용할 턴 수")]
    [Min(1)] public int extendedDefenseDurationTurns = 2;



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
        int cost = GetEffectiveMpCost(caster);
        if (cost > 0 && !caster.TryConsumeMP(cost))
        {
            Debug.Log($"[SelfIsolationTimedSkill] MP 부족: {displayName} (필요 {cost})");
            yield break;
        }

        var usc = caster.GetComponent<UnitStateController>();
        if (usc == null)
            usc = caster.gameObject.AddComponent<UnitStateController>();

        int route = GetRoute(caster);

        // 실제 적용할 지속 턴 계산
        int defenseDuration = baseDefenseDurationTurns;
        if (trainingExtendDefenseDuration &&
            routeForExtendDefenseDuration >= 0 &&
            route == routeForExtendDefenseDuration)
        {
            defenseDuration = Mathf.Max(defenseDuration, extendedDefenseDurationTurns);
        }

        StatusId defId = baseDefenseStatusId;
        int defStacks = Mathf.Max(1, baseDefenseStacks);
        var allUnits = Object.FindObjectsOfType<BattleUnit>();

        // 방어 중첩 적용
        if (defId != StatusId.None)
        {
            foreach (var u in allUnits)
            {
                if (u == null || u.IsDead || u.IsRetreated) continue;

                var sc = u.GetComponent<StatusController>();
                if (sc == null) continue;

                sc.ApplyWithTurnContext(
                    defId,
                    defStacks,
                    Mathf.Max(1, defenseDuration)
                );
            }
        }

        // 강제 이동 방지용 이동 저항 상태 1턴 부여
        if (trainingPreventForcedMove &&
            routeForPreventForcedMove >= 0 &&
            route == routeForPreventForcedMove &&
            moveResistStatusId != StatusId.None)
        {
            foreach (var u in allUnits)
            {
                if (u == null || u.IsDead || u.IsRetreated) continue;

                var sc = u.GetComponent<StatusController>();
                if (sc == null) continue;

                sc.ApplyWithTurnContext(
                    moveResistStatusId,
                    1,                                   // 스택 1
                    Mathf.Max(1, moveResistDurationTurns) // 기본 1턴
                );
            }
        }

        yield break;
    }

    public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        yield break;
    }

    public override int GetEffectiveMpCost(BattleUnit caster)
    {
        int cost = mpCost;
        if (caster == null) return cost;

        int route = caster.GetTrainingRouteIndex(this);
        if (trainingUseMpOverride &&
            routeForMpOverride >= 0 &&
            route == routeForMpOverride)
        {
            cost = Mathf.Max(0, trainingMpCostRoute0);
        }
        return cost;
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
