using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 맵 내 엔티티(플레이어, 오브젝트) 생성을 담당합니다.
/// </summary>
public class ExplorationEntitySpawner : MonoBehaviour, IMapComponent
{
    private MapManager _manager;

    public void Initialize(MapManager manager)
    {
        _manager = manager;
    }

    /// <summary>
    /// 플레이어를 랜덤 이동 가능 타일에 배치합니다.
    /// (바닥 O, 장애물 X, 벽 X 인 타일 중 랜덤 선택)
    ///
    /// usedCell: 선택된 타일 셀 좌표. 이후 오브젝트 배치 시 해당 위치를 제외합니다.
    /// </summary>
    public PlayerMovement SpawnPlayer(
        GameObject playerPrefab,
        GameObject map,
        List<Tilemap> floors,
        List<Tilemap> obstacles,
        List<Tilemap> walls,
        out Vector3Int usedCell)
    {
        usedCell = new Vector3Int(int.MinValue, int.MinValue, 0);

        if (PlayerMovement.Instance != null)
            DestroyImmediate(PlayerMovement.Instance.gameObject);

        if (playerPrefab == null)
        {
            Debug.LogError("[EntitySpawner] Player Prefab is null!");
            return null;
        }

        GameObject player = Instantiate(playerPrefab);
        var movement = player.GetComponent<PlayerMovement>();

        Vector3 spawnPos = Vector3.zero;
        bool found = false;

        // 이동 가능한 타일 중 랜덤 선택
        if (map != null && floors != null && floors.Count > 0)
        {
            var candidates = BuildWalkableCandidates(floors, obstacles, walls);
            if (candidates.Count > 0)
            {
                int randomIdx = Random.Range(0, candidates.Count);
                var picked = candidates[randomIdx];

                spawnPos = picked.worldPos;
                usedCell = picked.cell;
                found = true;
                Debug.Log($"[EntitySpawner] 랜덤 타일 스폰: 셀={usedCell}, 위치={spawnPos}");
            }
        }

        if (found)
        {
            spawnPos.z = 0f;
            player.transform.position = spawnPos;
        }
        else
        {
            Debug.LogWarning("[EntitySpawner] 이동 가능한 타일을 찾지 못해 원점(0,0,0)에 배치합니다.");
        }

        // 카메라 연결
        var camScript = FindAnyObjectByType<CameraFollow2D>();
        if (camScript != null)
        {
            camScript.target = player.transform;
            camScript.SnapToTarget();
        }

        return movement;
    }

    /// <summary>
    /// 맵 오브젝트를 배치합니다.
    /// 플레이어가 선점한 타일(playerUsedCell)을 제외하고
    /// pattern → object(상자) → trap(함정) 순서로 배치합니다.
    /// </summary>
    public void SpawnMapObjects(
        GameObject map,
        List<Tilemap> floors,
        List<Tilemap> obstacles,
        List<Tilemap> walls,
        Vector3Int playerUsedCell)
    {
        if (map == null) return;

        var spawner = map.GetComponentInChildren<MapObjectSpawner>();
        if (spawner == null) return;

        // 플레이어가 사용한 셀 제외
        List<Vector3Int> excludePositions = new List<Vector3Int>();
        if (playerUsedCell.x != int.MinValue)
            excludePositions.Add(playerUsedCell);

        // ExcludeSpawn 태그가 붙은 콜라이더 영역 제외
        List<Collider2D> excludeColliders = map
            .GetComponentsInChildren<Collider2D>()
            .Where(c => c.CompareTag("ExcludeSpawn"))
            .ToList();

        spawner.Spawn(floors, obstacles, walls, excludePositions, excludeColliders.ToArray()).Forget();
    }

    // ── 내부 유틸 ─────────────────────────────────────────

    private struct WalkableCandidate
    {
        public Tilemap map;
        public Vector3Int cell;
        public Vector3 worldPos;
    }

    /// <summary>
    /// 이동 가능한 타일 후보 목록을 반환합니다.
    /// (바닥 타일 존재 + 장애물 없음 + 벽 없음)
    /// </summary>
    private List<WalkableCandidate> BuildWalkableCandidates(
        List<Tilemap> floors,
        List<Tilemap> obstacles,
        List<Tilemap> walls)
    {
        // 각 셀의 가장 위에 있는 바닥 타일맵 결정
        var highestFloorMap = new Dictionary<Vector3Int, Tilemap>();
        foreach (var floor in floors)
        {
            if (floor == null) continue;
            foreach (Vector3Int pos in floor.cellBounds.allPositionsWithin)
            {
                if (floor.HasTile(pos))
                    highestFloorMap[pos] = floor;
            }
        }

        var result = new List<WalkableCandidate>();
        foreach (var kvp in highestFloorMap)
        {
            Vector3Int pos = kvp.Key;
            Tilemap map = kvp.Value;

            // 벽 타일 셀 제외
            if (walls != null && walls.Any(w => w != null && w.HasTile(pos)))
                continue;

            // 장애물 타일 셀 제외
            if (obstacles != null && obstacles.Any(o => o != null && o.HasTile(pos)))
                continue;

            Vector3 worldPos = map.GetCellCenterWorld(pos);
            worldPos.z = 0f;

            result.Add(new WalkableCandidate { map = map, cell = pos, worldPos = worldPos });
        }

        return result;
    }
}
