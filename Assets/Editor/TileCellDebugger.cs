using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;

[CustomEditor(typeof(Transform))]
public class TileCellDebugger : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        Transform t = (Transform)target;
        // 계층에서 Tilemap 자동 탐색
        Tilemap tilemap = null;
        Transform cur = t.parent;
        while (cur != null && tilemap == null)
        {
            tilemap = cur.GetComponentInChildren<Tilemap>();
            cur = cur.parent;
        }
        if (tilemap != null)
        {
            Vector3Int cell = tilemap.WorldToCell(t.position);
            Vector3 cellCenter = tilemap.GetCellCenterWorld(cell);
            EditorGUILayout.LabelField("셀 좌표", cell.ToString());
            EditorGUILayout.LabelField("셀 중심(월드)", cellCenter.ToString("F3"));
            EditorGUILayout.LabelField("오브젝트 위치", t.position.ToString("F3"));
        }
    }
}
