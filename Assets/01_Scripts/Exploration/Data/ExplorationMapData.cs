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

        Debug.Log($"[ExplorationMapData] Auto Setup 완료: Floor({floorMaps.Count}), Wall({wallMaps.Count}), Obstacle({obstacleMaps.Count})");
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
