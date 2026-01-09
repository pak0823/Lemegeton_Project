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

        // 프리팹 설정 후 실제로 맵을 생성하도록 호출
        ResetExplorationMap();
    }

    GameObject GetRandomNormalMapPrefab()
    {
        if (stageDB == null) return null;

        // 현재 스테이지 번호와 일치하는 데이터 찾기
        var data = stageDB.normalStages.FirstOrDefault(x => x.stageNumber == currentStage);

        // 데이터가 없거나, 프리팹 배열이 비어있는지 확인
        // (주의: 배열은 Count가 아니라 Length 입니다)
        if (data == null || data.normalMapPrefabs == null || data.normalMapPrefabs.Length == 0)
            return null;

        // 랜덤 반환
        return data.normalMapPrefabs[Random.Range(0, data.normalMapPrefabs.Length)];
    }

    void InstantiatePlayer(List<Tilemap> _floors, List<Tilemap> _obstacles, List<Tilemap> _walls)
    {
        if (playerPrefab == null) return;

        // 기존 플레이어 제거 (중복 방지)
        if (Shared.PlayerMovement != null)
            Destroy(Shared.PlayerMovement.gameObject);

        // 플레이어 생성
        GameObject p = Instantiate(playerPrefab);
        var pm = p.GetComponent<PlayerMovement>();

        if (pm != null)
        {
            // 리스트 형태의 바닥 전달
            pm.SetTilemaps(_floors, _obstacles, _walls);
            Shared.PlayerMovement = pm;
        }

        // PlayerStart 위치로 이동
        if (currentMap != null)
        {
            var spawn = currentMap.transform.Find("PlayerStart");
            if (spawn != null)
            {
                Vector3 spawnPos = spawn.position;
                spawnPos.z = 0f;
                p.transform.position = spawnPos;
            }
                
            else
                Debug.LogWarning("[MapManager] 맵에 'PlayerStart' 오브젝트가 없습니다.");
        }

        var camScript = FindAnyObjectByType<CameraFollow2D>();
        if (camScript != null)
        {
            camScript.target = p.transform; // 타겟 갱신
            camScript.SnapToTarget();       // 부드러운 이동 없이 즉시 시점 이동
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
        var spawnPoint = currentMap.transform.Find("PlayerStart");

        if (spawner == null)
        {
            // 스포너가 필수는 아닐 수 있으므로 경고 없이 리턴하거나 로그 출력
            return;
        }

        // 스폰 제외 영역 설정 (PlayerStart 주변 등)
        List<Collider2D> excludeList = new List<Collider2D>();

        // Tag가 "ExcludeSpawn"인 콜라이더들
        var tagged = currentMap.GetComponentsInChildren<Collider2D>()
            .Where(c => c.CompareTag("ExcludeSpawn"));
        excludeList.AddRange(tagged);

        // 플레이어 시작 위치 주변도 제외하고 싶다면 가상의 범위 추가 가능
        // 지금은 PlayerStart 오브젝트에 콜라이더가 있다면 그것을 사용한다고 가정

        spawner.Spawn(_floors, _obstacles, _walls, excludeList.ToArray());
    }

    void HookCameraToPlayer(Transform player)
    {
        // 메인 카메라의 CameraFollow2D 찾기
        var cam = Camera.main ? Camera.main.GetComponent<CameraFollow2D>()
                              : FindObjectOfType<CameraFollow2D>(true);
        if (!cam) return;

        // 타깃을 갓 생성된 플레이어로 지정 + 즉시 스냅
        cam.SetTarget(player, snap: true);

        // (선택) 월드 경계 콜라이더 자동 지정
        // - 우선 WorldBounds라는 이름의 오브젝트를 찾고
        // - 없으면 CompositeCollider2D → BoxCollider2D 순으로 폴백
        Collider2D bounds = null;
        var t = currentMap.transform.Find("WorldBounds");
        if (t) t.TryGetComponent(out bounds);
        if (!bounds) bounds = currentMap.GetComponentInChildren<CompositeCollider2D>(true);
        if (!bounds) bounds = currentMap.GetComponentInChildren<BoxCollider2D>(true);

        if (bounds) cam.worldBounds = bounds;
    }

    void ApplyExplorationSnapshot(ExplorationSnapshot snap, Tilemap floorMap, List<Tilemap> wallMap)
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
                    existPush.SetTilemaps(new List<Tilemap> { floorMap }, wallMap);
                existIp.LoadState(s);
                continue;
            }

            // 없으면(랜덤 스폰되던 Chest/Trap)만 프리팹으로 재생성
            if (s.kind == "Chest" || s.kind == "Trap" || s.kind == "Encounter")
            {
                // Trap: 발동/비활성이면 스킵
                if (s.kind == "Trap" && (s.b1 || !s.b2))
                    continue;

                // Encounter: consumed면 스킵
                if (s.kind == "Encounter" && s.b1)
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
                    push.SetTilemaps(new List<Tilemap> { floorMap }, wallMap);

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
        // 기존 맵 및 플레이어 정리
        if (currentMap != null) Destroy(currentMap);
        if (Shared.PlayerMovement != null) Destroy(Shared.PlayerMovement.gameObject);

        if (backUpMapPrefab == null)
        {
            Debug.LogError("[MapManager] 생성할 맵 프리팹이 없습니다!");
            return;
        }

        // 맵 생성
        currentMap = Instantiate(backUpMapPrefab, Vector3.zero, Quaternion.identity, gridParent);

        var (floorMaps, obstacleMaps, wallMaps) = FindTilemapsMulti(currentMap);

        if (floorMaps.Count == 0)
        {
            Debug.LogError("Floor 타일맵을 찾을 수 없습니다! (WalkableLayers 하위에 있는지 확인 필요)");
            return;
        }

        // 플레이어 생성
        InstantiatePlayer(floorMaps, obstacleMaps, wallMaps);

        // TrySpawnObjects 호출 시 장애물/벽 정보도 함께 전달
        TrySpawnObjects(floorMaps, obstacleMaps, wallMaps);
    }

    // 바닥 맵과 장애물 맵을 따로 찾아서 반환하도록 변경
    (List<Tilemap> floors, List<Tilemap> obstacles, List<Tilemap> walls) FindTilemapsMulti(GameObject map)
    {
        List<Tilemap> floors = new List<Tilemap>(); //이동 가능 타일 리스트
        List<Tilemap> obstacles = new List<Tilemap>(); // 장애물 리스트
        List<Tilemap> walls = new List<Tilemap>();  //벽 타일 리스트

        foreach (var tm in map.GetComponentsInChildren<Tilemap>())
        {
            // 벽 (Wall)
            if (tm.CompareTag("Wall"))
            {
                walls.Add(tm);
            }
            // 장애물 (Obstacle)
            else if (tm.CompareTag("Obstacle"))
            {
                obstacles.Add(tm);
            }
            // 바닥 (Floor)
            else if (tm.CompareTag("Ground"))
            {
                floors.Add(tm);
            }
            // 태그가 없는 경우(Untagged)에 대한 처리
            else
            {
                Debug.LogWarning($"[MapManager] Tag가 설정되지 않은 타일맵 발견: {tm.name}");
            }
        }
        return (floors, obstacles, walls);
    }
}
