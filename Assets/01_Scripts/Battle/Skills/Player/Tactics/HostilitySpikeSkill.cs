using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Battle/Skills/Common/Hostility Spike", fileName = "HostilitySpikeSkill")]
public class HostilitySpikeSkill : SkillAsset, ISelfCastSkill
{
    [Header("Hostility Settings")]
    [Tooltip("참조 배수 (예: 5.0 = 최대 적의 * 5만큼 증가)")]
    public float referenceMultiplier = 5.0f;

    public bool SelfCastOnSelect => true;   // 선택 즉시 자기 자신에게 발동

    // ==== Training 공통 ====
    int GetRoute(BattleUnit caster)
    {
        if (!caster) return -1;
        return caster.GetTrainingRouteIndex(this);
    }

    [Header("Training / MP Cost")]
    [Tooltip("훈련에서 MP 비용을 덮어쓸지 여부")]
    public bool trainingUseMpOverride = false;

    [Tooltip("MP 비용 덮어쓰기 적용할 훈련 루트(-1이면 비활성, 0~2)")]
    [Range(-1, 2)] public int routeForMpOverride = -1;

    [Tooltip("해당 루트에서 사용할 MP 비용")]
    [Min(0)] public int trainingMpCostOverride = 0;

    [Header("Training / Defense Stack Buff")]
    [Tooltip("방어 중첩 상태를 부여할지 여부")]
    public bool trainingApplyDefenseStacks = false;

    [Tooltip("방어 중첩 상태를 적용할 훈련 루트(-1이면 비활성, 0~2)")]
    [Range(-1, 2)] public int routeForDefenseStacks = -1;

    [Tooltip("부여할 StatusId (StackableStatusVisualDB에 아이콘/이름 연결 가능)")]
    public StatusId trainingDefenseStatusId = StatusId.None;

    [Min(1)] public int trainingDefenseStacks = 1;
    [Min(1)] public int trainingDefenseDurationTurns = 2;

    [Header("Training / Free Action")]
    [Tooltip("특정 루트에서 이 스킬을 무료턴(행동 소모 없음)으로 만들지 여부")]
    public bool trainingUseFreeAction = false;

    [Tooltip("무료턴으로 사용할 훈련 루트(-1이면 비활성, 0~2)")]
    [Range(-1, 2)] public int routeForFreeAction = -1;

#if UNITY_EDITOR
    void OnValidate() { targetMode = SkillTargetMode.Unit; }
#endif

    void OnEnable()
    {
        targetMode = SkillTargetMode.Unit;
        power = 0f;                        // 피해 없음
        school = DamageSchool.Physical;    // 의미 거의 없음, 기본값
        costResource = SkillCostResource.MP;
    }

    public override int GetEffectiveCost(BattleUnit _caster)
    {
        int cost = base.GetEffectiveCost(_caster);
        if (!trainingUseMpOverride || !_caster) return cost;

        int route = GetRoute(_caster);
        if (routeForMpOverride >= 0 && route == routeForMpOverride)
        {
            return Mathf.Max(0, trainingMpCostOverride);
        }

        return cost;
    }

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int _originCell, bool _isOddRow)
    {
        // 프리뷰 불필요 → 빈 영역
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
        if (!_battlemanager || !_caster) yield break;

        //자원 소비 (훈련까지 반영된 실제 비용)
        var res = GetCostResource(_caster);
        int cost = GetEffectiveCost(_caster);
        if (cost > 0 && !_caster.TryConsumeResource(res, cost)) yield break;

        int route = GetRoute(_caster);

        // 같은 팀 유닛들 중 Hostility 최댓값 찾기
        float maxHost = 0f;
        foreach (var u in Object.FindObjectsOfType<BattleUnit>())
        {
            if (u == null || u.IsDead) continue;
            if (u.data.team != _caster.data.team) continue;    // 같은 편 기준

            maxHost = Mathf.Max(maxHost, Mathf.Max(0f, u.Hostility));
        }

        // 증가량 계산: maxHost * referenceMultiplier
        float delta = maxHost * Mathf.Max(0f, referenceMultiplier);
        if (delta > 0f)
            _caster.AddHostility(delta);

        // 훈련 효과 - 방어 중첩 상태 부여
        if (trainingApplyDefenseStacks &&
            routeForDefenseStacks >= 0 &&
            route == routeForDefenseStacks &&
            trainingDefenseStatusId != StatusId.None)
        {
            var sc = _caster.GetComponent<StatusController>();
            if (sc != null)
            {
                sc.ApplyWithTurnContext(
                    trainingDefenseStatusId,
                    Mathf.Max(1, trainingDefenseStacks),
                    Mathf.Max(1, trainingDefenseDurationTurns)
                );
            }
        }

        // 무료턴 여부는 BattleManager에서 routeForFreeAction 기준으로 처리

        yield break;
    }

    public override IEnumerator ResolveOnTile(BattleManager _battlemanager, Tilemap _map, Vector3Int _originCell, BattleUnit _caster)
    {
        // 타일 지목형 스킬이 아님
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
