using System.Collections.Generic;
using System.Linq;
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

    private Tilemap tilemap;

    private void Awake()
    {
        // 자식에 붙은 타일맵을 자동으로 찾아서 저장
        tilemap = GetComponentInChildren<Tilemap>();

        if (tilemap == null)
            Debug.LogError("MapObjectSpawner: 자식에서 Tilemap을 찾을 수 없습니다.");
    }

    // 맵 생성 직후 MapManager가 호출
    // excludeColliders 배열에 들어있는 콜라이더들의 영역 위엔 오브젝트를 스폰하지 않음
    public void Spawn(Tilemap tilemap, params Collider2D[] excludeColliders)
    {
        // Random.Range(int, int)는 최소 포함 / 최대 미포함이기 때문에 최대에 +1을함
        int trapSpawnCount = Random.Range(trapMinCount, trapMaxCount + 1);
        int chestSpawnCount = Random.Range(chestMinCount, chestMaxCount + 1);

        // 컨테이너 찾기
        Transform root = tilemap.transform.parent;
        Transform fallback = this.transform;

        Transform trapContainer = GetOrFallbackContainer(root, "TrapObject", fallback);
        Transform chestContainer = GetOrFallbackContainer(root, "ItemBoxObject", fallback);
        Transform patternContainer = GetOrFallbackContainer(root, "PatternObject", fallback);

        var floorCells = new List<Vector3Int>();
        foreach (Vector3Int pos in tilemap.cellBounds.allPositionsWithin)
        {
            // 바닥 타일인지
            if (tilemap.GetTile(pos)?.name.Contains("Floor") != true)
                continue;

            // excludeColliders 에 들어온 모든 콜라이더 영역 제외
            Vector3 worldPos = tilemap.GetCellCenterWorld(pos);
            if (excludeColliders.Any(col => col != null && col.OverlapPoint(worldPos)))
                continue;

            floorCells.Add(pos);
        }

        // 문양 배치 (오직 1개)
        if (patternPrefabs != null && patternPrefabs.Count > 0 && floorCells.Count > 0)
        {
            int idx = Random.Range(0, floorCells.Count);
            Vector3 worldPos = tilemap.GetCellCenterWorld(floorCells[idx]);
            var prefab = patternPrefabs[Random.Range(0, patternPrefabs.Count)];

            GameObject obj = Instantiate(prefab, worldPos, Quaternion.identity, patternContainer);

            var pid = obj.GetComponent<ExplorationPersistId>();
            if (pid == null) pid = obj.AddComponent<ExplorationPersistId>();
            obj.name = prefab.name;

            floorCells.RemoveAt(idx);
        }

        // 함정 배치
        for (int i = 0; i < trapSpawnCount && floorCells.Count > 0; i++)
        {
            int idx = Random.Range(0, floorCells.Count);
            Vector3 worldPos = tilemap.GetCellCenterWorld(floorCells[idx]);
            var prefab = trapPrefabs[Random.Range(0, trapPrefabs.Count)];

            GameObject obj = Instantiate(prefab, worldPos, Quaternion.identity, trapContainer);

            var pid = obj.GetComponent<ExplorationPersistId>();
            if (pid == null) pid = obj.AddComponent<ExplorationPersistId>();
            obj.name = prefab.name;

            floorCells.RemoveAt(idx);
        }

        // 상자 배치
        for (int i = 0; i < chestSpawnCount && floorCells.Count > 0; i++)
        {
            int idx = Random.Range(0, floorCells.Count);
            Vector3 worldPos = tilemap.GetCellCenterWorld(floorCells[idx]);
            var prefab = chestPrefabs[Random.Range(0, chestPrefabs.Count)];

            GameObject obj = Instantiate(prefab, worldPos, Quaternion.identity, chestContainer);

            var pid = obj.GetComponent<ExplorationPersistId>();
            if (pid == null) pid = obj.AddComponent<ExplorationPersistId>();
            obj.name = prefab.name;

            if (worldPos.x > 0f)
            {
                var sr = obj.GetComponent<SpriteRenderer>();
                if (sr != null) sr.flipX = true;
                else obj.transform.localScale = new Vector3(-1f, 1f, 1f);
            }

            floorCells.RemoveAt(idx);
        }
    }

    Transform GetOrFallbackContainer(Transform tilemapParent, string childName, Transform fallback)
    {
        if (tilemapParent == null) return fallback;

        var t = tilemapParent.Find(childName);
        if (t == null)
        {
            Debug.LogWarning($"MapObjectSpawner: '{childName}' 컨테이너를 찾을 수 없습니다. fallback을 사용합니다.");
            return fallback;
        }
        return t;
    }

    // min > max가 되는 것을 방지
    void OnValidate()
    {
        if (trapMinCount > trapMaxCount)
            trapMaxCount = trapMinCount;

        if (chestMinCount > chestMaxCount)
            chestMaxCount = chestMinCount;
    }
}
