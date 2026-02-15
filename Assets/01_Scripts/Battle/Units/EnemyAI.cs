using UnityEngine;
using System.Linq;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class EnemyAI : MonoBehaviour
{
    [System.Serializable] public struct WeightedSO { public SkillAsset skill; public float weight; }
    public WeightedSO[] soSkills;
    public SkillAsset plannedSkill;

    // [Refactor] 가상 메서드로 변경하여 자식 클래스에서 오버라이드 가능하도록 함
    public virtual bool TryPickSkillSO(out SkillAsset so)
    {
        so = null;
        if (soSkills == null || soSkills.Length == 0) return false;
        float sum = 0f; foreach (var e in soSkills) sum += Mathf.Max(0f, e.weight);
        if (sum <= 0f) { so = soSkills[Random.Range(0, soSkills.Length)].skill; return so != null; }
        float r = Random.value * sum; float acc = 0f;
        foreach (var e in soSkills) { acc += Mathf.Max(0f, e.weight); if (r <= acc) { so = e.skill; return so != null; } }
        return false;
    }

    // 다음 턴용 스킬을 미리 선정하여 보관하고 반환
    public virtual SkillAsset PlanNextSkill()
    {
        if (!TryPickSkillSO(out plannedSkill))
            plannedSkill = null;
        return plannedSkill;
    }

    // 실행 시점에 꺼내 쓰는 헬퍼(없으면 즉시 뽑기)
    public SkillAsset ConsumePlannedSkillOrPick()
    {
        var so = plannedSkill;
        plannedSkill = null; // 한 번 쓰면 비움
        if (so == null) TryPickSkillSO(out so);
        return so;
    }

    // [New] 타겟 선정 로직 (기본: Hostility 가중치 랜덤)
    public virtual BattleUnit SelectBestTarget(SkillAsset skill)
    {
        if (skill == null) return null;

        // 1. 생존한 플레이어 목록 가져오기
        var candidates = GetAlivePlayers();
        if (candidates.Count == 0) return null;

        // 2. EnemySkill의 Preference에 따른 분기
        if (skill is EnemySkill enemySkill)
        {
            switch (enemySkill.targetPreference)
            {
                case SkillTargetPreference.LowestHP:
                    return candidates.OrderBy(u => u.HP).FirstOrDefault();
                
                case SkillTargetPreference.Closest:
                    return GetClosestUnit(candidates);
                
                case SkillTargetPreference.Random:
                    return candidates[Random.Range(0, candidates.Count)];

                case SkillTargetPreference.HighestHostility:
                default:
                    // 기존 Hostility 기반 가중치 랜덤
                    return SkillAsset.PickTargetByWeightedHostility(candidates);
            }
        }

        // 기본값: Hostility 기반
        return SkillAsset.PickTargetByWeightedHostility(candidates);
    }

    protected List<BattleUnit> GetAlivePlayers()
    {
        return FindObjectsOfType<BattleUnit>()
            .Where(u => u != null && u.data.team == Team.Player && !u.IsDead)
            .Where(u => !SkillAsset.IsUntargetableByEnemy(u)) 
            .ToList();
    }

    protected BattleUnit GetClosestUnit(List<BattleUnit> candidates)
    {
        BattleUnit best = null;
        float minDistSq = float.MaxValue;
        Vector3 myPos = transform.position;

        foreach (var u in candidates)
        {
            float d = (u.transform.position - myPos).sqrMagnitude;
            if (d < minDistSq)
            {
                minDistSq = d;
                best = u;
            }
        }
        return best;
    }
}
