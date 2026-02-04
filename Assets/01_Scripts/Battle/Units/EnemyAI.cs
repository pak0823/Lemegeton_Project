using UnityEngine;

[DisallowMultipleComponent]
public class EnemyAI : MonoBehaviour
{
    [System.Serializable] public struct WeightedSO { public SkillAsset skill; public float weight; }
    public WeightedSO[] soSkills;
    public SkillAsset plannedSkill;

    public bool TryPickSkillSO(out SkillAsset so)
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
    public SkillAsset PlanNextSkill()
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
}
