using System.Collections.Generic;
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
        // SceneTransitionManager가 오버라이드 프리팹을 들고 있으면 그것을 사용
        if (Shared.SceneTransitionManager != null &&
        Shared.SceneTransitionManager.explorationMapPrefabOverride != null)
        {
            backUpMapPrefab = Shared.SceneTransitionManager.explorationMapPrefabOverride;
            Debug.Log("[MapManager] Override prefab 사용(재로딩 유지)");
        }
        else
        {
            backUpMapPrefab = GetRandomNormalMapPrefab();

            // 최초 생성 시 오버라이드 프리팹으로 등록(이후 재로딩 시 동일 프리팹 사용)
            if (Shared.SceneTransitionManager != null)
                Shared.SceneTransitionManager.explorationMapPrefabOverride = backUpMapPrefab;
        }


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

        var snap = (Shared.SceneTransitionManager != null) ? Shared.SceneTransitionManager.explorationSnapshot : null;
        if (snap != null && snap.objects != null && snap.objects.Count > 0)
        {
            // 스냅샷대로 재생성 + 상태 복원
            ApplyExplorationSnapshot(snap, floorMap, wallMap);
            // 다음 맵에선 다시 랜덤 스폰
            Shared.SceneTransitionManager.explorationSnapshot = null;
        }
        else
        {
            // 스냅샷이 없거나(objects==0) → 정상 랜덤 스폰
            TrySpawnObjects(floorMap);
        }

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

    void ApplyExplorationSnapshot(ExplorationSnapshot snap, Tilemap floorMap, Tilemap wallMap)
    {
        // 현재 맵에 이미 존재하는 Persistable들을 ID 맵으로 준비 (PushObject 등)
        var existing = new Dictionary<string, IExplorationPersistable>();
        foreach (var mb in currentMap.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb is IExplorationPersistable ip && !existing.ContainsKey(ip.PersistID))
                existing.Add(ip.PersistID, ip);


        // 스포너와 동일 컨테이너 사용(정렬/토글 일관성)
        Transform container = floorMap.transform.parent.Find("Object");
        if (container == null) container = (currentMap != null) ? currentMap.transform : gridParent;

        // 스냅샷대로 재생성
        foreach (var s in snap.objects)
        {
            // 기존 오브젝트(ID 매칭) 있으면 그걸 복원
            if (existing.TryGetValue(s.id, out var existIp))
            {
                // PushObject면 타일맵 주입 후 위치 복원
                if (existIp is PushObject existPush)
                    existPush.SetTilemaps(floorMap, wallMap);
                existIp.LoadState(s);
                continue;
            }

            // 없으면(랜덤 스폰되던 Chest/Trap)만 프리팹으로 재생성
            if (s.kind == "Chest" || s.kind == "Trap")
            {
                if (s.kind == "Trap" && (s.b1 || !s.b2))
                    continue;
                var prefab = FindPrefabByName(s.prefabName);
                if (prefab == null)
                {
                    Debug.LogWarning($"[Snapshot] prefab '{s.prefabName}' not found for {s.kind}/{s.id}");
                    continue;
                }
                var obj = Instantiate(prefab, s.position, Quaternion.identity, container);
                var pid = obj.GetComponent<ExplorationPersistId>();
                if (!pid) pid = obj.AddComponent<ExplorationPersistId>();
                pid.OverrideIdForRestore(s.id);
                obj.name = prefab.name;
                if (obj.TryGetComponent<PushObject>(out var push))
                    push.SetTilemaps(floorMap, wallMap);
                if (obj.TryGetComponent<MonoBehaviour>(out var mb) && mb is IExplorationPersistable ip2)
                    ip2.LoadState(s);
            }
            else
            {
                // Push 등인데 기존 오브젝트가 없다면 설계상 프리플레이스여야 하므로 경고만 (원하면 재생성 경로 추가)
                Debug.LogWarning($"[Snapshot] No existing object for ID={s.id} kind={s.kind}. Skipped instantiate.");
            }
        }

        Debug.Log($"[Snapshot] applied objects = {snap.objects.Count}");


        // Object 게이지 값 복원(초기화 방지)
        var objectgauge = Shared.ObjectGaugeManager;
        if (objectgauge != null)
        {
            objectgauge.SetObjectGaugeFromSnapshot(snap.totalBoxes, snap.openedBoxes, snap.triggeredTraps, snap.thresholdReached);
        }

        // 스냅샷 1회성 소모(다음 새맵에선 랜덤 스폰 정상화)
        {
            Shared.SceneTransitionManager.explorationSnapshot = null;
        }
    }

    GameObject FindPrefabByName(string prefabName)
    {
        //currentMap 아래에서 스포너 찾기 (여기에 trap/chest 프리팹 리스트가 있음)
        var spawner = currentMap != null ? currentMap.GetComponentInChildren<MapObjectSpawner>(true) : null;

        if (spawner != null)
        {
            if (spawner.trapPrefabs != null)
            {
                foreach (var p in spawner.trapPrefabs)
                    if (p && p.name == prefabName) return p;
            }
            if (spawner.chestPrefabs != null)
            {
                foreach (var p in spawner.chestPrefabs)
                    if (p && p.name == prefabName) return p;
            }
        }
        return null;
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
