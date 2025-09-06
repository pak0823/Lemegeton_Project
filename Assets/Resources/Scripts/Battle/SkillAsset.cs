// SkillAsset.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum DamageSchool { Physical, Magical }  //근력, 총명
public enum AttackAttr { None, Pierce, Strike, Slash /* etc */ }

public struct SkillRuntime
{
    public Tilemap map;
    public Vector3Int originCell;   // 시전자 조준/타겟 기준 셀
    public Vector3Int casterCell;   // 시전자 현재 셀
    public Vector3Int targetCell;   // 피격자 셀
}

public abstract class SkillAsset : ScriptableObject
{
    [Header("Meta")]
    public string displayName;
    public Sprite icon;

    [Header("Damage (default)")]
    public DamageSchool school = DamageSchool.Physical;
    public AttackAttr attribute = AttackAttr.None;
    public float power = 1f; // 기본 배수(예: 1.0 = 원 공격력)

    [Header("Cost")]
    [Min(0)] public int mpCost = 0;

    [Header("Targeting")]
    public SkillTargetMode targetMode; // 기존 enum 재사용 (Unit/Tile) 

    [Header("Compat (임시)")]
    public SkillId legacyId = SkillId.Skill1; // 기존 분기 로직 호환용

    /// <summary>미리보기/피격판정을 위한 범위 셀 반환. origin = 대상 유닛 셀(유닛형) 또는 조준 셀(타일형)</summary>
    public abstract IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddRow);

    /// <summary>유닛 지목형 해결</summary>
    public abstract IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target);

    /// <summary>타일 지목형 해결</summary>
    public abstract IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster);

    //스킬별 대미지 계산. 필요시 하위 클래스에서 override
    public virtual int ComputeDamage(BattleUnit caster, BattleUnit target, in SkillRuntime ctx)
    {
        // 물리=PhysicalDamage, 마법=MagicDamage
        int baseStat = (school == DamageSchool.Physical)
            ? Mathf.Max(1, caster.PhysicalDamage)
            : Mathf.Max(1, caster.MagicDamage);

        float mult = Mathf.Max(0f, power);

        if (attribute != AttackAttr.None && target != null && target.resistTable != null)
        {
            foreach (var mod in target.resistTable)
                if (mod.attr == attribute) { mult *= Mathf.Max(0f, mod.mult); break; }
        }

        return Mathf.Max(1, Mathf.RoundToInt(baseStat * mult));
    }
}