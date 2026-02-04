using System;
using System.Collections;
using System.Collections.Generic;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public class GoogleSheetImporter
{
    // 복사한 구글 시트 CSV URL
    private static string sheetUrl = "https://docs.google.com/spreadsheets/d/e/2PACX-1vR-8wA50W8w_PsLGgoW9Vs-PbhKlEQeF0avqxG4AGfTkXklONFKXhd0_46gynEq3jgE2hMXNrJUcyRc/pub?gid=914486176&single=true&output=csv";
    private static string savePath = "Assets/03_Data/Unit/Player"; // 에셋이 생성될 경로

    [MenuItem("Tools/Sync Unit Data (ID Based)")]

    public static void ImportData() => EditorCoroutineUtility.StartCoroutineOwnerless(DownloadCSV());

    private static IEnumerator DownloadCSV()
    {
        Debug.Log("구글 스프레드시트에서 데이터를 가져오는 중...");

        using (UnityWebRequest www = UnityWebRequest.Get(sheetUrl))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                // 정적 메서드이므로 인스턴스 없이 호출
                ProcessCSV(www.downloadHandler.text);
                Debug.Log("<color=green>모든 유닛 데이터 동기화 완료!</color>");
            }
            else
            {
                Debug.LogError($"데이터 로드 실패: {www.error}");
            }
        }
    }

    private static void ProcessCSV(string csv)
    {
        // 줄바꿈 기호 대응 분리
        string[] rows = csv.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        // 기존 모든 UnitData 로드 (ID 매칭용)
        string[] guids = AssetDatabase.FindAssets("t:UnitData");
        Dictionary<int, UnitData> unitDict = new Dictionary<int, UnitData>();
        foreach (var guid in guids)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null && !unitDict.ContainsKey(asset.unitID)) unitDict.Add(asset.unitID, asset);
        }

        // 시트 데이터 순회 (6행부터)
        for (int i = 5; i < rows.Length; i++)
        {
            string[] cols = rows[i].Split(',');
            if (cols.Length < 8 || !int.TryParse(cols[0], out int id)) continue; // Num 파싱 실패 시 스킵

            string alias = cols[1].Trim();
            if (string.IsNullOrEmpty(alias)) continue;

            UnitData so;
            // ID가 없으면 신규 생성, 있으면 기존 파일 사용
            if (!unitDict.TryGetValue(id, out so))
            {
                so = ScriptableObject.CreateInstance<UnitData>();
                so.unitID = id;
                // 폴더 체크 및 생성
                if (!AssetDatabase.IsValidFolder(savePath)) System.IO.Directory.CreateDirectory(savePath);
                AssetDatabase.CreateAsset(so, $"{savePath}/{alias}_{id}.asset");
                Debug.Log($"<color=cyan>신규 유닛 생성: {alias}({id})</color>");
            }

            // 데이터 갱신
            so.DisplayName = alias;
            int.TryParse(cols[3], out so.baseSTR);
            int.TryParse(cols[4], out so.baseCLV);
            int.TryParse(cols[5], out so.baseAGI);
            int.TryParse(cols[6], out so.baseBDY);
            int.TryParse(cols[7], out so.baseMND);
            int.TryParse(cols[8], out so.baseINS);

            EditorUtility.SetDirty(so);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("<color=green>ID 기반 동기화 완료!</color>");
    }
}