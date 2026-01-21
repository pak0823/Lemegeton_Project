using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 자신을 중심으로 주변 2칸에 '야수의 영역'을 2턴 동안 생성.
/// - 영역은 스킬을 사용한 그 위치에 고정
/// - 이 스킬을 사용한 유닛은 영역 안에서 이동할 때 행동을 소비하지 않음(턴이 끝나지 않음)
/// </summary>
[CreateAssetMenu(
    menuName = "Battle/Skills/Zone/Self Beast Domain",
    fileName = "SelfBeastDomainSkill")]
public class SelfBeastDomainSkill : SkillAsset, ISelfCastSkill
{
    [Header("지속 턴(시전자 기준 턴 수)")]
    public int durationTurns = 2;

    [Header("영역 반경 (타일 거리)")]
    public int radius = 2;

    public bool SelfCastOnSelect => true;

    [Header("Training")]
    [Header("영역 내 아군 저항 중첩 부여")]
    [Tooltip("영역 내 저항 중첩을 1 부여할지 여부")]
    public bool trainingGiveResistanceOnCast = false;
    [Tooltip("저항 중첩 부여를 적용할 훈련 루트(-1이면 비활성, 0~2)")]
    [Range(-1, 2)] public int routeForResistanceOnCast = -1;
    [Tooltip("부여할 저항 중첩 수 (기본 1)")]
    [Min(0)] public int resistanceStacksOnCast = 1;
    [Tooltip("저항 상태 지속 턴 수(예: 영역 지속과 동일하게 하고 싶으면 durationTurns에 맞춰 수동 설정)")]
    [Min(1)] public int resistanceDurationTurns = 1;

    [Header("영역 내 턴 시작 분노 감소")]
    [Tooltip("영역 안에서 차례가 올 때 분노를 감소시킬지 여부")]
    public bool trainingReduceRageOnTurnStart = false;
    [Tooltip("분노 감소 효과를 적용할 훈련 루트(-1이면 비활성, 0~2)")]
    [Range(-1, 2)] public int routeForRageReduceOnTurnStart = -1;
    [Tooltip("CLV × rageReducePerClv만큼 Rage 감소 (CLV 계산은 BattleManager 쪽에서 구현)")]
    public float rageReducePerClv = 0.40f;

    [Header("무료 행동")]
    [Tooltip("이 스킬 사용 시 턴을 마치지 않는 무료 행동으로 만들지 여부")]
    public bool trainingUseFreeAction = false;
    [Tooltip("무료 행동으로 처리할 훈련 루트(-1이면 비활성, 0~2)")]
    [Range(-1, 2)] public int routeForFreeAction = -1;

#if UNITY_EDITOR
    void OnValidate()
    {
        targetMode = SkillTargetMode.Unit;
    }
#endif

    void OnEnable()
    {
        // 자기 자신 대상, 데미지는 없지만 기본 물리 스쿨로 맞춰둠
        targetMode = SkillTargetMode.Unit;
        school = DamageSchool.Physical;
        costResource = SkillCostResource.MP;
    }

    int GetRoute(BattleUnit _caster)
    {
        if (!_caster) return -1;
        return _caster.GetTrainingRouteIndex(this);
    }

    /// <summary>
    /// 범위 프리뷰: 캐스터 위치 기준 반경 2 원형.
    /// 실제로는 ResolveOnUnit에서 BattleManager 쪽에 영역을 등록한다.
    /// </summary>
    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int _originCell, bool _ /*unused*/)
    {
        foreach (var c in AreaShapes.BeastDomainArea(_originCell, radius))
            yield return c;
    }

    public override int GetEffectiveCost(BattleUnit _caster)
    {
        return base.GetEffectiveCost(_caster);
    }

    public override IEnumerator Execute(BattleManager bm, BattleUnit caster, BattleUnit targetUnit, Tilemap targetMap, Vector3Int targetCell)
    {
        // 타겟팅 과정 없이, 즉시 자기 자신(caster)을 대상으로 실행 흐름 진입
        // PerformStandardUnitSkillFlow가 애니메이션 -> ResolveOnUnit 호출을 다 해줌
        yield return bm.PerformStandardUnitSkillFlow(this, caster, caster);
    }

    public override IEnumerator ResolveOnUnit(BattleManager _battlemanager, BattleUnit _caster, BattleUnit _target)
    {
        if (!_battlemanager) yield break;
        if (!_caster) yield break;

        _target = _caster;

        // MP 소모
        var res = GetCostResource(_caster);
        int cost = GetEffectiveCost(_caster);
        if (cost > 0 && !_caster.TryConsumeResource(res, cost)) yield break;

        // 캐스터가 바인드된 타일맵과 셀을 그대로 사용
        Tilemap map = _caster.CurrentMap;
        if (!map)
        {
            Debug.LogWarning("[BeastDomain] 캐스터의 CurrentMap이 없습니다.");
            yield break;
        }

        Vector3Int originCell = _caster.Cell;
        if (!map.HasTile(originCell))
        {
            Debug.LogWarning($"[BeastDomain] 중심 셀에 타일이 없습니다: {originCell}");
            yield break;
        }

        // BattleManager에 영역 생성 요청
        _battlemanager.fieldManager.SpawnBeastDomainZone(map, _caster, originCell, radius, durationTurns);

        // 훈련: 스킬 사용 시 자신에게 저항 부여
        int route = GetRoute(_caster);
        if (trainingGiveResistanceOnCast &&
            routeForResistanceOnCast >= 0 &&
            route == routeForResistanceOnCast &&
            resistanceStacksOnCast > 0)
        {
            var sc = _caster.GetComponent<StatusController>();
            if (sc != null)
            {
                sc.ApplyWithTurnContext(
                    StatusId.Resistance,
                    Mathf.Max(1, resistanceStacksOnCast),
                    Mathf.Max(1, resistanceDurationTurns)
                );
                Debug.Log($"[BeastDomain] 저항 훈련: {_caster.name} 자신에게 Resistance {resistanceStacksOnCast}스택, {resistanceDurationTurns}턴 부여");
            }
        }

        Debug.Log($"[BeastDomain] {_caster.name}가 야수의 영역을 생성함 (중심:{originCell}, 반경:{radius}, 지속:{durationTurns}턴)");

        yield break;
    }

    public override IEnumerator ResolveOnTile(BattleManager _battlemanager, Tilemap _map, Vector3Int _originCell, BattleUnit _caster)
    {
        // 이 스킬은 타일 지정이 아니라 자기 자신 대상이라 여기서는 아무것도 안 함
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
