using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MapObjectSpawner : MonoBehaviour
{
    [Header("Trap")]
    public List<GameObject> trapPrefabs;
    public int trapMinCount = 0;
    public int trapMaxCount = 1;

    [Header("Object")]
    public List<GameObject> chestPrefabs;
    public int chestMinCount = 0;
    public int chestMaxCount = 1;

    [Header("Pattern")]
    public List<GameObject> patternPrefabs;
    const int patternCount = 1;

    // 스폰 위치 후보를 저장할 구조체 (어느 타일맵의 어느 좌표인가)
    private struct SpawnCandidate
    {
        public Tilemap map;
        public Vector3Int cell;
        public Vector3 worldPos;
    }

    // 맵 생성 직후 MapManager가 호출
    public void Spawn(List<Tilemap> _floors, List<Tilemap> _obstacles, List<Tilemap> _wall, params Collider2D[] _excludeColliders)
    {
        if (_floors == null || _floors.Count == 0) return;

        // 좌표별 가장 높은 층의 타일맵 찾기
        // Dictionary를 사용해 (x,y) 좌표 하나당 최상단 타일맵 하나만 남깁니다.
        // floors 리스트는 보통 [Ground1, Ground2, Ground3] 순서이므로, 나중에 나오는 게 위쪽 층입니다.
        Dictionary<Vector3Int, Tilemap> highestFloorMap = new Dictionary<Vector3Int, Tilemap>();

        foreach (var map in _floors)
        {
            if (map == null) continue;
            foreach (Vector3Int pos in map.cellBounds.allPositionsWithin)
            {
                if (map.HasTile(pos))
                {
                    // 같은 좌표에 이미 타일이 있어도, 더 나중(높은 층)의 타일로 덮어씁니다.
                    highestFloorMap[pos] = map;
                }
            }
        }

        // 2. 유효성 검사 (벽, 장애물, 제외영역 필터링)
        List<SpawnCandidate> candidates = new List<SpawnCandidate>();

        foreach (var kvp in highestFloorMap)
        {
            Vector3Int pos = kvp.Key;
            Tilemap map = kvp.Value; // 해당 좌표의 최상단 타일맵

            // 벽이 있는 곳 제외
           
            if (_wall != null)
            {
                bool hasWall = false;

                foreach (var wall in _wall)
                {
                    if (wall.HasTile(pos))
                    {
                        hasWall = true;
                        break;
                    }
                }
            }

            // 장애물(물, 구멍 등)이 있는 곳 제외
            // 바닥(Ground 0)이 있어도 그 위에 물(Water 0)이 칠해져 있다면 생성 불가
            if (_obstacles != null)
            {
                bool isObstacle = false;
                foreach (var obsMap in _obstacles)
                {
                    if (obsMap != null && obsMap.HasTile(pos))
                    {
                        isObstacle = true;
                        break;
                    }
                }
                if (isObstacle) continue;
            }

            // 스폰 제외 영역(플레이어 시작점 등) 체크
            // GetCellCenterWorld는 Tile Anchor 설정이 반영된 시각적 위치를 반환하므로 [요청사항 1] 해결
            Vector3 worldPos = map.GetCellCenterWorld(pos);

            // 2D 정렬을 위해 Z는 0으로 맞추되, 시각적 Y위치는 map의 Anchor설정을 따름
            // (만약 오브젝트가 타일 뒤로 숨는다면 Sprite Sort Point를 Pivot으로 하거나 Order in Layer 조정 필요)
            worldPos.z = 0;

            bool isExcluded = false;
            if (_excludeColliders != null)
            {
                foreach (var col in _excludeColliders)
                {
                    if (col != null && col.OverlapPoint(worldPos))
                    {
                        isExcluded = true;
                        break;
                    }
                }
            }

            if (!isExcluded)
            {
                candidates.Add(new SpawnCandidate { map = map, cell = pos, worldPos = worldPos });
            }
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning("MapObjectSpawner: 오브젝트를 생성할 유효한 빈 바닥이 없습니다.");
            return;
        }

        // 3. 컨테이너 준비
        Transform root = _floors[0].transform.parent; // WalkableLayers
        if (root == null) root = this.transform;

        Transform fallback = this.transform;
        Transform trapContainer = GetOrFallbackContainer(root, "TrapObject", fallback);
        Transform chestContainer = GetOrFallbackContainer(root, "ItemBoxObject", fallback);
        Transform patternContainer = GetOrFallbackContainer(root, "PatternObject", fallback);

        // 4. 오브젝트 배치 (랜덤 선택)
        int trapSpawnCount = Random.Range(trapMinCount, trapMaxCount + 1);
        int chestSpawnCount = Random.Range(chestMinCount, chestMaxCount + 1);

        // [문양]
        if (patternPrefabs != null && patternPrefabs.Count > 0 && candidates.Count > 0)
        {
            int idx = Random.Range(0, candidates.Count);
            SpawnObj(patternPrefabs, candidates[idx], patternContainer, _wall);
            candidates.RemoveAt(idx);
        }

        // [함정]
        for (int i = 0; i < trapSpawnCount && candidates.Count > 0; i++)
        {
            int idx = Random.Range(0, candidates.Count);
            SpawnObj(trapPrefabs, candidates[idx], trapContainer, _wall);
            candidates.RemoveAt(idx);
        }

        // [상자]
        for (int i = 0; i < chestSpawnCount && candidates.Count > 0; i++)
        {
            int idx = Random.Range(0, candidates.Count);
            GameObject obj = SpawnObj(chestPrefabs, candidates[idx], chestContainer, _wall);

            if (obj != null && candidates[idx].worldPos.x > 0f)
            {
                var sr = obj.GetComponent<SpriteRenderer>();
                if (sr != null) sr.flipX = true;
                else obj.transform.localScale = new Vector3(-1f, 1f, 1f);
            }

            candidates.RemoveAt(idx);
        }
    }
    GameObject SpawnObj(List<GameObject> prefabs, SpawnCandidate data, Transform parent, List<Tilemap> walls)
    {
        if (prefabs == null || prefabs.Count == 0) return null;

        var prefab = prefabs[Random.Range(0, prefabs.Count)];

        // SpawnCandidate에서 이미 계산된, 최상단 타일 기준 위치 사용
        Vector3 spawnPos = data.worldPos;

        GameObject obj = Instantiate(prefab, spawnPos, Quaternion.identity, parent);

        var pid = obj.GetComponent<ExplorationPersistId>();
        if (pid == null) pid = obj.AddComponent<ExplorationPersistId>();
        obj.name = prefab.name;

        var push = obj.GetComponent<PushObject>();
        if (push != null)
        {
            // 단일 맵을 리스트로 포장해서 전달
            push.SetTilemaps(new List<Tilemap> { data.map }, walls);
        }

        return obj;
    }

    Transform GetOrFallbackContainer(Transform parent, string childName, Transform fallback)
    {
        if (parent == null) return fallback;
        
        // 부모 바로 아래에 있는지 찾기
        var t = parent.Find(childName);
        
        // 없으면 새로 생성
        if (t == null)
        {
            GameObject go = new GameObject(childName);
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;
            t = go.transform;
        }
        return t;
    }

    // min > max가 되는 것을 방지
    void OnValidate()
    {
        if (trapMinCount > trapMaxCount) trapMaxCount = trapMinCount;
        if (chestMinCount > chestMaxCount) chestMaxCount = chestMinCount;
    }
}
