using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Highlighter : MonoBehaviour
{
    public Tilemap overlayMap;       // Overlay 타일맵
    public TileBase highlightTile;   // 반투명 타일

    public void ShowCells(Tilemap baseMap, IEnumerable<Vector3Int> cells)
    {
        overlayMap.ClearAllTiles();
        foreach (var c in cells)
        {
            if (baseMap.HasTile(c)) overlayMap.SetTile(c, highlightTile);
        }
    }
    public void Clear() => overlayMap.ClearAllTiles();
}
