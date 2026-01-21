// SkillAsset.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using static ParametricDamageSkill;

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
    public int trainingCost;  //해금 비용
}

public abstract class SkillAsset : ScriptableObject
{
    [Header("Display")]
    public string displayName;
    public Sprite descriptionImage; //스킬 범위 설명 이미지(패널에 표시)
    [TextArea] public string description;   // 스킬 설명(패널에 표시)

    [Header("Damage Type")]
    public DamageSchool school = DamageSchool.Physical;
    public AttackAttr attribute = AttackAttr.None;
    public float power = 1f; // 기본 배수(예: 1.0 = 원 공격력)

    [Header("Cost")]
    public SkillCostResource costResource = SkillCostResource.MP;
    [Tooltip("실제 사용되는 기본 비용 (MP/Rage 공통)")]
    [Min(0)] public int cost = 0;

    [HideInInspector]
    public int mpCost = 0;

    [Header("Targeting")]
    public SkillTargetMode targetMode; // 기존 enum 재사용 (Unit/Tile) 

    [Header("Targeting Rules")]
    [Tooltip("이 스킬을 누구에게 사용할 수 있는가?")]
    public SkillTargetAlignment targetAlignment = SkillTargetAlignment.Enemy; // 기본값은 적

    [Header("Cooldown")]
    [Tooltip("해당 스킬 사용 후 다시 사용할 때까지 필요한 '자신의 턴 수'. 0이면 쿨다운 없음.")]
    public int cooldownTurns = 0;

    [Header("Animation")]
    [Tooltip("이 스킬의 애니메이션 타입. 근접, 원거리, 자기 강화 등.")]
    public SkillAnimKind animKind = SkillAnimKind.Melee;

    [Tooltip("이 스킬의 기본 애니메이션 트리거 이름. 비워두면 타입별 기본값(Attack/Ranged/Casting 등)을 사용.")]
    public string animTriggerOverride;

    [Header("Melee / Gap Close")]
    [Tooltip("Unit 타겟 스킬일 때, 대상에게 뛰어가서 공격할지 여부. false면 제자리에서 시전.")]
    public bool useGapCloseJump = true;

    [Header("Compat")]
    public SkillId legacyId = SkillId.Skill1; // 기존 분기 로직 호환용

    [Header("Training")]
    [Tooltip("훈련 UI에 표시할 3개 루트의 제목/설명. 비어 있으면 기본 텍스트로 대체.")]
    public TrainingRouteInfo[] trainingRoutes = new TrainingRouteInfo[3];

    // BattleManager는 이 함수만 호출하고, 구체적인 절차(선택, 애니, 효과)는 스킬이 알아서 함.
    public virtual IEnumerator Execute(BattleManager _battlemanager, BattleUnit _caster, BattleUnit _targetUnit = null, Tilemap _targetMap = null, Vector3Int _targetCell = default)
    {
        // 기본 동작: 타겟 모드에 따라 단순 Resolve 호출
        // 특수 로직이 필요한 자식 클래스(넉백, 후퇴 등)는 이 함수를 override해서 쓴다.

        if (targetMode == SkillTargetMode.Unit)
        {
            if (_targetUnit != null)
            {
                // 공통 갭클로즈(접근) & 공격 연출 코루틴 호출
                yield return _battlemanager.PerformStandardUnitSkillFlow(this, _caster, _targetUnit);
            }
        }
        else // Tile Mode
        {
            if (_targetMap != null)
            {
                yield return _battlemanager.PerformStandardTileSkillFlow(this, _targetMap, _targetCell, _caster);
            }
        }
    }

    public static bool IsUntargetableByEnemy(BattleUnit _target)
    {
        if (_target == null || _target.IsDead) return true;

        var usc = _target.GetComponent<UnitStateController>();
        if (usc == null) return false;

        // 잠복(기존) + 연막 은신(신규 버프) 모두 동일하게 "타겟 지정 불가"
        return usc.Has(UnitStateId.Ambush) || usc.HasBuff(UnitStateBuffId.SmokeHidden);
    }

    // 기본값 0 = 제압 감소 없음
    public virtual int GetSuppressionOnHit(BattleUnit _caster) => 0;

    /// <summary>미리보기/피격판정을 위한 범위 셀 반환. origin = 대상 유닛 셀(유닛형) 또는 조준 셀(타일형)</summary>
    public abstract IEnumerable<Vector3Int> GetAreaCells(Vector3Int _originCell, bool _isOddRow);

    /// <summary>유닛 지목형 해결</summary>
    public abstract IEnumerator ResolveOnUnit(BattleManager _battlemanager, BattleUnit _caster, BattleUnit _target);

    /// <summary>타일 지목형 해결</summary>
    public abstract IEnumerator ResolveOnTile(BattleManager _battlemanager, Tilemap _map, Vector3Int _originCell, BattleUnit _caster);

    //스킬별 대미지 계산. 필요시 하위 클래스에서 override
    public virtual float ComputeDamage(BattleUnit _caster, BattleUnit _target, in SkillRuntime _skillruntime)
    {
        if (_caster == null)
            return 0f;

        // 기본 공격 스탯 선택
        float stat = 0f;
        switch (school)
        {
            case DamageSchool.Physical:
                stat = _caster.PhysicalDamage;
                break;

            case DamageSchool.Magical:
                stat = _caster.MagicDamage;
                break;

            case DamageSchool.Composite:
                // 고정 대미지 STR + MAG
                stat = _caster.PhysicalDamage + _caster.MagicDamage;
                break;

            default:
                stat = _caster.PhysicalDamage; // 혹은 0f
                break;
        }

        // 기술 위력 적용
        float dmg = stat * power;

        return dmg;
    }
    public virtual int GetBaseCost()
    {
        // 신규 cost를 우선, 0이면 기존 mpCost로 fallback
        if (cost > 0) return cost;
        return mpCost;
    }

    public static BattleUnit PickTargetByWeightedHostility(List<BattleUnit> _potentialTargets)
    {
        if (_potentialTargets == null || _potentialTargets.Count == 0) return null;
        if (_potentialTargets.Count == 1) return _potentialTargets[0];

        // 모든 대상의 Hostility 합계 계산
        float totalHostility = 0f;
        foreach (var unit in _potentialTargets)
        {
            totalHostility += Mathf.Max(0f, unit.Hostility);
        }

        // 합계가 0 이하면 (모두 적대감이 0), 랜덤으로 한 명 선택
        if (totalHostility <= 0)
        {
            return _potentialTargets[Random.Range(0, _potentialTargets.Count)];
        }

        // 0 ~ totalHostility 사이의 랜덤 값 선택
        float randomPoint = Random.Range(0, totalHostility);

        // 랜덤 값에서 각 유닛의 Hostility를 빼나가다가 0 이하가 되면 해당 유닛 선택
        foreach (var unit in _potentialTargets)
        {
            randomPoint -= Mathf.Max(0f, unit.Hostility);
            if (randomPoint <= 0)
            {
                return unit;
            }
        }

        // 만약의 경우(부동소수점 오류 등)를 대비해 마지막 유닛을 반환
        return _potentialTargets[_potentialTargets.Count - 1];
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
    public static BattleUnit PickPreferredStatusThenHighestHostility(IEnumerable<BattleUnit> _candidates, StatusId _preferred)
    {
        if (_candidates == null) return null;
        var list = _candidates
            .Where(u => u && u.team == Team.Player && !u.IsDead)
            .Where(u => !IsUntargetableByEnemy(u))   // 연막 은신/잠복 타겟 제외
            .ToList();
        if (list.Count == 0) return null;

        var slowed = list.Where(u =>
        {
            var sc = u.GetComponent<StatusController>();
            return sc != null && sc.Has(_preferred);
        }).ToList();

        var pool = (slowed.Count > 0) ? slowed : list;

        // 가시 감쇠 고려한 가중치 랜덤
        float total = 0f;
        foreach (var u in pool)
            total += Mathf.Max(0f, u.Hostility);

        if (total <= 0f) return pool[Random.Range(0, pool.Count)];

        float r = Random.Range(0, total);
        foreach (var u in pool)
        {
            r -= Mathf.Max(0f, u.Hostility);
            if (r <= 0f) return u;
        }
        return pool[pool.Count - 1];
        //if (slowed.Count > 0) return PickTargetByWeightedHostility(slowed);
        //return PickTargetByWeightedHostility(list);
    }
    public virtual string GetFullDescriptionRich(BattleUnit _caster)
    {
        var res = GetCostResource(_caster);
        int cost = GetEffectiveCost(_caster);

        if (cost <= 0) return description;

        string label = (res == SkillCostResource.MP) ? "MP" : "Rage";
        string color = (res == SkillCostResource.MP) ? "#00A2FF" : "#FF4B4B"; // 예시
        return $"{description}<size=20%><color=#808080>({label}:<color={color}>{cost}</color>)</color></size>";
    }

    public virtual SkillCostResource GetCostResource(BattleUnit caster) => costResource;

    public virtual bool ShouldGapCloseToTarget(BattleUnit _caster, BattleUnit _target)    /// Unit 타겟 사용 시 점프 연출(gap close)을 사용할지 여부
    {
        return useGapCloseJump;
    }
    public virtual int GetEffectiveCost(BattleUnit _caster)
    {
        // 기본값: base cost (MP/Rage 공통)
        return Mathf.Max(0, GetBaseCost());
    }
    public virtual int GetEffectiveCooldownTurns(BattleUnit _caster)
    {
        // 기본은 그냥 설정값 그대로
        return cooldownTurns;
    }

}