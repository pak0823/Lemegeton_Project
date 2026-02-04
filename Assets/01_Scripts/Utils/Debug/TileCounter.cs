using UnityEngine;
using UnityEngine.Tilemaps;

public class TileCounter : MonoBehaviour
{
    public Tilemap tilemap;         // 검사할 타일맵
    public string partialName = "Floor";  // 검색할 타일의 이름에 포함될 이름

    void Start()
    {
        int count = CountTilesByPartialName(tilemap, partialName);
        Debug.Log($"이름에 '{partialName}'이(가) 포함된 타일 개수: {count}");
    }

    int CountTilesByPartialName(Tilemap map, string namePart)
    {
        int count = 0;
        map.CompressBounds();  // 타일이 존재하는 셀 영역만 스캔

        foreach (Vector3Int pos in map.cellBounds.allPositionsWithin)
        {
            TileBase tile = map.GetTile(pos);
            if (tile != null && tile.name != null &&
                tile.name.ToLower().Contains(namePart.ToLower()))
            {
                count++;
            }
        }

        return count;
    }
}
