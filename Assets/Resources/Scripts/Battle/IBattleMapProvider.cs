public interface IBattleMapProvider
{
    UnityEngine.Tilemaps.Tilemap PlayerFloor { get; }
    UnityEngine.Tilemaps.Tilemap EnemyFloor { get; }
    UnityEngine.Tilemaps.Tilemap AllyOverlay { get; }  // 선택
    UnityEngine.Tilemaps.Tilemap EnemyOverlay { get; }  // 선택
    event System.Action OnMapsReady;
}