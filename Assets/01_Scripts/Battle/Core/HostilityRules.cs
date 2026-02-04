using UnityEngine;

public static class HostilityRules
{
    /// <summary>피해 기반 적대감: dmg * (1 + 대상의 체력손실비율) * 보스배수 * 시전자상태배수</summary>
    public static float FromDamage(int dmg, BattleUnit caster, BattleUnit target)
    {
        if (caster == null || target == null) return 0f;
        float missingHpRatio = 1f - ((float)target.HP / Mathf.Max(1, target.MaxHP));
        float healthMultiplier = 1f + missingHpRatio;
        float bossScaling = (target.data.isBoss == ISBOSS.Boss) ? 2.0f : 1.0f;
        float statusMultiplier = caster.HostilityGenerationMultiplier;
        return Mathf.Max(0f, dmg) * healthMultiplier * bossScaling * statusMultiplier;
    }

    /// <summary>회복 기반 적대감: heal * (1 + 시전자 체력손실비율) * 시전자상태배수</summary>
    public static float FromHeal(int healAmount, BattleUnit caster)
    {
        if (caster == null) return 0f;
        float missingHpRatio = 1f - ((float)caster.HP / Mathf.Max(1, caster.MaxHP));
        float healthMultiplier = 1f + missingHpRatio;
        float statusMultiplier = caster.HostilityGenerationMultiplier;
        return Mathf.Max(0f, healAmount) * healthMultiplier * statusMultiplier;
    }

    public static float GetVisibleHostility(BattleUnit _unit)
    {
        float baseHostility = _unit.Hostility; // 원본(변하지 않음)
        return baseHostility;
    }
}
