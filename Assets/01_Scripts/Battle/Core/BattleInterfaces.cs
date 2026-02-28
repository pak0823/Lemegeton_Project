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
public interface IProjectileTileSkill
{
    //투사체 프리팹. 스킬이 직접 소유(권장)
    ProjectileController GetProjectilePrefab(BattleUnit caster);

    //투사체 속도(유닛/초). 필요없으면 0 또는 음수 반환 → 기본값 사용
    float GetProjectileSpeed(BattleUnit caster);
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

public interface ITrainingRouteInfoProvider //인덱스에 해당하는 Title/설명 가져오기
{
    string GetTrainingRouteTitle(int routeIndex);
    string GetTrainingRouteDescription(int routeIndex);
}
public interface IGridProvider
{
    Tilemap GetMap(Team team);
    void SetOccupied(Team team, Vector3Int cell, bool occupied);
    bool IsOccupied(Team team, Vector3Int cell);
    bool IsWalkable(Tilemap map, Vector3Int cell);
    BattleUnit GetUnitAt(Vector3Int cell);
    int CrossMapDistance(Tilemap reference, Tilemap fromMap, Vector3Int fromCell, Tilemap toMap, Vector3Int toCell);
    IEnumerable<BattleUnit> GetUnitsInArea(Tilemap map, IEnumerable<Vector3Int> cells);
}
public interface IFieldController
{
    // 야수의 영역 생성 (SelfBeastDomainSkill 등에서 사용)
    void SpawnBeastDomainZone(Tilemap map, BattleUnit owner, Vector3Int center, int radius, int duration);

    // 상태 이상 타일 생성 (LastVorgToxicPassive, ParametricDamageSkill 등에서 사용)
    void CreateStatusTileZone(BattleUnit owner, Tilemap map, Vector3Int cell, int zoneDuration, TileBase newTileBase, StatusId statusId, int stack = 1, int statusDuration = 3);

    // 턴 시작/종료 시 호출되는 로직 (필요시 추가)
    void OnTurnStart(BattleUnit unit);
    void OnTurnEnd(BattleUnit unit);
}