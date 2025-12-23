using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Battle/Skills/State/Self Vigilance", fileName = "SelfVigilanceSkill")]
public class SelfVigilanceSkill : SkillAsset, ISelfCastSkill
{
    public int durationTurns = 1;
    public bool SelfCastOnSelect => true;

    [Header("Training")]
    [Header("자원 소모 감소")]
    [Tooltip("훈련에서 Rage 비용을 덮어쓸지 여부")]
    public bool trainingUseRageOverride = false;
    [Tooltip("Rage 비용 덮어쓰기를 적용할 훈련 루트(-1이면 비활성, 0~2)")]
    [Range(-1, 2)] public int routeForRageOverride = -1;
    [Tooltip("해당 루트에서 사용할 Rage 비용")]
    [Min(0)] public int trainingRageCostOverride = 0;

    [Header("통찰 약화 부여")]
    [Tooltip("특정 루트에서 '자신을 공격한 적'에게 통찰 약화를 줄지 여부")]
    public bool trainingUseInsightDebuff = false;
    [Tooltip("통찰 약화 효과를 적용할 훈련 루트(-1이면 비활성, 0~2)")]
    [Range(-1, 2)] public int routeForInsightDebuff = -1;

    [Header("적의 증가")]
    [Tooltip("특정 루트에서 적의를 크게 올릴지 여부")]
    public bool trainingUseHostilitySpike = false;
    [Tooltip("적의 증가 효과를 적용할 훈련 루트(-1이면 비활성, 0~2)")]
    [Range(-1, 2)] public int routeForHostilitySpike = -1;
    [Tooltip("참조 배수 (예: 5.0 = 최대 적의 * 5만큼 증가)")]
    public float hostilityReferenceMultiplier = 5.0f;

#if UNITY_EDITOR
    void OnValidate() { targetMode = SkillTargetMode.Unit; }
#endif
    void OnEnable() 
    { 
        targetMode = SkillTargetMode.Unit;
        school = DamageSchool.Physical;
        costResource = SkillCostResource.Rage;
    }

    int GetRoute(BattleUnit caster)
    {
        if (!caster) return -1;
        return caster.GetTrainingRouteIndex(this);
    }

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddRow)
    {
        yield break;
    }
    public override int GetEffectiveCost(BattleUnit caster)
    {
        int baseCost = base.GetEffectiveCost(caster);
        if (!trainingUseRageOverride || !caster) return baseCost;

        int route = GetRoute(caster);
        if (routeForRageOverride >= 0 && route == routeForRageOverride)
            return Mathf.Max(0, trainingRageCostOverride);

        return baseCost;
    }

    public override IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
    {
        if (!bm || !caster) yield break;

        // 항상 자기 자신에게
        target = caster;

        // 자원 소비 (훈련 반영)
        var res = GetCostResource(caster);
        int cost = GetEffectiveCost(caster);
        if (cost > 0 && !caster.TryConsumeResource(res, cost))
            yield break;

        var usc = caster.GetComponent<UnitStateController>();
        if (usc == null)
        {
            Debug.LogError("[Vigilance] UnitStateController 없는 유닛에 경계를 적용하려 했습니다. 프리팹에 UnitStateController를 붙이세요.");
            yield break;
        }

        // 1턴 지속 경계 상태 부여
        usc.ApplyForTurns(UnitStateId.Guard, durationTurns);

        int route = GetRoute(caster);

        // 적의 증가 (최고 Hostility × 배수)
        if (trainingUseHostilitySpike &&
            routeForHostilitySpike >= 0 &&
            route == routeForHostilitySpike)
        {
            float maxHost = 0f;
            foreach (var u in Object.FindObjectsOfType<BattleUnit>())
            {
                if (u == null || u.IsDead) continue;
                if (u.team != caster.team) continue; // 같은 편 기준(설정에 따라 바꿀 수 있음)

                maxHost = Mathf.Max(maxHost, Mathf.Max(0f, u.Hostility));
            }

            float delta = maxHost * Mathf.Max(0f, hostilityReferenceMultiplier);
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
        string baseDesc = base.GetFullDescriptionRich(caster);

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
