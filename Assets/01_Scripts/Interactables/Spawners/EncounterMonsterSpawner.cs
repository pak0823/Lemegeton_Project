using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

public class EncounterMonsterSpawner : MonoBehaviour
{
    [Header("Encounter Monster")]
    [Tooltip("스폰할 인카운터 몬스터 프리팹 리스트")]
    public List<AssetReferenceGameObject> monsterRefs = new List<AssetReferenceGameObject>();

    [Tooltip("배치할 인카운터 몬스터의 수")]
    public int spawnCount = 3;

    private struct SpawnCandidate
    {
        public Tilemap map;
        public Vector3Int cell;
        public Vector3 worldPos;
    }

    /// <summary>
    /// 인카운터 몬스터를 맵 빈 공간에 랜덤하게 배치합니다.
    /// 배치가 완료된 좌표 리스트를 반환하여, 이후 배치될 다른 오브젝트(상자 등)가 겹치지 않게 합니다.
    /// </summary>
    public async UniTask<List<Vector3Int>> SpawnAsync(List<Tilemap> _floors, List<Tilemap> _obstacles, List<Tilemap> _wall, List<Vector3Int> excludePositions = null, params Collider2D[] _excludeColliders)
    {
        List<Vector3Int> usedCells = new List<Vector3Int>();

        try
        {
            if (_floors == null || _floors.Count == 0 || monsterRefs == null || monsterRefs.Count == 0) return usedCells;

            Dictionary<Vector3Int, Tilemap> highestFloorMap = new Dictionary<Vector3Int, Tilemap>();

            foreach (var map in _floors)
            {
                if (map == null) continue;
                foreach (Vector3Int pos in map.cellBounds.allPositionsWithin)
                {
                    if (map.HasTile(pos)) highestFloorMap[pos] = map;
                }
            }

            List<SpawnCandidate> candidates = new List<SpawnCandidate>();

            foreach (var kvp in highestFloorMap)
            {
                Vector3Int pos = kvp.Key;
                Tilemap map = kvp.Value;

                if (_wall != null && _wall.Exists(w => w != null && w.HasTile(pos))) continue;
                if (_obstacles != null && _obstacles.Exists(o => o != null && o.HasTile(pos))) continue;

                Vector3 worldPos = map.GetCellCenterWorld(pos);
                worldPos.z = 0;

                bool isExcluded = false;
                if (excludePositions != null && excludePositions.Contains(pos)) isExcluded = true;

                if (!isExcluded && _excludeColliders != null)
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
                Debug.LogWarning("EncounterMonsterSpawner: 몬스터를 생성할 유효한 빈 바닥이 없습니다.");
                return usedCells;
            }

            Transform root = _floors[0].transform.parent;
            if (root == null) root = this.transform;

            Transform fallback = this.transform;
            Transform monsterContainer = GetOrFallbackContainer(root, "EncounterMonsterObject", fallback);

            List<EncounterMonster> spawnedMonsters = new List<EncounterMonster>();

            for (int i = 0; i < spawnCount && candidates.Count > 0; i++)
            {
                int idx = Random.Range(0, candidates.Count);
                GameObject obj = await SpawnObjAsync(monsterRefs, candidates[idx], monsterContainer, _wall);

                if (obj != null)
                {
                    usedCells.Add(candidates[idx].cell);
                    EncounterMonster em = obj.GetComponent<EncounterMonster>();
                    if (em != null) spawnedMonsters.Add(em);
                }
                candidates.RemoveAt(idx);
            }

            // 필드 보스 지정 (1개)
            if (spawnedMonsters.Count > 0)
            {
                int bossIndex = Random.Range(0, spawnedMonsters.Count);
                EncounterMonster boss = spawnedMonsters[bossIndex];
                boss.IsFieldBoss = true;

                Debug.Log($"[EncounterMonsterSpawner] 필드 보스 지정됨 (시각적 효과 없음): {boss.gameObject.name} (위치: {boss.transform.position})");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }

        return usedCells;
    }

    async Task<GameObject> SpawnObjAsync(List<AssetReferenceGameObject> refs, SpawnCandidate data, Transform parent, List<Tilemap> walls)
    {
        if (refs == null || refs.Count == 0) return null;
        var targetRef = refs[Random.Range(0, refs.Count)];
        Vector3 spawnPos = data.worldPos;

        var handle = targetRef.InstantiateAsync(spawnPos, Quaternion.identity, parent);
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            GameObject obj = handle.Result;
            var pid = obj.GetComponent<ExplorationPersistId>();
            if (pid == null) pid = obj.AddComponent<ExplorationPersistId>();

            // 스폰된 몬스터가 PlayerInteractionHandler에서 감지(OverlapCircle + Tag check)될 수 있도록 처리
            obj.tag = "EncounterObject";

            // Layer설정도 필요할 수 있음. 보통 Encounter 레이어는 10이거나 특정 인덱스.
            // "Encounter" 레이어가 확실히 존재한다면: obj.layer = LayerMask.NameToLayer("Encounter");
            int encounterLayer = LayerMask.NameToLayer("Encounter");
            if (encounterLayer != -1)
            {
                obj.layer = encounterLayer;
            }

#if UNITY_EDITOR
            obj.name = targetRef.editorAsset != null ? targetRef.editorAsset.name : "EncounterMonster";
#else
            obj.name = "EncounterMonster";
#endif
            return obj;
        }
        else
        {
            Debug.LogError($"[EncounterMonsterSpawner] 생성 실패: {targetRef.AssetGUID}");
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
}
