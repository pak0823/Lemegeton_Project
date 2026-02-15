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
            DestroyImmediate(PlayerMovement.Instance.gameObject);
        }

        if (playerPrefab == null)
        {
            Debug.LogError("[EntitySpawner] Player Prefab is null!");
            return null;
        }

        GameObject player = Instantiate(playerPrefab);
        var movement = player.GetComponent<PlayerMovement>();

        // Find Start Position (New Logic)
        if (map != null)
        {
            Vector3 spawnPos = Vector3.zero;
            bool found = false;

            // 1. Try Find Components
            var points = map.GetComponentsInChildren<PlayerSpawnPoint>();
            if (points != null && points.Length > 0)
            {
                var target = points[Random.Range(0, points.Length)];
                spawnPos = target.transform.position;
                found = true;
                // Debug.Log($"[EntitySpawner] Spawned at 'PlayerSpawnPoint' ({spawnPos})");
            }

            // 2. Fallback to Name Search
            if (!found)
            {
                var spawn = map.transform.Find("PlayerStart");
                if (spawn != null)
                {
                    spawnPos = spawn.position;
                    found = true;
                    // Debug.Log($"[EntitySpawner] Spawned at 'PlayerStart' ({spawnPos})");
                }
            }

            if (found)
            {
                spawnPos.z = 0f;
                player.transform.position = spawnPos;
            }
            else
            {
                Debug.LogWarning("[EntitySpawner] No Spawn Point found (PlayerSpawnPoint or 'PlayerStart').");
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

        // Collect Exclude Positions from PlayerSpawnPoints
        List<Vector3Int> excludePositions = new List<Vector3Int>();
        var points = map.GetComponentsInChildren<PlayerSpawnPoint>();
        if (points != null)
        {
            foreach (var p in points)
            {
                // Convert World Pos to Cell Pos (assuming first floor map is reference)
                if (floors != null && floors.Count > 0)
                {
                    excludePositions.Add(floors[0].WorldToCell(p.transform.position));
                }
            }
        }

        List<Collider2D> excludeColliders = new List<Collider2D>();
        var tagged = map.GetComponentsInChildren<Collider2D>().Where(c => c.CompareTag("ExcludeSpawn"));
        excludeColliders.AddRange(tagged);

        spawner.Spawn(floors, obstacles, walls, excludePositions, excludeColliders.ToArray());
    }
}
