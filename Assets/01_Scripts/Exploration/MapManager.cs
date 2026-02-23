using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

public class MapManager : MonoBehaviour
{
    public static MapManager Instance;


    // 현재 로드된 맵의 연결 데이터 (MapTransitionManager가 참조)
    public MapConnectionData CurrentConnectionData { get; private set; }
    // 현재 로드된 맵의 ExplorationMapData (MapTransitionManager가 참조)
    public ExplorationMapData CurrentMapData { get; private set; }


public StageDatabase stageDB;
    public int currentStage = 1;
    public Transform gridParent;
    public GameObject playerPrefab;
    [SerializeField] public LayerMask impassableLayerMask; // Pathfinding용

    [Header("Sub-Systems")]
    public ExplorationMapLoader mapLoader;
    public ExplorationEntitySpawner entitySpawner;
    public ExplorationPersistenceManager persistenceManager;
    public PathfindingSystem pathfindingSystem;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Auto-add subsystems if missing
        if (!mapLoader) mapLoader = gameObject.AddComponent<ExplorationMapLoader>();
        if (!entitySpawner) entitySpawner = gameObject.AddComponent<ExplorationEntitySpawner>();
        if (!persistenceManager) persistenceManager = gameObject.AddComponent<ExplorationPersistenceManager>();
        if (!pathfindingSystem) pathfindingSystem = gameObject.AddComponent<PathfindingSystem>();

        mapLoader.Initialize(this);
        entitySpawner.Initialize(this);
        persistenceManager.Initialize(this);
        // PathfindingSystem initialized in ResetExplorationMap with tilemaps
    }

    void Start()
    {
        GenerateStageMap();
    }


    /// <summary>
    /// 지정된 맵 ID의 프리팹을 로드합니다. MapTransitionManager에서 호출합니다.
    /// </summary>
    /// <summary>
    /// 숨겨진 포탈에서 직접 프리팩을 지정하여 맵을 로드합니다.
    /// MapConnectionData 등록없이 수동으로 프리팩을 지정하는 숨겨진 맵에 사용합니다.
    /// </summary>
    public void LoadHiddenMap(string mapId, GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError($"[MapManager] LoadHiddenMap 실패: prefab이 null입니다. mapId={mapId}");
            return;
        }

        UnloadCurrentMap();

        mapLoader.SetMapPrefab(prefab);
        mapLoader.LoadMap(gridParent, forceCreate: true);

        GameObject currentMap = mapLoader.CurrentMap;
        if (currentMap == null) return;

        CurrentMapData = currentMap.GetComponent<ExplorationMapData>();

        SetupMapInternal(currentMap);

        Debug.Log($"[MapManager] LoadHiddenMap 완료: {mapId}");
    }


public void LoadSpecificMap(string mapId)
    {
        if (CurrentConnectionData == null)
        {
            Debug.LogError("[MapManager] LoadSpecificMap 실패: CurrentConnectionData가 없습니다.");
            return;
        }

        var prefab = CurrentConnectionData.GetPrefab(mapId);
        if (prefab == null)
        {
            Debug.LogError($"[MapManager] '{mapId}' 에 해당하는 프리팹을 찾을 수 없습니다.");
            return;
        }

        // 기존 맵 제거
        UnloadCurrentMap();

        // 새 맵 로드
        mapLoader.SetMapPrefab(prefab);
        mapLoader.LoadMap(gridParent, forceCreate: true);

        GameObject currentMap = mapLoader.CurrentMap;
        if (currentMap == null) return;

        // ExplorationMapData 갱신
        CurrentMapData = currentMap.GetComponent<ExplorationMapData>();

        SetupMapInternal(currentMap);

        Debug.Log($"[MapManager] LoadSpecificMap 완료: {mapId}");
    }

    /// <summary>
    /// 현재 로드된 맵을 제거합니다.
    /// </summary>
    public void UnloadCurrentMap()
    {
        persistenceManager.ClearActiveAddressables();
        var oldMap = mapLoader.CurrentMap;
        if (oldMap != null) Destroy(oldMap);
    }

    /// <summary>
    /// 맵 공통 초기화 (타일맵, 패스파인딩, 카메라, 포그)
    /// </summary>
    private void SetupMapInternal(GameObject currentMap)
    {
        var mapData = currentMap.GetComponent<ExplorationMapData>();
        if (mapData == null)
        {
            Debug.LogError($"[MapManager] '{currentMap.name}'에 ExplorationMapData가 없습니다.");
            return;
        }

        CurrentMapData = mapData;

        var floors = mapData.floorMaps;
        var walls = mapData.wallMaps;
        var obstacles = mapData.obstacleMaps;

        if (pathfindingSystem)
            pathfindingSystem.Initialize(floors, obstacles, walls, impassableLayerMask);

        HookCameraToPlayer(PlayerMovement.Instance?.transform, currentMap);

        Collider2D mapBounds = null;
        var t = currentMap.transform.Find("WorldBounds");
        if (t) t.TryGetComponent(out mapBounds);
        if (!mapBounds) mapBounds = currentMap.GetComponentInChildren<CompositeCollider2D>(true);
        if (!mapBounds) mapBounds = currentMap.GetComponentInChildren<BoxCollider2D>(true);

        if (mapBounds && ExplorationFogManager.Instance && PlayerMovement.Instance != null)
            ExplorationFogManager.Instance.Initialize(PlayerMovement.Instance.transform, mapBounds.bounds);

        // MapTransitionManager에 현재 맵 ID 알림
        if (MapTransitionManager.Instance != null && mapData != null)
            MapTransitionManager.Instance.SetCurrentMapId(mapData.mapId);
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

        // 스테이지가 로드될 때 맵 연결 데이터도 함께 초기화
        CurrentConnectionData = data.mapConnectionData;
        if (CurrentConnectionData == null)
            Debug.LogWarning($"[MapManager] Stage {currentStage}의 MapConnectionData가 설정되지 않았습니다.");

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
        var mapData = currentMap.GetComponent<ExplorationMapData>();
        if (mapData == null)
        {
            Debug.LogError($"[MapManager] '{currentMap.name}'에 ExplorationMapData 컴포넌트가 없습니다.");
            return;
        }

        CurrentMapData = mapData;

        var floors = mapData.floorMaps;
        var walls = mapData.wallMaps;
        var obstacles = mapData.obstacleMaps;

        if (floors == null || floors.Count == 0)
        {
            Debug.LogError("Floor 타일맵 리스트가 비어있습니다! (ExplorationMapData 확인 필요)");
            return;
        }

        // Initialize PathfindingSystem
        if (pathfindingSystem)
        {
            pathfindingSystem.Initialize(floors, obstacles, walls, impassableLayerMask);
        }

        // 3. Entity Spawn Phase (Player): 랜덤 이동 가능 타일에 플레이어 배치
        Vector3Int playerUsedCell;
        var playerMovement = entitySpawner.SpawnPlayer(playerPrefab, currentMap, floors, obstacles, walls, out playerUsedCell);
        Transform playerTransform = (playerMovement != null) ? playerMovement.transform : null;

        HookCameraToPlayer(playerTransform, currentMap);

        // 4. Object Spawn Phase: 플레이어 타일 제외 후 pattern → object → trap 순으로 배치
        if (!isReturning)
        {
            entitySpawner.SpawnMapObjects(currentMap, floors, obstacles, walls, playerUsedCell);
        }

        // 5. Persistence Restore Phase
        if (isReturning)
        {
            RestoreSnapshotAsync(currentMap, floors[0], walls).Forget();
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

        // MapTransitionManager에 현재 맵 ID 알림
        if (MapTransitionManager.Instance != null && mapData != null)
            MapTransitionManager.Instance.SetCurrentMapId(mapData.mapId);
    }

    async UniTaskVoid RestoreSnapshotAsync(GameObject map, Tilemap floorMap, List<Tilemap> wallMap)
    {
        try
        {
            if (SceneTransitionManager.Instance == null || !SceneTransitionManager.Instance.HasExplorationSnapshot)
                return;

            var snap = SceneTransitionManager.Instance.explorationSnapshot;
            Transform container = (map != null) ? map.transform : gridParent;

            await persistenceManager.RestoreSnapshot(snap, map, container, floorMap, wallMap);

            SceneTransitionManager.Instance.ClearExplorationSnapshot();
            Debug.Log("[MapManager] Snapshot restored via PersistenceManager.");
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
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
