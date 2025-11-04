using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public interface IBattleMapProvider
{
    UnityEngine.Tilemaps.Tilemap PlayerFloor { get; }
    UnityEngine.Tilemaps.Tilemap EnemyFloor { get; }
    UnityEngine.Tilemaps.Tilemap AllyOverlay { get; }  // 선택
    UnityEngine.Tilemaps.Tilemap EnemyOverlay { get; }  // 선택
    event System.Action OnMapsReady;
}

// 자기 자신에게 즉시 시전되는 스킬 식별용
public interface ISelfCastSkill
{
    // true면 타겟팅 UI 없이 선택 즉시 ResolveOnUnit(caster, caster)
    bool SelfCastOnSelect { get; }
}

public interface ISkillForStateResolver
{
    // 캐스터 상태를 보고 실제 사용할 SO를 돌려준다(치환).
    SkillAsset ResolveForCaster(BattleUnit caster);
}

public interface ITargetMapProvider
{
    // 타일 지목형 미리보기/시전을 위한 기본 타일맵을 지정한다.
    Tilemap GetTargetMap(BattleManager bm, BattleUnit caster);
}
public interface ISkillCustomPreview
{
    /// <summary>스킬 타겟팅 진입 시, 표시할 프리뷰 셀 집합을 돌려준다.</summary>
    IEnumerable<Vector3Int> GetPreviewCells(BattleManager bm, BattleUnit caster);

    /// <summary>프리뷰/클릭을 받을 타일맵(없으면 null)</summary>
    Tilemap GetTargetMap(BattleManager bm, BattleUnit caster);
}
public interface IInstantTileSkill { } // 타일 클릭 시 공격/투사체 연출 없이 즉시 Resolve