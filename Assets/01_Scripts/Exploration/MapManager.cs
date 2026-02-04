using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.AddressableAssets; // [필수] 어드레서블
using UnityEngine.ResourceManagement.AsyncOperations; // [필수] 핸들
using System.Threading.Tasks; // [필수] Task

public class MapManager : MonoBehaviour
{
    public static MapManager Instance;
    public StageDatabase stageDB;
    public int currentStage = 1;
    public Transform gridParent;
    public GameObject playerPrefab;

    private GameObject currentMap; // 최초 선택된 맵 프리팹
    private GameObject backUpMapPrefab;  // 최초 선택된 맵 저장 프리팹
    private MapToggleManager mapToggle;

    // [개선 1] 생성된 어드레서블 오브젝트를 추적하기 위한 리스트
    private List<GameObject> activeSnapshotObjects = new List<GameObject>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        mapToggle = GetComponent<MapToggleManager>();
    }

    void Start()
    {
        GenerateStageMap();
    }

    void GenerateStageMap()
    {
        if (SceneTransitionManager.Instance != null &&
            SceneTransitionManager.Instance.explorationMapPrefabOverride != null)
        {
            backUpMapPrefab = SceneTransitionManager.Instance.explorationMapPrefabOverride;
            Debug.Log("[MapManager] Override prefab 사용(재로딩 유지)");
        }
        else
        {
            backUpMapPrefab = GetRandomNormalMapPrefab();

            if (SceneTransitionManager.Instance != null)
                SceneTransitionManager.Instance.explorationMapPrefabOverride = backUpMapPrefab;
        }

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

    void InstantiatePlayer(List<Tilemap> _floors, List<Tilemap> _obstacles, List<Tilemap> _walls)
    {
        if (playerPrefab == null) return;

        if (PlayerMovement.Instance != null)
            Destroy(PlayerMovement.Instance.gameObject);

        GameObject player = Instantiate(playerPrefab);
        var playermovement = player.GetComponent<PlayerMovement>();

        if (playermovement != null)
        {
            playermovement.SetTilemaps(_floors, _obstacles, _walls);
        }

        if (currentMap != null)
        {
            var spawn = currentMap.transform.Find("PlayerStart");
            if (spawn != null)
            {
                Vector3 spawnPos = spawn.position;
                spawnPos.z = 0f;
                player.transform.position = spawnPos;
            }
            else
                Debug.LogWarning("[MapManager] 맵에 'PlayerStart' 오브젝트가 없습니다.");
        }

        var camScript = FindAnyObjectByType<CameraFollow2D>();
        if (camScript != null)
        {
            camScript.target = player.transform;
            camScript.SnapToTarget();
        }
    }

    void SetupMapToggle(Tilemap _floors, Tilemap _wall)
    {
        mapToggle.mainMap = currentMap;
        mapToggle.gridParent = gridParent;
    }

    void TrySpawnObjects(List<Tilemap> _floors, List<Tilemap> _obstacles, List<Tilemap> _walls)
    {
        var spawner = currentMap.GetComponentInChildren<MapObjectSpawner>();

        if (spawner == null) return;

        List<Collider2D> excludeList = new List<Collider2D>();

        var tagged = currentMap.GetComponentsInChildren<Collider2D>()
            .Where(c => c.CompareTag("ExcludeSpawn"));
        excludeList.AddRange(tagged);

        spawner.Spawn(_floors, _obstacles, _walls, excludeList.ToArray());
    }

    void HookCameraToPlayer(Transform player)
    {
        var cam = Camera.main ? Camera.main.GetComponent<CameraFollow2D>()
                              : FindObjectOfType<CameraFollow2D>(true);
        if (!cam) return;

        cam.SetTarget(player, snap: true);

        Collider2D bounds = null;
        var t = currentMap.transform.Find("WorldBounds");
        if (t) t.TryGetComponent(out bounds);
        if (!bounds) bounds = currentMap.GetComponentInChildren<CompositeCollider2D>(true);
        if (!bounds) bounds = currentMap.GetComponentInChildren<BoxCollider2D>(true);

        if (bounds) cam.worldBounds = bounds;
    }

    // [개선 2] 병렬 로딩 적용 (속도 최적화)
    async void ApplyExplorationSnapshot(ExplorationSnapshot snap, Tilemap floorMap, List<Tilemap> wallMap)
    {
        var existing = new Dictionary<string, IExplorationPersistable>();
        foreach (var mb in currentMap.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb is IExplorationPersistable ip && !existing.ContainsKey(ip.PersistID))
                existing.Add(ip.PersistID, ip);

        Transform container = floorMap.transform.parent.Find("Object");
        if (container == null) container = (currentMap != null) ? currentMap.transform : gridParent;

        // 병렬 처리를 위한 Task 리스트 생성
        List<Task> loadingTasks = new List<Task>();

        foreach (var s in snap.objects)
        {
            // 1. 이미 존재하는 오브젝트 복구 (동기 처리)
            if (existing.TryGetValue(s.id, out var existIp))
            {
                if (existIp is PushObject existPush)
                    existPush.SetTilemaps(new List<Tilemap> { floorMap }, wallMap);
                existIp.LoadState(s);
                continue;
            }

            // 2. 새로 생성해야 하는 오브젝트 (비동기 처리 대상)
            if (s.kind == "Chest" || s.kind == "Trap" || s.kind == "Encounter")
            {
                if (s.kind == "Trap" && (s.b1 || !s.b2)) continue;
                if (s.kind == "Encounter" && s.b1) continue;

                var refObj = FindPrefabByName(s.prefabName);
                if (refObj == null)
                {
                    Debug.LogWarning($"[Snapshot] prefab '{s.prefabName}' not found for {s.kind}/{s.id}");
                    continue;
                }

                // 로딩 태스크를 리스트에 추가 (즉시 await 하지 않음 -> 병렬 실행)
                //loadingTasks.Add(RestoreSingleObjectAsync(refObj, s, container, floorMap, wallMap));
            }
            else
            {
                Debug.LogWarning($"[Snapshot] No existing object for ID={s.id} kind={s.kind}. Skipped instantiate.");
            }
        }

        // 모든 로딩이 끝날 때까지 대기
        await Task.WhenAll(loadingTasks);

        Debug.Log($"[Snapshot] applied objects = {snap.objects.Count} (Active Addressables: {activeSnapshotObjects.Count})");
    }

    // [헬퍼 함수] 단일 오브젝트 비동기 복구 로직 (ApplyExplorationSnapshot에서 호출)
    //async Task RestoreSingleObjectAsync(AssetReferenceGameObject refObj, ExplorationSnapshot.ObjectData s, Transform container, Tilemap floorMap, List<Tilemap> wallMap)
    //{
    //    var handle = refObj.InstantiateAsync(s.position, Quaternion.identity, container);
    //    await handle.Task;

    //    if (handle.Status == AsyncOperationStatus.Succeeded)
    //    {
    //        GameObject obj = handle.Result;

    //        // [개선 1] 메모리 관리를 위해 리스트에 등록
    //        activeSnapshotObjects.Add(obj);

    //        var pid = obj.GetComponent<ExplorationPersistId>();
    //        if (!pid) pid = obj.AddComponent<ExplorationPersistId>();
    //        pid.OverrideIdForRestore(s.id);

    //        // 이름 설정
    //        obj.name = refObj.editorAsset != null ? refObj.editorAsset.name : s.prefabName;

    //        if (obj.TryGetComponent<PushObject>(out var push))
    //            push.SetTilemaps(new List<Tilemap> { floorMap }, wallMap);

    //        if (obj.TryGetComponent<MonoBehaviour>(out var mb) && mb is IExplorationPersistable ip2)
    //            ip2.LoadState(s);
    //    }
    //    else
    //    {
    //        Debug.LogError($"[Snapshot] Failed to instantiate {s.prefabName}");
    //    }
    //}

    AssetReferenceGameObject FindPrefabByName(string prefabName)
    {
        var spawner = currentMap != null ? currentMap.GetComponentInChildren<MapObjectSpawner>(true) : null;

        if (spawner != null)
        {
            if (spawner.trapRefs != null)
            {
                foreach (var p in spawner.trapRefs)
                    if (CheckNameMatch(p, prefabName)) return p;
            }
            if (spawner.chestRefs != null)
            {
                foreach (var p in spawner.chestRefs)
                    if (CheckNameMatch(p, prefabName)) return p;
            }
            if (spawner.patternRefs != null)
            {
                foreach (var p in spawner.patternRefs)
                    if (CheckNameMatch(p, prefabName)) return p;
            }
        }
        return null;
    }

    bool CheckNameMatch(AssetReferenceGameObject refObj, string targetName)
    {
        if (refObj == null) return false;

#if UNITY_EDITOR
        if (refObj.editorAsset != null && refObj.editorAsset.name == targetName) return true;
#endif
        string key = refObj.RuntimeKey.ToString();
        return key.Contains(targetName);
    }

    public void ResetExplorationMap()
    {
        // [개선 1] 어드레서블로 생성된 오브젝트들 정식 반납 (메모리 해제)
        // 리스트를 역순으로 돌거나, foreach로 돌며 해제합니다.
        foreach (var obj in activeSnapshotObjects)
        {
            if (obj != null)
            {
                Addressables.ReleaseInstance(obj);
            }
        }
        activeSnapshotObjects.Clear(); // 리스트 비우기

        // 기존 맵 파괴
        if (currentMap != null) Destroy(currentMap);
        if (PlayerMovement.Instance != null) Destroy(PlayerMovement.Instance.gameObject);

        if (backUpMapPrefab == null)
        {
            Debug.LogError("[MapManager] 생성할 맵 프리팹이 없습니다!");
            return;
        }

        currentMap = Instantiate(backUpMapPrefab, Vector3.zero, Quaternion.identity, gridParent);

        var (floorMaps, obstacleMaps, wallMaps) = FindTilemapsMulti(currentMap);

        if (floorMaps.Count == 0)
        {
            Debug.LogError("Floor 타일맵을 찾을 수 없습니다! (WalkableLayers 하위에 있는지 확인 필요)");
            return;
        }

        InstantiatePlayer(floorMaps, obstacleMaps, wallMaps);
        TrySpawnObjects(floorMaps, obstacleMaps, wallMaps);
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
            else Debug.LogWarning($"[MapManager] Tag가 설정되지 않은 타일맵 발견: {tm.name}");
        }
        return (floors, obstacles, walls);
    }
}