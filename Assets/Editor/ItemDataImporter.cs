using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System;
using Unity.EditorCoroutines.Editor;

public class ItemDataImporter
{
    // [주의] ItemData 전용 시트의 CSV 웹 게시 URL을 넣을 것
    private static string sheetUrl = "https://docs.google.com/spreadsheets/d/e/2PACX-1vR-8wA50W8w_PsLGgoW9Vs-PbhKlEQeF0avqxG4AGfTkXklONFKXhd0_46gynEq3jgE2hMXNrJUcyRc/pub?gid=113024103&single=true&output=csv";
    private static string savePath = "Assets/03_Data/Item/Item_Mat"; // 아이템 에셋 저장 경로

    [MenuItem("Tools/Sync Item Data (ID Based)")]
    public static void ImportData() => EditorCoroutineUtility.StartCoroutineOwnerless(DownloadCSV());

    private static IEnumerator DownloadCSV()
    {
        using (UnityWebRequest www = UnityWebRequest.Get(sheetUrl))
        {
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success) ProcessCSV(www.downloadHandler.text);
        }
    }

    private static void ProcessCSV(string csv)
    {
        string[] rows = csv.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        // 기존 에셋 로드 (ID 기반 매칭용)
        string[] guids = AssetDatabase.FindAssets("t:ItemData");
        Dictionary<string, ItemData> itemDict = new Dictionary<string, ItemData>();
        foreach (var guid in guids)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) itemDict[asset.itemID] = asset;
        }

        // 데이터 파싱 (헤더 건너뛰고 n행부터 시작)
        for (int i = 3; i < rows.Length; i++)
        {
            string[] cols = rows[i].Split(',');
            if (cols.Length < 7) continue;

            string id = cols[0].Trim(); // A열: itemID
            if (string.IsNullOrEmpty(id)) continue;

            ItemData so;
            if (!itemDict.TryGetValue(id, out so))
            {
                // 없으면 자동 생성
                so = ScriptableObject.CreateInstance<ItemData>();
                so.itemID = id;
                if (!AssetDatabase.IsValidFolder(savePath)) System.IO.Directory.CreateDirectory(savePath);
                AssetDatabase.CreateAsset(so, $"{savePath}/Item_{id}.asset");
                Debug.Log($"<color=cyan>신규 아이템 생성: {id}</color>");
            }

            // 데이터 갱신 (열 번호는 본인 시트 순서에 맞게 조정)
            so.itemName = cols[1].Trim();         // B열: 이름
            if (Enum.TryParse(cols[2].Trim(), true, out ItemType type)) so.itemType = type; // C열: 타입
            int.TryParse(cols[3], out so.maxStack); // D열: 최대중첩
            so.itemDescription = cols[4].Trim();   // E열: 설명
            so.atlasAddress = cols[5].Trim();      // F열: Atlas 주소
            so.spriteName = cols[6].Trim();        // G열: Sprite 이름

            EditorUtility.SetDirty(so);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("<color=green>아이템 데이터 동기화 완료!</color>");
    }
}