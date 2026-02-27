using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 자신에게 'n턴 동안' UnitState를 부여하는 스킬 (쇄국 1턴 용).
/// - 기존 SelfStateSkill은 무기한, 이건 턴 지속형.
/// </summary>
[CreateAssetMenu(menuName = "Battle/Skill/Player/Self/IsolationTimed", fileName = "CS_IsolationTimed")]
public class SelfIsolationTimedSkill : SkillAsset, ISelfCastSkill
{
    [Header("기본 방어 중첩 효과")]
    [Tooltip("기본 방어 상태 ID")]
    public StatusId baseDefenseStatusId = StatusId.Defense;
    [Min(1)] public int baseDefenseStacks = 3;
    [Min(1)] public int baseDefenseDurationTurns = 1;

    [Header("Training")]

    [Header("강제 이동 방지")]
    [Tooltip("이 스킬로 부여된 상태가 강제 이동을 막게 할지 여부")]
    public bool trainingPreventForcedMove = false;
    [Tooltip("강제 이동 방지 효과를 적용할 훈련 루트(-1이면 비활성)")]
    [Range(-1, 2)] public int routeForPreventForcedMove = -1;
    [Tooltip("강제 이동을 막는 데 사용할 상태 ID (예: MoveResist)")]
    public StatusId moveResistStatusId = StatusId.Fixing;
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
    void OnEnable()
    {
        targetMode = SkillTargetMode.Unit;
        costResource = SkillCostResource.MP;
    }

    int GetRoute(BattleUnit _caster)
    {
        if (!_caster) return -1;
        return _caster.GetTrainingRouteIndex(this);
    }

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int _originCell, bool _isOddRow)
    {
        // 자기 자신 타겟이라 프리뷰 필요 없음
        yield break;
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
        if (!_battlemanager) yield break;

        // 실제 캐스터는 항상 자기 자신
        _target = _caster;

        // MP 비용 계산 (훈련 반영)
        var res = GetCostResource(_caster);
        int cost = GetEffectiveCost(_caster);
        if (cost > 0 && !_caster.TryConsumeResource(res, cost)) yield break;

        var usc = _caster.GetComponent<UnitStateController>();
        if (usc == null)
            usc = _caster.gameObject.AddComponent<UnitStateController>();

        int route = GetRoute(_caster);

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

        // [Optimization] Use BattleManager Registry
        var bm = BattleManager.Instance;
        var allUnits = (bm != null) ? bm.ActiveUnits : System.Linq.Enumerable.Empty<BattleUnit>();

        // 방어 중첩 적용
        if (defId != StatusId.None)
        {
            foreach (var u in allUnits)
            {
                if (u == null || u.IsDead || u.IsRetreated) continue;

                // 아군만
                if (u.data.team != _caster.data.team) continue;

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

                // 아군만
                if (u.data.team != _caster.data.team) continue;

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

    public override IEnumerator ResolveOnTile(BattleManager _battlemanager, Tilemap _map, Vector3Int _originCell, BattleUnit _caster)
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
        return SkillTooltipUtil.AppendTrainingRouteDescription(
            baseDesc,
            info.title,
            info.description
        );
    }
}
