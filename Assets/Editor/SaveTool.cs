// 에디터 전용 (Scripts/Editor 폴더에 저장)
using UnityEditor;
using UnityEngine;

public class SaveTool
{
    [MenuItem("Tools/Clear Save Data")]
    public static void ClearSave()
    {
        PlayerPrefs.DeleteAll();
        Debug.Log("세이브 데이터 삭제 완료!");
    }
}