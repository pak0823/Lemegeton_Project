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
    void OnEnable()
    {
        targetMode = SkillTargetMode.Unit;
        school = DamageSchool.Physical;
        // power는 ComputeDamage에서 안 쓰니 0 또는 1 아무거나 상관 없음
    }

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddRow)
    {
        yield return originCell; // 단일 대상이므로 원점 셀만
    }

    public override int ComputeDamage(BattleUnit caster, BattleUnit target, in SkillRuntime ctx)
    {
        if (caster == null || target == null) return 0;

        bool targetHasSlow = false;
        var sc = target.GetComponent<StatusController>();
        if (sc != null) targetHasSlow = sc.Has(StatusId.Slow);

        float mult = damageMultiplier * (targetHasSlow ? slowBonusMultiplier : 1f);
        float raw = caster.PhysicalDamage * mult;
        return Mathf.Max(0, Mathf.FloorToInt(raw));
    }

    public override IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
    {
        if (caster == null) yield break;

        // 생존 플레이어 수집
        var players = Object.FindObjectsOfType<BattleUnit>()
            .Where(u => u != null && u.team == Team.Player && !u.IsDead)
            .Where(u =>
            {
                var usc = u.GetComponent<UnitStateController>();
                return usc == null || !usc.Has(UnitStateId.Ambush); // 잠복이면 제외
            })
            .ToList();

        if (players.Count == 0) yield break;

        // 둔화 보유자 우선 선정
        var slowed = players.Where(u => {
            var sc = u.GetComponent<StatusController>();
            return sc != null && sc.Has(StatusId.Slow);
        }).ToList();


        // 둔화 보유자가 있다면 그중에서 '적대감 최상위'
        // 없다면 전체 플레이어 중 '적대감 최상위'
        BattleUnit actualTarget = null;
        if (slowed.Count > 0)
            actualTarget = SkillAsset.PickTargetByWeightedHostility(slowed);
        else
            actualTarget = SkillAsset.PickTargetByWeightedHostility(players);

        // 보정(혹시 null이면 랜덤 보정)
        if (actualTarget == null)
            actualTarget = players[Random.Range(0, players.Count)];

        if (actualTarget == null || actualTarget.IsDead) yield break;

        // 임팩트 타이밍에 대미지 적용 (둔화 대상이면 3배)
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
        yield return caster.AnimateAttack(actualTarget);

        // 안전장치: 애니 이벤트 누락 시 폴백
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
        yield break; // Unit형이므로 사용 안함
    }
}
