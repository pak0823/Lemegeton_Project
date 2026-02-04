using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
public class TilemapDebugger : MonoBehaviour
{
    public Color hexFillColor = new Color(0f, 1f, 0.4f, 0.13f);    // 바닥 타일 채움색
    public Color hexOutlineColor = new Color(1f, 1f, 0f, 0.18f);   // 전체 셀 외곽선
    public Color wallFillColor = new Color(1f, 0f, 0f, 0.25f);     // 벽 타일(빨간색)

    public Tilemap wallTilemap; // Inspector에서 "벽" 타일맵(예: Layer10 등) drag & drop

    private Tilemap tilemap;

    void OnDrawGizmos()
    {
        if (tilemap == null)
            tilemap = GetComponent<Tilemap>();
        if (tilemap == null) return;

        // 전체 셀(Outline)
        foreach (var pos in tilemap.cellBounds.allPositionsWithin)
        {
            Vector3 world = tilemap.GetCellCenterWorld(pos);
            DrawHexOutline(world, tilemap.cellSize, hexOutlineColor);
        }

        // 바닥 타일 셀(채움)
        foreach (var pos in tilemap.cellBounds.allPositionsWithin)
        {
            if (!tilemap.HasTile(pos)) continue;

            // 벽 타일맵이 있고, 이 셀에 벽이 있으면 빨간색
            if (wallTilemap != null && wallTilemap.HasTile(pos))
                DrawHexFilled(tilemap.GetCellCenterWorld(pos), tilemap.cellSize, wallFillColor);
            else
                DrawHexFilled(tilemap.GetCellCenterWorld(pos), tilemap.cellSize, hexFillColor);
        }
    }

    void DrawHexOutline(Vector3 center, Vector3 size, Color color)
    {
        float w = size.x * 0.5f;
        float h = size.y * 0.5f;
        float r = Mathf.Min(w, h) * 0.98f;
        Vector3[] pts = new Vector3[7];
        for (int i = 0; i < 7; ++i)
        {
            float ang = Mathf.Deg2Rad * (90f + 60f * i);
            pts[i] = center + new Vector3(r * Mathf.Cos(ang), r * Mathf.Sin(ang), 0f);
        }
        Gizmos.color = color;
        for (int i = 0; i < 6; ++i)
            Gizmos.DrawLine(pts[i], pts[i + 1]);
    }

    void DrawHexFilled(Vector3 center, Vector3 size, Color color)
    {
        float w = size.x * 0.5f;
        float h = size.y * 0.5f;
        float r = Mathf.Min(w, h) * 0.95f;
        Vector3[] pts = new Vector3[6];
        for (int i = 0; i < 6; ++i)
        {
            float ang = Mathf.Deg2Rad * (90f + 60f * i);
            pts[i] = center + new Vector3(r * Mathf.Cos(ang), r * Mathf.Sin(ang), 0f);
        }
        Gizmos.color = color;
        for (int i = 0; i < 6; ++i)
        {
            int j = (i + 1) % 6;
            Gizmos.DrawLine(center, pts[i]);
            Gizmos.DrawLine(pts[i], pts[j]);
        }
    }
}
