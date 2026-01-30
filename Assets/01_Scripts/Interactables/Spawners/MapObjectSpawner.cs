using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.AddressableAssets; // 어드레서블
using UnityEngine.ResourceManagement.AsyncOperations; // 비동기 핸들
using System.Threading.Tasks; // Task 사용

public class MapObjectSpawner : MonoBehaviour
{
    [Header("Trap")]
    public List<AssetReferenceGameObject> trapRefs;
    public int trapMinCount = 0;
    public int trapMaxCount = 1;

    [Header("Object")]
    public List<AssetReferenceGameObject> chestRefs;
    public int chestMinCount = 0;
    public int chestMaxCount = 1;

    [Header("Pattern")]
    public List<AssetReferenceGameObject> patternRefs;
    const int patternCount = 1;

    // 스폰 위치 후보를 저장할 구조체
    private struct SpawnCandidate
    {
        public Tilemap map;
        public Vector3Int cell;
        public Vector3 worldPos;
    }

    public async void Spawn(List<Tilemap> _floors, List<Tilemap> _obstacles, List<Tilemap> _wall, params Collider2D[] _excludeColliders)
    {
        if (_floors == null || _floors.Count == 0) return;

        Dictionary<Vector3Int, Tilemap> highestFloorMap = new Dictionary<Vector3Int, Tilemap>();

        foreach (var map in _floors)
        {
            if (map == null) continue;
            foreach (Vector3Int pos in map.cellBounds.allPositionsWithin)
            {
                if (map.HasTile(pos))
                {
                    highestFloorMap[pos] = map;
                }
            }
        }

        List<SpawnCandidate> candidates = new List<SpawnCandidate>();

        foreach (var kvp in highestFloorMap)
        {
            Vector3Int pos = kvp.Key;
            Tilemap map = kvp.Value;

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
                if (hasWall) continue;
            }

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

            Vector3 worldPos = map.GetCellCenterWorld(pos);
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

        Transform root = _floors[0].transform.parent;
        if (root == null) root = this.transform;

        Transform fallback = this.transform;
        Transform trapContainer = GetOrFallbackContainer(root, "TrapObject", fallback);
        Transform chestContainer = GetOrFallbackContainer(root, "ItemBoxObject", fallback);
        Transform patternContainer = GetOrFallbackContainer(root, "PatternObject", fallback);

        int trapSpawnCount = Random.Range(trapMinCount, trapMaxCount + 1);
        int chestSpawnCount = Random.Range(chestMinCount, chestMaxCount + 1);

        // [문양]
        if (patternRefs != null && patternRefs.Count > 0 && candidates.Count > 0)
        {
            int idx = Random.Range(0, candidates.Count);
            await SpawnObjAsync(patternRefs, candidates[idx], patternContainer, _wall);
            candidates.RemoveAt(idx);
        }

        // [함정]
        for (int i = 0; i < trapSpawnCount && candidates.Count > 0; i++)
        {
            int idx = Random.Range(0, candidates.Count);
            await SpawnObjAsync(trapRefs, candidates[idx], trapContainer, _wall);
            candidates.RemoveAt(idx);
        }

        // [상자]
        for (int i = 0; i < chestSpawnCount && candidates.Count > 0; i++)
        {
            int idx = Random.Range(0, candidates.Count);

            GameObject obj = await SpawnObjAsync(chestRefs, candidates[idx], chestContainer, _wall);

            // 생성된 오브젝트가 있으면 뒤집기
            if (obj != null && candidates[idx].worldPos.x > 0f)
            {
                var sr = obj.GetComponent<SpriteRenderer>();
                if (sr != null) sr.flipX = true;
                else obj.transform.localScale = new Vector3(-1f, 1f, 1f);
            }

            candidates.RemoveAt(idx);
        }
    }

    async Task<GameObject> SpawnObjAsync(List<AssetReferenceGameObject> refs, SpawnCandidate data, Transform parent, List<Tilemap> walls)
    {
        if (refs == null || refs.Count == 0) return null;

        // 랜덤으로 주소(Reference) 선택
        var targetRef = refs[Random.Range(0, refs.Count)];

        // SpawnCandidate 위치 사용
        Vector3 spawnPos = data.worldPos;

        var handle = targetRef.InstantiateAsync(spawnPos, Quaternion.identity, parent);
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            GameObject obj = handle.Result;

            // 컴포넌트 설정 
            var pid = obj.GetComponent<ExplorationPersistId>();
            if (pid == null) pid = obj.AddComponent<ExplorationPersistId>();

            // 이름 설정 (EditorAsset을 통해 원래 프리팹 이름을 가져올 수 있음)
            obj.name = targetRef.editorAsset != null ? targetRef.editorAsset.name : "SpawnedObject";

            var push = obj.GetComponent<PushObject>();
            if (push != null)
            {
                push.SetTilemaps(new List<Tilemap> { data.map }, walls);
            }

            return obj;
        }
        else
        {
            Debug.LogError($"[MapObjectSpawner] 생성 실패: {targetRef.AssetGUID}");
            return null;
        }
    }

    Transform GetOrFallbackContainer(Transform parent, string childName, Transform fallback)
    {
        if (parent == null) return fallback;

        var t = parent.Find(childName);

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

    void OnValidate()
    {
        if (trapMinCount > trapMaxCount) trapMaxCount = trapMinCount;
        if (chestMinCount > chestMaxCount) chestMaxCount = chestMinCount;
    }
}