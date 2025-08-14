using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;

public class BattleMapManager : MonoBehaviour, IBattleMapProvider
{
    [Header("전투 타일맵")]
    public Tilemap playerFloor;
    public Tilemap enemyFloor;

    [Header("오버레이(선택)")]
    public Tilemap playerOverlay;
    public Tilemap enemyOverlay;

    public Tilemap PlayerFloor => playerFloor;
    public Tilemap EnemyFloor => enemyFloor;
    public Tilemap AllyOverlay => playerOverlay;
    public Tilemap EnemyOverlay => enemyOverlay;

    public event System.Action OnMapsReady;

    void Awake()
    {
        Shared.battleMapManager = this;
        if (playerFloor == null) playerFloor = FindByName("Player_Tilemap");
        if (enemyFloor == null) enemyFloor = FindByName("Enemy_Tilemap");
        if (Shared.SceneTransitionManager == null)
            Debug.LogWarning("[BattleMapManager] SceneTransitionManager 없음(전투단독테스트라면 문제없음).");
    }
    void Start()
    {
        OnMapsReady?.Invoke(); // ← 여기서 호출
    }

    Tilemap FindByName(string contains)
    {
        foreach (var tm in FindObjectsOfType<Tilemap>())
            if (tm.name.ToLower().Contains(contains.ToLower())) return tm;
        return null;
    }
}
