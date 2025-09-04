// SkillAsset.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public abstract class SkillAsset : ScriptableObject
{
    [Header("Meta")]
    public string displayName;
    public Sprite icon;

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
}
