using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;

public class EntranceSnapper : EditorWindow
{
    [MenuItem("Tools/Entrance �ڵ� �� ������")]
    public static void ShowWindow()
    {
        GetWindow(typeof(EntranceSnapper));
    }

    void OnGUI()
    {
        if (GUILayout.Button("������ Entrance ������Ʈ, �� �߽����� ����"))
        {
            SnapEntrances();
        }
    }

    void SnapEntrances()
    {
        foreach (var obj in Selection.gameObjects)
        {
            // �θ�-���� �������� Tilemap �ڵ� Ž��
            var tilemap = FindTilemapInParents(obj.transform);
            if (tilemap == null)
            {
                Debug.LogWarning($"{obj.name}�� �θ� ������ Tilemap�� �����ϴ�.");
                continue;
            }

            // localPosition �� ������ǥ �� ����ǥ �� �� �߽� ������ǥ �� �������� localPosition ��ȯ
            Vector3 worldPos = obj.transform.position;
            Vector3Int cellPos = tilemap.WorldToCell(worldPos);
            Vector3 cellCenter = tilemap.GetCellCenterWorld(cellPos);
            Vector3 parentLocal = obj.transform.parent.InverseTransformPoint(cellCenter);

            Undo.RecordObject(obj.transform, "Entrance ����");
            obj.transform.localPosition = parentLocal;
        }
        Debug.Log("Entrance �ڵ� ���� �Ϸ�!");
    }

    // ���� �������� Tilemap �ڵ� Ž�� �Լ�
    Tilemap FindTilemapInParents(Transform t)
    {
        while (t != null)
        {
            var tilemap = t.GetComponent<Tilemap>();
            if (tilemap != null) return tilemap;
            t = t.parent;
        }
        return null;
    }
}
