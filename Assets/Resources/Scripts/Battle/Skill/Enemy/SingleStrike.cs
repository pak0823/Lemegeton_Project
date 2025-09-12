// Assets/Scripts/Skills/EA_SingleStrike.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Battle/SkillAsset/Enemy/SingleStrike")]
public class SingleStrike : SkillAsset
{
    [Header("Damage")]
    public float damageMultiplier = 1f; // 필요 시 가중치

#if UNITY_EDITOR
    void OnValidate() { targetMode = SkillTargetMode.Unit; }  // 에디터에서 항상 Unit로 고정
#endif

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddRow)
    {
        yield return originCell; // 단일 대상이므로 원점 셀만
    }

    public override IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
    {
        if (caster == null || target == null || target.IsDead) yield break;

        bool impactDone = false;
        System.Action impact = null;
        impact = () =>
        {
            caster.OnAttackImpact -= impact;
            impactDone = true;

            if (target != null && !target.IsDead)
            {
                target.PlayHit();
                int dmg = Mathf.Max(1, Mathf.RoundToInt(caster.PhysicalDamage * damageMultiplier));
                target.TakeDamage(dmg);
            }
        };

        caster.OnAttackImpact += impact;
        yield return caster.AnimateAttack(target);

        // 안전장치: 애니 이벤트 누락 시 폴백
        if (!impactDone && target != null && !target.IsDead)
        {
            target.PlayHit();
            int dmg = Mathf.Max(1, Mathf.RoundToInt(caster.PhysicalDamage * damageMultiplier));
            target.TakeDamage(dmg);
        }
    }
    public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        yield break; // Unit형이므로 사용 안함
    }
}
