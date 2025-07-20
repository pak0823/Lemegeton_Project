using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MapObjectSpawner : MonoBehaviour
{
    public List<GameObject> trapPrefabs;
    public int trapCount = 3;

    public List<GameObject> chestPrefabs;
    public int chestCount = 2;

    private Tilemap tilemap;

    private void Awake()
    {
        // 자식에 붙은 타일맵을 자동으로 찾아서 저장
        tilemap = GetComponentInChildren<Tilemap>();
        if (tilemap == null)
            Debug.LogError("MapObjectSpawner: 자식에서 Tilemap을 찾을 수 없습니다.");
    }

    // 맵 생성 직후 MapManager가 호출
    public void Spawn(Tilemap tilemap)
    {
        var bounds = tilemap.cellBounds;
        var floorCells = new List<Vector3Int>();
        foreach (var pos in bounds.allPositionsWithin)
            if (tilemap.GetTile(pos)?.name.Contains("Floor") == true)
                floorCells.Add(pos);

        Debug.Log($"[Spawner] floorCells={floorCells.Count}");

        // 함정 배치
        for (int i = 0; i < trapCount && floorCells.Count > 0; i++)
        {
            int idx = Random.Range(0, floorCells.Count);
            Vector3 worldPos = tilemap.GetCellCenterWorld(floorCells[idx]);
            Instantiate(trapPrefabs[Random.Range(0, trapPrefabs.Count)], worldPos, Quaternion.identity, transform);
            floorCells.RemoveAt(idx);
        }

        // 상자 배치
        for (int i = 0; i < chestCount && floorCells.Count > 0; i++)
        {
            int idx = Random.Range(0, floorCells.Count);
            Vector3 worldPos = tilemap.GetCellCenterWorld(floorCells[idx]);
            Instantiate(chestPrefabs[Random.Range(0, chestPrefabs.Count)], worldPos, Quaternion.identity, transform);
            floorCells.RemoveAt(idx);
        }
    }
}
