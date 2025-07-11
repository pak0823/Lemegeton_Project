using UnityEngine;
using UnityEngine.Tilemaps;

// 플레이어 오브젝트에 붙이면 현재 밟고 있는 셀을 표시
public class PlayerCellDebugger : MonoBehaviour
{
    public Tilemap tilemap; // 검사할 타일맵(바닥)
    public Color playerCellColor = new Color(1f, 0.3f, 0.3f, 0.35f); // 빨간색

    void OnDrawGizmos()
    {
        if (tilemap == null) return;
        Vector3Int cell = tilemap.WorldToCell(transform.position);
        Vector3 world = tilemap.CellToWorld(cell) + tilemap.cellSize * 0.5f;
        Gizmos.color = playerCellColor;
        Gizmos.DrawCube(world, tilemap.cellSize * 0.9f);
    }
}
