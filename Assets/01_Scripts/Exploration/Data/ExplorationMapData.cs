using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ExplorationMapData : MonoBehaviour
{
    [Header("맵 식별자")]
    [Tooltip("이 맵의 고유 ID. MapConnectionData의 mapId 및 PortalController의 destinationMapId와 일치해야 합니다. (예: moat, camp)")]
    public string mapId;

    [Header("포탈 도착 스폰 포인트")]
    [Tooltip("어느 맵에서 왔는지에 따라 플레이어를 다른 위치에 스폰합니다.")]
    public List<PortalArrivalPoint> arrivalPoints = new List<PortalArrivalPoint>();

    [Header("숨겨진 포탈 (선택)")]
    [Tooltip("특정 타일에 플레이어가 도달하면 숨겨진 맵으로 이동하는 포탈 목록")]
    public List<HiddenPortalData> hiddenPortals = new List<HiddenPortalData>();

    [Header("타일맵 리스트")]
    public List<Tilemap> floorMaps = new List<Tilemap>();     // "Ground" 태그
    public List<Tilemap> wallMaps = new List<Tilemap>();      // "Wall" 태그 (Impassable Layer)
    public List<Tilemap> obstacleMaps = new List<Tilemap>();  // "Obstacle" 태그

#if UNITY_EDITOR
    [ContextMenu("Auto Setup (By Tag)")]
    public void AutoSetup()
    {
        floorMaps.Clear();
        wallMaps.Clear();
        obstacleMaps.Clear();

        // 태그 기준으로 타일맵 자동 수집
        foreach (var tm in GetComponentsInChildren<Tilemap>(true))
        {
            if (tm.CompareTag("Ground"))
            {
                if (!floorMaps.Contains(tm)) floorMaps.Add(tm);
            }
            else if (tm.CompareTag("Wall"))
            {
                if (!wallMaps.Contains(tm)) wallMaps.Add(tm);
            }
            else if (tm.CompareTag("Obstacle"))
            {
                if (!obstacleMaps.Contains(tm)) obstacleMaps.Add(tm);
            }
        }

        // 정렬: SortingOrder 오름차순
        floorMaps.Sort((a, b) =>
        {
            var ra = a.GetComponent<TilemapRenderer>();
            var rb = b.GetComponent<TilemapRenderer>();
            int oa = ra ? ra.sortingOrder : 0;
            int ob = rb ? rb.sortingOrder : 0;
            return oa.CompareTo(ob);
        });

        // 포탈 스폰 및 히든 포탈 초기화 추가
        arrivalPoints.Clear();
        hiddenPortals.Clear();

        // 임시 스폰 마커들을 담을 부모 컨테이너 생성/탐색
        Transform spawnContainer = transform.Find("_SpawnPoints");
        if (spawnContainer == null)
        {
            GameObject containerGo = new GameObject("_SpawnPoints");
            containerGo.transform.SetParent(transform);
            containerGo.transform.localPosition = Vector3.zero;
            spawnContainer = containerGo.transform;
        }
        hiddenPortals.Clear();

        // 1. 일반 포탈 컨트롤러 스캔 (도착 스폰 포인트 자동 등록)
        // PortalController의 destinationMapId를 fromMapId로 역산하여 스폰 포인트를 세팅합니다.
        foreach (var portal in GetComponentsInChildren<PortalController>(true))
        {
            if (!string.IsNullOrEmpty(portal.destinationMapId))
            {
                bool exists = arrivalPoints.Exists(p => p.fromMapId == portal.destinationMapId);
                if (!exists)
                {
                    // 1칸 옆 빈칸 좌표 탐색
                    Vector3 spawnPos = GetWalkableAdjacentWorldPos(portal.transform.position);

                    // 해당 좌표에 빈 마커 게임오브젝트 생성 (또는 재사용)
                    string markerName = $"Spawn_{portal.destinationMapId}";
                    Transform marker = spawnContainer.Find(markerName);
                    if (marker == null)
                    {
                        GameObject go = new GameObject(markerName);
                        go.transform.SetParent(spawnContainer);
                        marker = go.transform;
                    }
                    marker.position = spawnPos;

                    arrivalPoints.Add(new PortalArrivalPoint
                    {
                        fromMapId = portal.destinationMapId,
                        spawnTransform = marker
                    });
                }
            }
        }

        // 2. 히든 포탈 컨트롤러 스캔
        foreach (var hidden in GetComponentsInChildren<HiddenPortalController>(true))
        {
            if (!string.IsNullOrEmpty(hidden.hiddenMapId))
            {
                bool exists = hiddenPortals.Exists(h => h.hiddenMapId == hidden.hiddenMapId);
                if (!exists)
                {
                    hiddenPortals.Add(new HiddenPortalData
                    {
                        hiddenMapId = hidden.hiddenMapId,
                        hiddenMapPrefab = hidden.hiddenMapPrefab,
                        exitSpawnTransform = hidden.exitSpawnTransform,
                        triggerTileCell = hidden.triggerTileCell
                    });
                }
            }
        }

        Debug.Log($"[ExplorationMapData] Auto Setup 완료: Floor({floorMaps.Count}), Wall({wallMaps.Count}), Obstacle({obstacleMaps.Count}), ArrivalPoints({arrivalPoints.Count}), HiddenPortals({hiddenPortals.Count})");
        UnityEditor.EditorUtility.SetDirty(this);
    }

    /// <summary>
    /// 지정된 월드 좌표(보통 포탈 위치) 주변 상하좌우 4방향을 검사하여
    /// 바닥이 있고 벽/장애물이 없는 가장 첫 번째 빈칸의 월드 좌표를 반환합니다.
    /// 모두 막혀있을 경우 원래의 중심 좌표를 그대로 반환합니다.
    /// </summary>
    private Vector3 GetWalkableAdjacentWorldPos(Vector3 centerWorldPos)
    {
        if (floorMaps.Count == 0) return centerWorldPos;

        // 기준 타일맵 (가장 첫 번째 혹은 메인 바닥 타일맵 기준)
        Tilemap mainFloor = floorMaps[0];
        Vector3Int centerCell = mainFloor.WorldToCell(centerWorldPos);

        // 헥사 타일(Pointed-Top)의 인접 6방향 검색
        // Y좌표 짝/홀수에 따라 필요한 오프셋이 다릅니다. (PathfindingSystem의 로직 차용)
        bool isOddRow = (centerCell.y & 1) != 0;

        Vector3Int[] directions;
        if (isOddRow)
        {
            directions = new Vector3Int[]
            {
                new Vector3Int(0, -1, 0), // SW (아래쪽을 우선 검사하는 경향 유지)
                new Vector3Int(1, -1, 0), // SE
                new Vector3Int(-1, 0, 0), // W
                new Vector3Int(1, 0, 0),  // E
                new Vector3Int(0, 1, 0),  // NW
                new Vector3Int(1, 1, 0)   // NE
            };
        }
        else
        {
            directions = new Vector3Int[]
            {
                new Vector3Int(-1, -1, 0),// SW
                new Vector3Int(0, -1, 0), // SE
                new Vector3Int(-1, 0, 0), // W
                new Vector3Int(1, 0, 0),  // E
                new Vector3Int(-1, 1, 0), // NW
                new Vector3Int(0, 1, 0)   // NE
            };
        }

        foreach (var dir in directions)
        {
            Vector3Int checkCell = centerCell + dir;

            // 1. 바닥(Floor) 타일이 존재하는가?
            bool hasFloor = false;
            foreach (var floor in floorMaps)
            {
                if (floor.HasTile(checkCell))
                {
                    hasFloor = true;
                    break;
                }
            }
            if (!hasFloor) continue;

            // 2. 벽(Wall) 타일이 존재하는가?
            bool isWall = false;
            foreach (var wall in wallMaps)
            {
                if (wall.HasTile(checkCell))
                {
                    isWall = true;
                    break;
                }
            }
            if (isWall) continue;

            // 3. 장애물(Obstacle) 타일이 존재하는가?
            bool isObstacle = false;
            foreach (var obs in obstacleMaps)
            {
                if (obs.HasTile(checkCell))
                {
                    isObstacle = true;
                    break;
                }
            }
            if (isObstacle) continue;

            // 모든 조건을 만족하면 해당 셀의 중앙 월드 좌표 반환
            return mainFloor.GetCellCenterWorld(checkCell);
        }

        // 4방향 모두 막혀있으면 원래 거시기 좌표로 강제 폴백
        Debug.LogWarning($"[ExplorationMapData] {centerWorldPos} 주변에 스폰 가능한 빈칸이 없습니다. 포탈 위치에 그대로 스폰합니다.");
        return centerWorldPos;
    }
#endif
}
