using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Highlighter : MonoBehaviour
{
    public Tilemap overlayMap;       // Overlay 타일맵
    public TileBase highlightTile;   // 반투명 타일

    [Tooltip("켜면 baseMap 타일이 있는 칸에만 표시(클리핑). 끄면 맵 밖도 표시")]
    public bool clipToBaseMap = false;

    public void ShowCells(Tilemap baseMap, IEnumerable<Vector3Int> cells)
    {
        overlayMap.ClearAllTiles();
        foreach (var c in cells)
        {
            if (clipToBaseMap)
            {
                if (baseMap != null && baseMap.HasTile(c))
                    overlayMap.SetTile(c, highlightTile);
            }
            else
            {
                overlayMap.SetTile(c, highlightTile);
            }
        }
    }
    public void Clear() => overlayMap.ClearAllTiles();
}
