using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.Linq;

public class ExplorationEntitySpawner : MonoBehaviour, IMapComponent
{
    private MapManager _manager;

    public void Initialize(MapManager manager)
    {
        _manager = manager;
    }

    public PlayerMovement SpawnPlayer(GameObject playerPrefab, GameObject map, List<Tilemap> floors, List<Tilemap> obstacles, List<Tilemap> walls)
    {
        if (PlayerMovement.Instance != null)
        {
            // Destroy immediately to ensure Instance is cleared
            DestroyImmediate(PlayerMovement.Instance.gameObject); 
        }

        if (playerPrefab == null)
        {
            Debug.LogError("[EntitySpawner] Player Prefab is null!");
            return null;
        }

        GameObject player = Instantiate(playerPrefab);
        var movement = player.GetComponent<PlayerMovement>();
        
        if (movement != null)
        {
            movement.SetTilemaps(floors, obstacles, walls);
        }

        // Find Start Position
        if (map != null)
        {
            var spawn = map.transform.Find("PlayerStart");
            if (spawn != null)
            {
                Vector3 pos = spawn.position;
                pos.z = 0f;
                player.transform.position = pos;
            }
            else
            {
                Debug.LogWarning("[EntitySpawner] 'PlayerStart' point not found in map.");
            }
        }

        // Camera Setup
        var camScript = FindAnyObjectByType<CameraFollow2D>();
        if (camScript != null)
        {
            camScript.target = player.transform;
            camScript.SnapToTarget();
        }

        return movement;
    }

    public void SpawnMapObjects(GameObject map, List<Tilemap> floors, List<Tilemap> obstacles, List<Tilemap> walls)
    {
        if (map == null) return;

        var spawner = map.GetComponentInChildren<MapObjectSpawner>();
        if (spawner == null) return;

        List<Collider2D> excludeList = new List<Collider2D>();
        var tagged = map.GetComponentsInChildren<Collider2D>().Where(c => c.CompareTag("ExcludeSpawn"));
        excludeList.AddRange(tagged);

        spawner.Spawn(floors, obstacles, walls, excludeList.ToArray());
    }
}
