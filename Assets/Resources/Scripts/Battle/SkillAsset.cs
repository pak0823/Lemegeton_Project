// SkillAsset.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum DamageSchool { Physical, Magical, Composite }  //근력, 총명 , 복합
public enum AttackAttr { None, Pierce, Strike, Slash }
public enum TargetPriorityMode
{
    None,
    RandomSurvivor,
    HighestHostility,
    PreferredStatusThenHighestHostility,  // 예: Slow 우선 → 그 안에서 적대감 최고
}

public struct SkillRuntime
{
    public Tilemap map;
    public Vector3Int originCell;   // 시전자 조준/타겟 기준 셀
    public Vector3Int casterCell;   // 시전자 현재 셀
    public Vector3Int targetCell;   // 피격자 셀
}

[System.Serializable]
public struct TrainingRouteInfo
{
    public string title;
    [TextArea] public string description;
}

public abstract class SkillAsset : ScriptableObject
{
    [Header("Display")]
    public string displayName;
    public Sprite descriptionImage; //스킬 범위 설명 이미지(패널에 표시)
    [TextArea] public string description;   // 스킬 설명(패널에 표시)

    [Header("Damage (default)")]
    public DamageSchool school = DamageSchool.Physical;
    public AttackAttr attribute = AttackAttr.None;
    public float power = 1f; // 기본 배수(예: 1.0 = 원 공격력)

    [Header("Cost")]
    [Min(0)] public int mpCost = 0;

    [Header("Targeting")]
    public SkillTargetMode targetMode; // 기존 enum 재사용 (Unit/Tile) 

    [Header("Cooldown")]
    [Tooltip("해당 스킬 사용 후 다시 사용할 때까지 필요한 '자신의 턴 수'. 0이면 쿨다운 없음.")]
    public int cooldownTurns = 0;

    [Header("Melee / Gap Close")]
    [Tooltip("Unit 타겟 스킬일 때, 대상에게 뛰어가서 공격할지 여부. false면 제자리에서 시전.")]
    public bool useGapCloseJump = true;

    [Header("Compat (임시)")]
    public SkillId legacyId = SkillId.Skill1; // 기존 분기 로직 호환용

    [Header("Training (UI only)")]
    [Tooltip("훈련 UI에 표시할 3개 루트의 제목/설명. 비어 있으면 기본 텍스트로 대체.")]
    public TrainingRouteInfo[] trainingRoutes = new TrainingRouteInfo[3];

    // 기본값 0 = 제압 감소 없음
    public virtual int GetSuppressionOnHit(BattleUnit caster) => 0;

    /// <summary>미리보기/피격판정을 위한 범위 셀 반환. origin = 대상 유닛 셀(유닛형) 또는 조준 셀(타일형)</summary>
    public abstract IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddRow);

    /// <summary>유닛 지목형 해결</summary>
    public abstract IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target);

    /// <summary>타일 지목형 해결</summary>
    public abstract IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster);

    //스킬별 대미지 계산. 필요시 하위 클래스에서 override
    public virtual int ComputeDamage(BattleUnit caster, BattleUnit target, in SkillRuntime ctx)
    {
        int baseStat;

        switch (school)
        {
            case DamageSchool.Physical:
                baseStat = Mathf.Max(1, caster.PhysicalDamage);
                break;

            case DamageSchool.Magical:
                baseStat = Mathf.Max(1, caster.MagicDamage);
                break;

            case DamageSchool.Composite:
                // 물리 + 마법 합산
                baseStat = Mathf.Max(1, caster.PhysicalDamage + caster.MagicDamage);
                break;

            default:
                baseStat = Mathf.Max(1, caster.PhysicalDamage);
                break;
        }

        float mult = Mathf.Max(0f, power);

        if (attribute != AttackAttr.None && target != null && target.resistTable != null)
        {
            foreach (var mod in target.resistTable)
                if (mod.attr == attribute) { mult *= Mathf.Max(0f, mod.mult); break; }
        }

        return Mathf.Max(1, Mathf.RoundToInt(baseStat * mult));
    }

    public static BattleUnit PickTargetByWeightedHostility(List<BattleUnit> potentialTargets)
    {
        if (potentialTargets == null || potentialTargets.Count == 0) return null;
        if (potentialTargets.Count == 1) return potentialTargets[0];

        // 모든 대상의 Hostility 합계 계산
        float totalHostility = 0f;
        foreach (var unit in potentialTargets)
        {
            float visMult = SmokeZoneRuntime.GetVisibilityMultiplier(unit);
            totalHostility += Mathf.Max(0f, unit.Hostility * visMult);
        }

        // 합계가 0 이하면 (모두 적대감이 0), 랜덤으로 한 명 선택
        if (totalHostility <= 0)
        {
            return potentialTargets[Random.Range(0, potentialTargets.Count)];
        }

        // 0 ~ totalHostility 사이의 랜덤 값 선택
        float randomPoint = Random.Range(0, totalHostility);

        // 랜덤 값에서 각 유닛의 Hostility를 빼나가다가 0 이하가 되면 해당 유닛 선택
        foreach (var unit in potentialTargets)
        {
            randomPoint -= unit.Hostility;
            if (randomPoint <= 0)
            {
                return unit;
            }
        }

        // 만약의 경우(부동소수점 오류 등)를 대비해 마지막 유닛을 반환
        return potentialTargets[potentialTargets.Count - 1];
    }

    // <summary>컬렉션에서 '적대감(Hostility)'이 가장 높은 플레이어 유닛을 선택. 동률이면 랜덤.</summary>
    //public static BattleUnit PickHighestHostility(IEnumerable<BattleUnit> candidates)
    //{
    //    if (candidates == null) return null;
    //    // 생존한 플레이어만
    //    var list = candidates.Where(u => u != null && u.team == Team.Player && !u.IsDead).ToList();
    //    if (list.Count == 0) return null;

    //    float max = list.Max(u => Mathf.Max(0, u.Hostility));
    //    var top = list.Where(u => Mathf.Max(0, u.Hostility) == max).ToList();

    //    return top[Random.Range(0, top.Count)];
    //}
    public static BattleUnit PickPreferredStatusThenHighestHostility(IEnumerable<BattleUnit> candidates, StatusId preferred)
    {
        if (candidates == null) return null;
        var list = candidates.Where(u => u && u.team == Team.Player && !u.IsDead).ToList();
        if (list.Count == 0) return null;

        var slowed = list.Where(u =>
        {
            var sc = u.GetComponent<StatusController>();
            return sc != null && sc.Has(preferred);
        }).ToList();

        var pool = (slowed.Count > 0) ? slowed : list;

        // 가시 감쇠 고려한 가중치 랜덤
        float total = 0f;
        foreach (var u in pool)
            total += Mathf.Max(0f, u.Hostility * SmokeZoneRuntime.GetVisibilityMultiplier(u));

        if (total <= 0f) return pool[Random.Range(0, pool.Count)];

        float r = Random.Range(0, total);
        foreach (var u in pool)
        {
            r -= Mathf.Max(0f, u.Hostility * SmokeZoneRuntime.GetVisibilityMultiplier(u));
            if (r <= 0f) return u;
        }
        return pool[pool.Count - 1];
        //if (slowed.Count > 0) return PickTargetByWeightedHostility(slowed);
        //return PickTargetByWeightedHostility(list);
    }
    public virtual string GetFullDescriptionRich()
    {
        // description 끝에 (MP:00) 추가
        if (mpCost > 0)
        {
            string mpColor = "#00A2FF"; // 밝은 파란색
            return $"{description}<size=20%><color=#808080>(MP:<color={mpColor}>{mpCost}</color>)</color></size>";
        }
        else
        {
            return description;
        }
    }
    public virtual bool ShouldGapCloseToTarget(BattleUnit caster, BattleUnit target)    /// Unit 타겟 사용 시 점프 연출(gap close)을 사용할지 여부
    {
        return useGapCloseJump;
    }
    public virtual int GetEffectiveMpCost(BattleUnit caster)
    {
        // 기본값은 그냥 mpCost 그대로
        return mpCost;
    }
    public virtual string GetFullDescriptionRich(BattleUnit caster)
    {
        // 기본적으로는 캐스터 정보 필요 없으면 기존 구현 재사용
        return GetFullDescriptionRich();
    }
}