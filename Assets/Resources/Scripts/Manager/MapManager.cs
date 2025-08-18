using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MapManager : MonoBehaviour
{
    public StageDatabase stageDB;
    public int currentStage = 1;
    public Transform gridParent;
    public GameObject playerPrefab;

    private GameObject currentMap;// 최초 선택된 맵 프리팹
    private GameObject backUpMapPrefab;  // 최초 선택된 맵 저장 프리팹
    private MapToggleManager mapToggle;

    void Awake()
    {
        mapToggle = GetComponent<MapToggleManager>();
        Shared.MapManager = this;
    }

    void Start()
    {
        GenerateStageMap();
    }

    void GenerateStageMap()
    {
        backUpMapPrefab = GetRandomNormalMapPrefab();
        if (backUpMapPrefab == null)
        {
            Debug.LogError($"Stage {currentStage}에 해당하는 일반 맵 프리팹을 찾을 수 없습니다.");
            return;
        }

        currentMap = Instantiate(backUpMapPrefab, Vector3.zero, Quaternion.identity, gridParent);
        var (floorMap, wallMap) = FindTilemaps(currentMap);

        if (floorMap == null)
        {
            Debug.LogError("Floor 타일맵을 찾을 수 없습니다!");
            return;
        }

        SetupMapToggle(floorMap, wallMap);
        TrySpawnObjects(floorMap);
        InstantiatePlayer(floorMap, wallMap);
    }

    GameObject GetRandomNormalMapPrefab()
    {
        var stageData = stageDB.normalStages.FirstOrDefault(s => s.stageNumber == currentStage);
        if (stageData == null || stageData.normalMapPrefabs.Length == 0)
            return null;

        int index = Random.Range(0, stageData.normalMapPrefabs.Length);
        return stageData.normalMapPrefabs[index];
    }

    (Tilemap floor, Tilemap wall) FindTilemaps(GameObject map)
    {
        Tilemap floor = null, wall = null;
        foreach (var tm in map.GetComponentsInChildren<Tilemap>())
        {
            var name = tm.gameObject.name.ToLower();
            if (name.Contains("floor")) floor = tm;
            if (name.Contains("wall")) wall = tm;
            if (floor != null && wall != null) break;
        }
        return (floor, wall);
    }

    void SetupMapToggle(Tilemap floor, Tilemap wall)
    {
        mapToggle.mainMap = currentMap;
        mapToggle.gridParent = gridParent;
    }

    void TrySpawnObjects(Tilemap floor)
    {
        var spawner = currentMap.GetComponentInChildren<MapObjectSpawner>();
        var spawnPoint = currentMap.transform.Find("PlayerStart");

        if (spawner == null || spawnPoint == null)
        {
            Debug.LogError("MapObjectSpawner 또는 PlayerStart 없음");
            return;
        }

        Vector3Int spawnCell = floor.WorldToCell(spawnPoint.position);
        var excludeColliders = currentMap.GetComponentsInChildren<Collider2D>()
            .Where(c => c.CompareTag("ExcludeSpawn")).ToArray();

        spawner.Spawn(floor, excludeColliders);
    }

    void InstantiatePlayer(Tilemap floor, Tilemap wall)
    {
        var spawnPoint = currentMap.transform.Find("PlayerStart");
        var position = spawnPoint != null ? spawnPoint.position : Vector3.zero;

        var player = Instantiate(playerPrefab, position, Quaternion.identity);
        var pm = player.GetComponent<PlayerMovement>();

        if (pm != null)
        {
            pm.SetTilemap(floor, wall);
            Debug.Log("[MapManager] SetTilemap 호출 완료");
        }
        else
        {
            Debug.LogError("[MapManager] PlayerMovement 컴포넌트를 찾을 수 없습니다.");
        }

        mapToggle.SetPlayerStartPosition(player.transform);
    }
    public void ResetExplorationMap()
    {
        if (currentMap != null)
            Destroy(currentMap);

        if (Shared.PlayerMovement != null)
        {
            Destroy(Shared.PlayerMovement.gameObject);
            Shared.PlayerMovement = null;
        }

        if (Shared.ObjectGaugeManager != null)
        {
            Shared.ObjectGaugeManager.ResetState();
        }

        currentMap = Instantiate(backUpMapPrefab, Vector3.zero, Quaternion.identity, gridParent);
        var (floorMap, wallMap) = FindTilemaps(currentMap);

        if (floorMap == null)
        {
            Debug.LogError("Floor 타일맵을 찾을 수 없습니다!");
            return;
        }

        SetupMapToggle(floorMap, wallMap);
        TrySpawnObjects(floorMap);
        InstantiatePlayer(floorMap, wallMap);

        Debug.Log("[MapManager] 탐험맵 재생성 완료");
    }
}
