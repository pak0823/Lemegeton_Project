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
        // °èÃþ¿¡¼­ Tilemap ÀÚµ¿ Å½»ö
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
            EditorGUILayout.LabelField("¼¿ ÁÂÇ¥", cell.ToString());
            EditorGUILayout.LabelField("¼¿ Áß½É(¿ùµå)", cellCenter.ToString("F3"));
            EditorGUILayout.LabelField("¿ÀºêÁ§Æ® À§Ä¡", t.position.ToString("F3"));
        }
    }
}
