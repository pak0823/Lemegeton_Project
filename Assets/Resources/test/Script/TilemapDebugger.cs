using UnityEngine;
using UnityEngine.Tilemaps;

// 타일맵 오브젝트에 붙이면 씬에서 타일 셀, 실제 타일 존재 셀 모두 표시됨
[ExecuteAlways]
public class TilemapDebugger : MonoBehaviour
{
    public Color hexFillColor = new Color(0f, 1f, 0.4f, 0.13f);    // 실제 타일 존재 셀 채움색
    public Color hexOutlineColor = new Color(1f, 1f, 0f, 0.18f);    // 전체 셀 외곽선

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

        // 실제 타일 존재 셀(채움)
        foreach (var pos in tilemap.cellBounds.allPositionsWithin)
        {
            if (!tilemap.HasTile(pos)) continue;
            Vector3 world = tilemap.GetCellCenterWorld(pos);
            DrawHexFilled(world, tilemap.cellSize, hexFillColor);
        }
    }

    // Pointed-Top 육각형 외곽선 그리기
    void DrawHexOutline(Vector3 center, Vector3 size, Color color)
    {
        float w = size.x * 0.5f;
        float h = size.y * 0.5f;
        float r = Mathf.Min(w, h) * 0.98f; // 셀 내외곽에 가깝게

        Vector3[] pts = new Vector3[7];
        for (int i = 0; i < 7; ++i)
        {
            float ang = Mathf.Deg2Rad * (90f + 60f * i); // Pointed-Top: 꼭짓점이 위로
            pts[i] = center + new Vector3(r * Mathf.Cos(ang), r * Mathf.Sin(ang), 0f);
        }
        Gizmos.color = color;
        for (int i = 0; i < 6; ++i)
            Gizmos.DrawLine(pts[i], pts[i + 1]);
    }

    // Pointed-Top 육각형 채우기 (삼각형 6개로 채움)
    void DrawHexFilled(Vector3 center, Vector3 size, Color color)
    {
        float w = size.x * 0.5f;
        float h = size.y * 0.5f;
        float r = Mathf.Min(w, h) * 0.95f;
        Vector3[] pts = new Vector3[6];
        for (int i = 0; i < 6; ++i)
        {
            float ang = Mathf.Deg2Rad * (90f + 60f * i); // 꼭짓점이 위로
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
