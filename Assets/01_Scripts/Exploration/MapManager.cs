using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;

public class MapManager : MonoBehaviour
{
    public static MapManager Instance;

    public StageDatabase stageDB;
    public int currentStage = 1;
    public Transform gridParent;
    public GameObject playerPrefab;

    [Header("Sub-Systems")]
    public ExplorationMapLoader mapLoader;
    public ExplorationEntitySpawner entitySpawner;
    public ExplorationPersistenceManager persistenceManager;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Auto-add subsystems if missing
        if (!mapLoader) mapLoader = gameObject.AddComponent<ExplorationMapLoader>();
        if (!entitySpawner) entitySpawner = gameObject.AddComponent<ExplorationEntitySpawner>();
        if (!persistenceManager) persistenceManager = gameObject.AddComponent<ExplorationPersistenceManager>();

        mapLoader.Initialize(this);
        entitySpawner.Initialize(this);
        persistenceManager.Initialize(this);
    }

    void Start()
    {
        GenerateStageMap();
    }

    void GenerateStageMap()
    {
        GameObject prefabToUse = null;

        if (SceneTransitionManager.Instance != null &&
            SceneTransitionManager.Instance.explorationMapPrefabOverride != null)
        {
            prefabToUse = SceneTransitionManager.Instance.explorationMapPrefabOverride;
            Debug.Log("[MapManager] Override prefab 사용(재로딩 유지)");
        }
        else
        {
            prefabToUse = GetRandomNormalMapPrefab();
            if (SceneTransitionManager.Instance != null)
                SceneTransitionManager.Instance.explorationMapPrefabOverride = prefabToUse;
        }

        // Set the prefab in loader
        mapLoader.SetMapPrefab(prefabToUse);

        ResetExplorationMap();
    }

    GameObject GetRandomNormalMapPrefab()
    {
        if (stageDB == null) return null;
        var data = stageDB.normalStages.FirstOrDefault(x => x.stageNumber == currentStage);
        if (data == null || data.normalMapPrefabs == null || data.normalMapPrefabs.Length == 0)
            return null;
        return data.normalMapPrefabs[Random.Range(0, data.normalMapPrefabs.Length)];
    }

    public void ResetExplorationMap()
    {
        bool isReturning = SceneTransitionManager.Instance != null && 
                           SceneTransitionManager.Instance.HasExplorationSnapshot;

        // 1. Map Load/Find Phase
        if (!isReturning)
        {
            // Fresh start -> Destroy old, Create New
            persistenceManager.ClearActiveAddressables();
            mapLoader.LoadMap(gridParent, forceCreate: true);
        }
        else
        {
            // Returning -> Try Find
            mapLoader.LoadMap(gridParent, forceCreate: false);
        }

        GameObject currentMap = mapLoader.CurrentMap;
        if (currentMap == null) return;

        // 2. Tilemap Discovery Phase
        var (floors, obstacles, walls) = FindTilemapsMulti(currentMap);
        if (floors.Count == 0)
        {
            Debug.LogError("Floor 타일맵을 찾을 수 없습니다! (WalkableLayers 하위에 있는지 확인 필요)");
            return;
        }

        // 3. Entity Spawn Phase (Player)
        var playerMovement = entitySpawner.SpawnPlayer(playerPrefab, currentMap, floors, obstacles, walls);
        Transform playerTransform = (playerMovement != null) ? playerMovement.transform : null;
        
        HookCameraToPlayer(playerTransform, currentMap);

        // 4. Object Spawn Phase
        if (!isReturning)
        {
            entitySpawner.SpawnMapObjects(currentMap, floors, obstacles, walls);
        }

        // 5. Persistence Restore Phase
        if (isReturning)
        {
            RestoreSnapshotAsync(currentMap, floors[0], walls);
        }
        
        // 6. Fog Initialization
        // 맵의 전체 크기(Bounds)를 가져와서 Fog를 덮음
        Collider2D mapBounds = null;
        var t = currentMap.transform.Find("WorldBounds");
        if (t) t.TryGetComponent(out mapBounds);
        if (!mapBounds) mapBounds = currentMap.GetComponentInChildren<CompositeCollider2D>(true);
        if (!mapBounds) mapBounds = currentMap.GetComponentInChildren<BoxCollider2D>(true);
        
        if (mapBounds && ExplorationFogManager.Instance && playerTransform != null)
        {
            ExplorationFogManager.Instance.Initialize(playerTransform, mapBounds.bounds);
        }
    }

    async void RestoreSnapshotAsync(GameObject map, Tilemap floorMap, List<Tilemap> wallMap)
    {
        if (SceneTransitionManager.Instance == null || !SceneTransitionManager.Instance.HasExplorationSnapshot)
            return;
        
        var snap = SceneTransitionManager.Instance.explorationSnapshot;
        Transform container = (map != null) ? map.transform : gridParent;

        await persistenceManager.RestoreSnapshot(snap, map, container, floorMap, wallMap);
        
        SceneTransitionManager.Instance.ClearExplorationSnapshot();
        Debug.Log("[MapManager] Snapshot restored via PersistenceManager.");
    }

    (List<Tilemap> floors, List<Tilemap> obstacles, List<Tilemap> walls) FindTilemapsMulti(GameObject map)
    {
        List<Tilemap> floors = new List<Tilemap>();
        List<Tilemap> obstacles = new List<Tilemap>();
        List<Tilemap> walls = new List<Tilemap>();

        foreach (var tm in map.GetComponentsInChildren<Tilemap>())
        {
            if (tm.CompareTag("Wall")) walls.Add(tm);
            else if (tm.CompareTag("Obstacle")) obstacles.Add(tm);
            else if (tm.CompareTag("Ground")) floors.Add(tm);
        }
        return (floors, obstacles, walls);
    }

    void HookCameraToPlayer(Transform player, GameObject map)
    {
        if (map == null) return;
        
        var cam = Camera.main ? Camera.main.GetComponent<CameraFollow2D>()
                              : FindObjectOfType<CameraFollow2D>(true);
        if (!cam) return;

        Collider2D bounds = null;
        var t = map.transform.Find("WorldBounds");
        if (t) t.TryGetComponent(out bounds);
        if (!bounds) bounds = map.GetComponentInChildren<CompositeCollider2D>(true);
        if (!bounds) bounds = map.GetComponentInChildren<BoxCollider2D>(true);

        if (bounds) cam.worldBounds = bounds;
    }
}