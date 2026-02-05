using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;

public class ExplorationMapLoader : MonoBehaviour, IMapComponent
{
    private MapManager _manager;

    public void Initialize(MapManager manager)
    {
        _manager = manager;
    }

    public GameObject CurrentMap { get; private set; }

    // Backup Prefab for reloading
    private GameObject _mapPrefab;

    public void SetMapPrefab(GameObject prefab)
    {
        _mapPrefab = prefab;
    }

    public void LoadMap(Transform parent, bool forceCreate = false)
    {
        if (!forceCreate)
        {
            // Try to find existing map first
            if (FindExistingMap(parent))
            {
                Debug.Log("[MapLoader] Found existing map in scene.");
                return;
            }
        }

        // Clean up old map if forcing create or not found
        UnloadMap();

        if (_mapPrefab == null)
        {
            Debug.LogError("[MapLoader] No Map Prefab assigned!");
            return;
        }

        Debug.Log($"[MapLoader] Instantiating new map: {_mapPrefab.name}");
        CurrentMap = Instantiate(_mapPrefab, Vector3.zero, Quaternion.identity, parent);
    }

    public void UnloadMap()
    {
        if (CurrentMap != null)
        {
            Destroy(CurrentMap);
            CurrentMap = null;
        }
    }

    // Refactored logic from MapManager.ResetExplorationMap (The Fix)
    private bool FindExistingMap(Transform gridParent)
    {
        // 1. Check direct child of Grid
        if (gridParent != null && gridParent.childCount > 0)
        {
            CurrentMap = gridParent.GetChild(0).gameObject;
            return true;
        }

        // 2. Check by MapObjectSpawner in scene
        var spawners = FindObjectsOfType<MapObjectSpawner>(true);
        if (spawners.Length > 0)
        {
            // Try to find the root map object from spawner
            Transform current = spawners[0].transform;
            
            // Traverse UP including self
            while (current != null)
            {
                // Check if this looks like a Map Root
                if (current.name.Contains("DEVOTION") || current.name.Contains("BRIDGE") || 
                    current.name.Contains("Map") || current.GetComponent<MapObjectSpawner>() != null)
                {
                    // Verify it's not just a spawner child object unless it's the root
                    // If the object name is generic but has spawner, we assume it's the map part
                    
                    // If we found a named map root, verify parentage
                    if (current.parent == gridParent || current.parent == null || current.parent.name == "Grid")
                    {
                        CurrentMap = current.gameObject;
                        return true;
                    }
                }
                current = current.parent;
            }

            // Fallback: direct parent of spawner (Common case: Spawner is child of Map)
            if (spawners[0].transform.parent != null)
            {
                CurrentMap = spawners[0].transform.parent.gameObject;
                return true;
            }
        }

        return false;
    }
}
