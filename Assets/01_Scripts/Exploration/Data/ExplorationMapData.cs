using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ExplorationMapData : MonoBehaviour
{
    [Header("타일맵 리스트")]
    public List<Tilemap> floorMaps = new List<Tilemap>();     // "Ground" or "Floor"
    public List<Tilemap> wallMaps = new List<Tilemap>();      // "Wall" (with Impassable Layer)
    public List<Tilemap> obstacleMaps = new List<Tilemap>();  // "Obstacle"
    
    [Header("스폰 포인트 (Optional)")]
    // 기존에는 하위의 PlayerSpawnPoint 컴포넌트를 찾거나 PlayerStart 이름을 찾았으나,
    // 여기서 명시적으로 관리할 수도 있음.
    public List<Transform> spawnPoints = new List<Transform>(); 

#if UNITY_EDITOR
    [ContextMenu("Auto Setup (By Tag)")]
    public void AutoSetup()
    {
        floorMaps.Clear();
        wallMaps.Clear();
        obstacleMaps.Clear();
        spawnPoints.Clear();

        // 1. Tilemap Auto Setup
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
        
        // 정렬: SortingOrder 순
        floorMaps.Sort((a, b) => 
        {
            var ra = a.GetComponent<TilemapRenderer>();
            var rb = b.GetComponent<TilemapRenderer>();
            int oa = ra ? ra.sortingOrder : 0;
            int ob = rb ? rb.sortingOrder : 0;
            return oa.CompareTo(ob);
        });

        // 2. Spawn Point Auto Setup (PlayerSpawnPoint 컴포넌트 기반)
        var spawnComps = GetComponentsInChildren<PlayerSpawnPoint>(true);
        foreach (var sp in spawnComps)
        {
            if (!spawnPoints.Contains(sp.transform)) spawnPoints.Add(sp.transform);
        }

        Debug.Log($"[ExplorationMapData] Auto Setup Complete: Floor({floorMaps.Count}), Wall({wallMaps.Count}), Obstacle({obstacleMaps.Count}), Spawns({spawnPoints.Count})");
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
#endif
}
