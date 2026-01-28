using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;

public class BattleMapManager : MonoBehaviour, IBattleMapProvider
{
    public static BattleMapManager Instance {  get; private set; }

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
        if(Instance == null) Instance = this;
        else Destroy(gameObject);

        if (playerFloor == null) playerFloor = FindByName("Player_Tilemap");
        if (enemyFloor == null) enemyFloor = FindByName("Enemy_Tilemap");
        if (SceneTransitionManager.Instance == null)
            Debug.LogWarning("[BattleMapManager] SceneTransitionManager 없음(전투단독테스트라면 문제없음).");
    }
    void Start()
    {
        if (playerFloor != null && enemyFloor != null)
            OnMapsReady?.Invoke(); // 여기서 호출
    }

    public void UseEnemyFloor(Tilemap newEnemyFloor, Tilemap newEnemyOverlay = null)
    {
        enemyFloor = newEnemyFloor;
        if (newEnemyOverlay != null) enemyOverlay = newEnemyOverlay;
    }

    Tilemap FindByName(string contains)
    {
        foreach (var tm in FindObjectsOfType<Tilemap>())
            if (tm.name.ToLower().Contains(contains.ToLower())) return tm;
        return null;
    }

    // 진형 인덱스를 실제 헥사 타일 좌표로 변환
    // 기준점(center)을 (0,0,0)으로 가정하고 상대 좌표를 반환
    public Vector3Int GetFormationSpawnPoint(int index)
    {
        // 3-4-5-4-3 구조 (UI와 동일한 배치)
        // 짝수/홀수 행(y)에 따라 x 좌표 보정이 필요할 수 있습니다. (Flat-top vs Pointy-top)
        // 아래는 일반적인 Pointy-top 헥사 기준 예시 좌표입니다.
        // 실제 게임 맵의 그리드 설정에 따라 x, y 값을 조금씩 수정해야 할 수 있습니다.

        switch (index)
        {
            // Row 1 (Top, 3개)
            case 0: return new Vector3Int(-3, 2, 0);
            case 1: return new Vector3Int(-2, 2, 0);
            case 2: return new Vector3Int(-1, 2, 0);

            // Row 2 (4개)
            case 3: return new Vector3Int(-4, 1, 0); // 혹은 -2, 1
            case 4: return new Vector3Int(-3, 1, 0);  // 혹은 -1, 1
            case 5: return new Vector3Int(-2, 1, 0);
            case 6: return new Vector3Int(-1, 1, 0);

            // Row 3 (Center, 5개) -> y=0
            case 7: return new Vector3Int(-4, 0, 0);
            case 8: return new Vector3Int(-3, 0, 0);
            case 9: return new Vector3Int(-2, 0, 0); // 중앙
            case 10: return new Vector3Int(-1, 0, 0);
            case 11: return new Vector3Int(0, 0, 0);

            // Row 4 (4개)
            case 12: return new Vector3Int(-4, -1, 0);
            case 13: return new Vector3Int(-3, -1, 0);
            case 14: return new Vector3Int(-2, -1, 0);
            case 15: return new Vector3Int(-1, -1, 0);

            // Row 5 (Bottom, 3개)
            case 16: return new Vector3Int(-3, -2, 0);
            case 17: return new Vector3Int(-2, -2, 0);
            case 18: return new Vector3Int(-1, -2, 0);

            default: return Vector3Int.zero;
        }
    }
}
