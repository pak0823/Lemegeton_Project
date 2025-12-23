using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 자신에게 1턴 동안 잠복 상태를 부여하는 스킬.
/// - 적은 이 유닛을 타겟으로 지정할 수 없음
/// - AGI 0.40, INS 4.00 (StateStatModifierDB에서 설정)
/// - 노을(이 스킬 보유 유닛)이 공격하면 잠복 해제
/// 훈련:
///  0번: 자원 소모 감소(전용 MP 코스트 override)
///  1번: 상태가 민첩을 약화하지 않음(AGI 페널티 상쇄 버프 부여)
///  2번: 스킬 사용 시 자신 적의 감소(0.40배)
///  + 상태 유지 중 자신의 차례 시작 시 생명 회복(CLV×4.00 → 임시로 BDY×4)
/// </summary>
[CreateAssetMenu(
    menuName = "Battle/Skills/State/Self Ambush",
    fileName = "SelfAmbushSkill")]
public class SelfAmbushSkill : SkillAsset, ISelfCastSkill
{
    [Header("소모 자원 감소")]
    public bool trainingUseMpOverride = false;
    [Range(-1, 2)]
    [Tooltip("MP 감소가 적용될 훈련 루트 인덱스 (-1이면 비활성)")]
    public int routeForMpOverride = 0;
    public int trainingMpCost = 2;

    [Header("약화 감소 제거")]
    public bool trainingNoAgiPenalty = true;
    [Range(-1, 2)]
    [Tooltip("민첩 약화 제거가 적용될 훈련 루트 인덱스 (-1이면 비활성)")]
    public int routeForNoAgiPenalty = 1;

    [Header("적의 감소")]
    [Tooltip("훈련 3번: 적의 감소 배율 (0.40)")]
    public bool trainingHostilityDown = true;
    [Range(-1, 2)]
    [Tooltip("적의 감소가 적용될 훈련 루트 인덱스 (-1이면 비활성)")]
    public int routeForHostilityDown = 2;
    public float hostilityMultiplier = 0.40f;

    [Header("턴 시작 회복")]
    [Tooltip("잠복 상태가 유지되는 동안, 자신의 턴 시작 시 회복을 줄지 여부")]
    public bool trainingHealOnTurnStart = true;
    [Range(-1, 2)]
    [Tooltip("턴 시작 회복이 적용될 훈련 루트 인덱스 (-1이면 비활성)")]
    public int routeForHealOnTurnStart = -1;
    public float healPerClv = 4.0f;

    public bool SelfCastOnSelect => true;

#if UNITY_EDITOR
    void OnValidate()
    {
        targetMode = SkillTargetMode.Unit;
    }
#endif

    void OnEnable()
    {
        targetMode = SkillTargetMode.Unit;
        school = DamageSchool.Physical; // 실제 피해 없음이지만, 호환성용 기본값
        costResource = SkillCostResource.MP;
    }

    int GetRoute(BattleUnit caster)
    {
        if (!caster) return -1;
        return caster.GetTrainingRouteIndex(this);
    }

    public override System.Collections.Generic.IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddRow)
    {
        // 자기 자신 대상 스킬이라 범위 프리뷰는 필요 없음
        yield break;
    }

    public override int GetEffectiveCost(BattleUnit caster)
    {
        int baseCost = base.GetEffectiveCost(caster);
        if (caster == null) return baseCost;

        int route = GetRoute(caster);

        if (trainingUseMpOverride &&
            routeForMpOverride >= 0 &&
            route == routeForMpOverride)
        {
            return trainingMpCost;
        }

        return baseCost;
    }

    /// <summary>
    /// CLV × 4.00 을 계산하는 자리.
    /// 현재 코드에 CLV 스탯이 없어, 임시로 caster.BDY 를 사용하고 있음.
    /// CLV 필드가 생기면 이 메서드 안만 수정해도 됨.
    /// </summary>
    public int ComputeTurnStartHeal(BattleUnit caster)
    {
        if (!caster) return 0;

        int amount = Mathf.Max(1, Mathf.FloorToInt(caster.MagicDamage * healPerClv));
        return amount;
    }

    /// <summary>
    /// 잠복 상태 해제 트리거: 이 유닛이 공격(실제 피해를 입힘)하면 잠복 제거.
    /// </summary>
    void RegisterBreakOnAttack(BattleUnit caster)
    {
        if (!caster) return;

        System.Action<BattleUnit, BattleUnit, int, SkillAsset> handler = null;
        handler = (dealer, victim, damage, source) =>
        {
            if (dealer != caster) return;

            var usc = caster.GetComponent<UnitStateController>();
            if (usc == null)
            {
                caster.OnDealtDamage -= handler;
                return;
            }

            if (usc.Has(UnitStateId.Ambush))
            {
                usc.Remove(UnitStateId.Ambush);
                Debug.Log($"[Ambush] {caster.name}가 공격하여 잠복 상태가 해제되었습니다.");
            }

            // 한 번 발동 후 더 이상 듣지 않음
            caster.OnDealtDamage -= handler;
        };

        caster.OnDealtDamage += handler;
    }

    public override IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
    {
        if (!caster) yield break;
        if (!bm) yield break;

        // 자기 자신
        target = caster;

        // 자원 소비 (훈련 반영)
        var res = GetCostResource(caster);
        int cost = GetEffectiveCost(caster);
        if (cost > 0 && !caster.TryConsumeResource(res, cost))
            yield break;

        // 상태 컨트롤러 확보
        var usc = caster.GetComponent<UnitStateController>();
        if (usc == null)
            usc = caster.gameObject.AddComponent<UnitStateController>();

        // 잠복 상태 부여
        bool added = usc.Apply(UnitStateId.Ambush);
        if (added)
        {
            Debug.Log($"[Ambush] {caster.name}에게 잠복 상태 부여 (공격 시까지 유지)");
        }

        int route = GetRoute(caster);

        // 민첩 약화 없음 → AGI 페널티 상쇄 버프 부여
        if (trainingNoAgiPenalty &&
            routeForNoAgiPenalty >= 0 &&
            route == routeForNoAgiPenalty)
        {
            if (usc.ApplyBuff(UnitStateBuffId.AmbushAgiCancel))
            {
                Debug.Log($"[Ambush] Route(NoAgi={routeForNoAgiPenalty}): {caster.name} 잠복 AGI 페널티 상쇄 버프 적용");
            }
        }

        // 적의 감소 (0.40 배)
        if (trainingHostilityDown &&
            routeForHostilityDown >= 0 &&
            route == routeForHostilityDown)
        {
            float before = caster.Hostility;
            float targetHost = Mathf.Max(0f, before * hostilityMultiplier);
            float delta = targetHost - before;
            caster.AddHostility(delta);
            Debug.Log($"[Ambush] Route(HostDown={routeForHostilityDown}): {caster.name} 적의 감소 {before:F2} → {targetHost:F2}");
        }

        // 공격 시 잠복 해제 트리거 등록
        RegisterBreakOnAttack(caster);

        yield break;
    }

    public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
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
