using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Highlighter : MonoBehaviour
{
    public Tilemap overlayMap;       // Overlay 타일맵
    public TileBase highlightTile;   // 반투명 타일

    [Tooltip("켜면 baseMap 타일이 있는 칸에만 표시(클리핑). 끄면 맵 밖도 표시")]
    public bool clipToBaseMap = false;

    const int TRANSIENT_TOKEN = 0;

    // 그룹 보관: token → (baseMap, cells)
    class Group { public Tilemap baseMap; public HashSet<Vector3Int> cells = new(); }
    readonly Dictionary<int, Group> _groups = new();
    int _nextToken = 1;

    // 임시 프리뷰 유지
    public void ShowCells(Tilemap baseMap, IEnumerable<Vector3Int> cells)
    {
        SetGroupCells(TRANSIENT_TOKEN, baseMap, cells);
    }
    // 임시 프리뷰만 제거
    public void ClearTransient()
    {
        if (_groups.Remove(TRANSIENT_TOKEN))
            RedrawAll();
    }
    // === 완전 삭제(모든 그룹) ===
    public void ClearAll()
    {
        _groups.Clear();
        overlayMap?.ClearAllTiles();
    }
    // === 새 그룹 토큰 발급 ===
    public int CreateGroup() => _nextToken++;

    // === 그룹 타일 세팅/갱신 ===
    public void SetGroupCells(int token, Tilemap baseMap, IEnumerable<Vector3Int> cells)
    {
        if (overlayMap == null || baseMap == null || cells == null) return;

        if (!_groups.TryGetValue(token, out var g))
            g = _groups[token] = new Group();

        g.baseMap = baseMap;
        g.cells.Clear();
        foreach (var c in cells) g.cells.Add(c);

        RedrawAll();
    }
    // === 특정 그룹만 제거 ===
    public void ClearGroup(int token)
    {
        if (_groups.Remove(token))
            RedrawAll();
    }

    void RedrawAll()
    {
        if (overlayMap == null) return;
        overlayMap.ClearAllTiles();

        foreach (var kv in _groups)
        {
            var g = kv.Value;
            if (g.baseMap == null || g.cells == null) continue;

            foreach (var cell in g.cells)
            {
                if (clipToBaseMap && !g.baseMap.HasTile(cell)) continue;

                var world = g.baseMap.GetCellCenterWorld(cell);
                var ocell = overlayMap.WorldToCell(world);
                overlayMap.SetTile(ocell, highlightTile);
            }
        }
    }

}
