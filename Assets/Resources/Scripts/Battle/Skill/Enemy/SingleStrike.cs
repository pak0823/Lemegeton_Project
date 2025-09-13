// Assets/Scripts/Skills/EA_SingleStrike.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Battle/SkillAsset/Enemy/SingleStrike")]
public class SingleStrike : SkillAsset
{
    [Header("Damage")]
    public float damageMultiplier = 1f; // 기본 배율(필요 시 조정)
    [Tooltip("둔화 보유자에게 적용할 추가 배수")]
    public float slowBonusMultiplier = 3f; // 요구사항: 현재 물공 * 3배

#if UNITY_EDITOR
    void OnValidate() { targetMode = SkillTargetMode.Unit; }  // 에디터에서 항상 Unit로 고정
#endif

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddRow)
    {
        yield return originCell; // 단일 대상이므로 원점 셀만
    }

    public override IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
    {
        if (caster == null) yield break;

        // 생존 플레이어 수집
        var players = Object.FindObjectsOfType<BattleUnit>()
            .Where(u => u != null && u.team == Team.Player && !u.IsDead)
            .ToList();
        if (players.Count == 0) yield break;

        // 둔화 보유자 우선 선정
        BattleUnit actualTarget = target; // 기본 = BM이 건네준 대상
        var slowed = players.Where(u => {
            var sc = u.GetComponent<StatusController>();
            return sc != null && sc.Has(StatusId.Slow);
        }).ToList();

        if (slowed.Count > 0)
        {
            // 둔화 대상이 하나 이상이면 그중에서 무작위 1인 우선 공격
            actualTarget = slowed[Random.Range(0, slowed.Count)];
        }
        else
        {
            // 둔화 대상이 없다면 기존 타겟이 null/사망일 수 있으므로 보정
            if (actualTarget == null || actualTarget.IsDead)
                actualTarget = players[Random.Range(0, players.Count)];
        }

        if (actualTarget == null || actualTarget.IsDead) yield break;

        // 임팩트 타이밍에 대미지 적용 (둔화 대상이면 3배)
        bool impactDone = false;
        System.Action impact = null;
        impact = () =>
        {
            caster.OnAttackImpact -= impact;
            impactDone = true;

            if (actualTarget != null && !actualTarget.IsDead)
            {
                actualTarget.PlayHit();

                bool targetHasSlow = false;
                var sc = actualTarget.GetComponent<StatusController>();
                if (sc != null) targetHasSlow = sc.Has(StatusId.Slow);

                float mult = damageMultiplier * (targetHasSlow ? slowBonusMultiplier : 1f);
                int dmg = Mathf.Max(1, Mathf.RoundToInt(caster.PhysicalDamage * mult));
                actualTarget.TakeDamage(dmg);
            }
        };

        caster.OnAttackImpact += impact;
        yield return caster.AnimateAttack(actualTarget);

        // 안전장치: 애니 이벤트 누락 시 폴백
        if (!impactDone && actualTarget != null && !actualTarget.IsDead)
        {
            actualTarget.PlayHit();

            bool targetHasSlow = false;
            var sc = actualTarget.GetComponent<StatusController>();
            if (sc != null) targetHasSlow = sc.Has(StatusId.Slow);

            float mult = damageMultiplier * (targetHasSlow ? slowBonusMultiplier : 1f);
            int dmg = Mathf.Max(1, Mathf.RoundToInt(caster.PhysicalDamage * mult));
            actualTarget.TakeDamage(dmg);
        }
    }
    public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        yield break; // Unit형이므로 사용 안함
    }
}
