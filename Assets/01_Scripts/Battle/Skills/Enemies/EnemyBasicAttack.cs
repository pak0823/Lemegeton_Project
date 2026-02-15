// Assets/Scripts/Skills/EnemyBasicAttack.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Battle/Skill/Enemy/BasicAttack")]
public class EnemyBasicAttack : EnemySkill
{
    [Header("Damage")]
    public float damageMultiplier = 1f; // 기본 배율(필요 시 조정)

    [Header("Bonus Condition (Optional)")]
    public StatusId bonusConditionStatus = StatusId.None; // 추가 피해를 주는 상태이상 조건

    [Tooltip("조건 만족 시 적용할 추가 배율 (기본값 1 = 없음, 예: 3 = 3배 데미지)")]
    [FormerlySerializedAs("slowBonusMultiplier")]
    public float bonusMultiplier = 1f; 

#if UNITY_EDITOR
    void OnValidate() { targetMode = SkillTargetMode.Unit; }
#endif
    void OnEnable()
    {
        targetMode = SkillTargetMode.Unit;
        school = DamageSchool.Physical;
    }

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddRow)
    {
        yield return originCell; // 단일 대상
    }

    public override float ComputeDamage(BattleUnit caster, BattleUnit target, in SkillRuntime ctx)
    {
        if (caster == null || target == null) return 0;

        float finalMult = damageMultiplier;

        // 조건부 추가 피해 로직
        if (bonusConditionStatus != StatusId.None)
        {
            var sc = target.GetComponent<StatusController>();
            if (sc != null && sc.Has(bonusConditionStatus))
            {
                finalMult *= bonusMultiplier;
            }
        }

        float raw = caster.STR * finalMult;
        return Mathf.Max(0, Mathf.FloorToInt(raw));
    }

    public override IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
    {
        if (caster == null || target == null || target.IsDead) yield break;

        // AI가 선택한 타겟(target)을 그대로 사용
        BattleUnit actualTarget = target;

        // 임팩트 등 처리
        bool impactDone = false;
        System.Action impact = null;
        impact = () =>
        {
            caster.OnAttackImpact -= impact;
            impactDone = true;

            if (actualTarget != null && !actualTarget.IsDead && bm != null)
            {
                var victims = new List<BattleUnit> { actualTarget };
                var map = actualTarget.CurrentMap ?? caster.CurrentMap;
                var originCell = actualTarget.Cell;
                bm.ExecuteSkillDamage(caster, victims, this, map, originCell);
            }
        };

        caster.OnAttackImpact += impact;
        yield return caster.AnimateAttack(actualTarget, null);

        if (!impactDone && actualTarget != null && !actualTarget.IsDead && bm != null)
        {
            var victims = new List<BattleUnit> { actualTarget };
            var map = actualTarget.CurrentMap ?? caster.CurrentMap;
            var originCell = actualTarget.Cell;
            bm.ExecuteSkillDamage(caster, victims, this, map, originCell);
        }
    }

    public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        yield break;
    }
}
