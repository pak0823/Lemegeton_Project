using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine.Networking;
using Project.Data;

public class GatherableDataImporter : EditorWindow
{
    // [Mod] 파일 대신 URL 입력 필드 사용
    private static string csvUrl = "https://docs.google.com/spreadsheets/d/e/2PACX-1vR-8wA50W8w_PsLGgoW9Vs-PbhKlEQeF0avqxG4AGfTkXklONFKXhd0_46gynEq3jgE2hMXNrJUcyRc/pub?gid=1395977100&single=true&output=csv";
    private static string targetPath = "Assets/03_Data/Interactions/Gatherasble";

    [MenuItem("Tools/Lemegeton/Data/Import Gatherables")]
    public static void ShowWindow()
    {
        GetWindow<GatherableDataImporter>("Gatherable Importer");
    }

    public static IEnumerator ImportRoutine()
    {
        string url = "https://docs.google.com/spreadsheets/d/e/2PACX-1vR-8wA50W8w_PsLGgoW9Vs-PbhKlEQeF0avqxG4AGfTkXklONFKXhd0_46gynEq3jgE2hMXNrJUcyRc/pub?gid=1395977100&single=true&output=csv";
        yield return ImportRoutine(url);
    }

    public static IEnumerator ImportRoutine(string url)
    {
        Debug.Log($"[GatherableImporter] Downloading CSV... {url}");
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                ImportData(www.downloadHandler.text);
            }
            else
            {
                Debug.LogError($"[GatherableImporter] Error: {www.error}");
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("CSV Importer (Google Sheets)", EditorStyles.boldLabel);

        // [Mod] URL 입력 필드 구성
        GUILayout.Label("CSV URL (Must be 'Published to Web')", EditorStyles.miniLabel);
        csvUrl = EditorGUILayout.TextField("CSV URL", csvUrl);
        
        targetPath = EditorGUILayout.TextField("Target Path", targetPath);

        if (GUILayout.Button("Download & Import"))
        {
            if (!string.IsNullOrEmpty(csvUrl))
            {
                Unity.EditorCoroutines.Editor.EditorCoroutineUtility.StartCoroutineOwnerless(ImportRoutine(csvUrl));
            }
            else
            {
                Debug.LogError("Please enter a valid CSV URL.");
            }
        }
    }
    
    // Legacy support for non-static context if needed, but we use static routine now.
    private void ImportFromUrl()
    {
         Unity.EditorCoroutines.Editor.EditorCoroutineUtility.StartCoroutineOwnerless(ImportRoutine(csvUrl));
    }

    private static void ImportData(string textData)
    {
        string[] lines = textData.Split('\n');
        
        if (!Directory.Exists(targetPath))
        {
            Directory.CreateDirectory(targetPath);
        }

        // 헤더 찾기 (ID 컬럼 기준)
        int startRow = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            var cells = SplitCsvLine(lines[i]);
            if (cells.Length > 0 && cells[0].Trim() == "ID")
            {
                // 헤더 다음다음 줄을 데이터 시작으로 가정 (3행: 설명, 4행: 데이터)
                // 시트에 따라 다를 수 있으므로 ID 값이 있는 줄부터 시작하도록 로직 수정 권장
                // 여기서는 안전하게 헤더 다음 줄부터 탐색하며 ID 있는 줄만 처리
                startRow = i + 1; 
                break;
            }
        }

        if (startRow == -1)
        {
            Debug.LogError("Could not find 'ID' header row.");
            return;
        }

        int successCount = 0;
        // startRow부터 끝까지 순회
        for (int i = startRow; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] row = SplitCsvLine(line);
            
            // [Check] ID 컬럼이 비어있으면 데이터가 없는 행으로 간주하고 스킵
            if (row.Length < 1 || string.IsNullOrEmpty(row[0].Trim())) continue;

            CreateAssetFromRow(row);
            successCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Import Complete! {successCount} items processed/updated.");
    }

    private static string[] SplitCsvLine(string line)
    {
        return line.Split(',');
    }

    private static void CreateAssetFromRow(string[] row)
    {
        // 0: ID
        // 1: Name 
        // 2: Name_KOR
        // 4: Observation_KOR
        // 7,8,9 : Interaction 1 (Text, Result, Prob)
        // 12,13,14 : Interaction 2
        // 17,18,19 : Interaction 3

        string id = row[0].Trim();
        string nameKor = row[2].Trim();
        string descKor = row[4].Trim();
        
        string fileName = $"Gatherable_{id}.asset";
        string fullPath = $"{targetPath}/{fileName}";

        // [Logic] 이미 존재하는 에셋이 있다면 LoadAssetAtPath로 불러옵니다.
        // 이를 통해 기존 참조(Reference)를 유지하면서 데이터만 갱신(Update)할 수 있습니다.
        GatherableDataSO data = AssetDatabase.LoadAssetAtPath<GatherableDataSO>(fullPath);
        
        if (data == null)
        {
            // 없으면 새로 생성
            data = ScriptableObject.CreateInstance<GatherableDataSO>();
            AssetDatabase.CreateAsset(data, fullPath);
        }

        // 데이터 덮어쓰기 (Update)
        data.objectName = nameKor;
        data.description = descKor;
        
        // 서브 에셋(OutcomeSO)들도 다시 생성해야 하므로 리스트 초기화
        // 주의: 기존 SubAsset들이 파일 내에 남아있을 수 있으므로 정리 필요
        data.outcomes.Clear(); 
        
        // 기존 파일에 붙어있는 하위 에셋들 제거 (Clean up)
        Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(fullPath);
        foreach (var sub in subAssets)
        {
            if (sub != data) // 메인 에셋이 아니면 삭제 (OutcomeSO들)
            {
                DestroyImmediate(sub, true);
            }
        }

        // 결과 1, 2, 3 파싱 및 추가
        ParseAndAddOutcome(data, row, 7, 8, 9);
        ParseAndAddOutcome(data, row, 12, 13, 14);
        ParseAndAddOutcome(data, row, 17, 18, 19);

        // 변경 사항 저장 표시
        EditorUtility.SetDirty(data);
    }

    private static void ParseAndAddOutcome(GatherableDataSO data, string[] row, int textIdx, int resultIdx, int probIdx)
    {
        if (row.Length <= probIdx) return;

        string text = row[textIdx].Trim();
        string resultDesc = row[resultIdx].Trim();
        string probStr = row[probIdx].Trim();

        if (string.IsNullOrEmpty(probStr)) return;
        if (!float.TryParse(probStr, out float prob)) return;
        if (prob <= 0) return; // 확률 0이면 추가 안함

        // 결과 생성
        InteractionOutcomeSO outcomeSO = null;
        
        if (resultDesc.Contains("감소")) // 함정
        {
            var trap = ScriptableObject.CreateInstance<TrapOutcomeSO>();
            trap.name = "TrapOutcome";
            trap.logMessage = "{0}의 {1}이(가) {2}만큼 감소했습니다."; 
            
            (string stat, int val) = ParseStatPenalty(resultDesc);
            trap.targetStat = stat;
            trap.reductionAmount = val;

            outcomeSO = trap;
        }
        else if (resultDesc.Contains("획득") || resultDesc.Contains("찾았습니다") || resultDesc.Contains("채굴")) // 보상
        {
            var reward = ScriptableObject.CreateInstance<RewardOutcomeSO>();
            reward.name = "RewardOutcome";
            outcomeSO = reward;
        }
        else // 꽝
        {
            var empty = ScriptableObject.CreateInstance<EmptyOutcomeSO>();
            empty.name = "EmptyOutcome";
            empty.message = text; 
            outcomeSO = empty;
        }

        if (outcomeSO != null)
        {
            // 서브 에셋으로 파일 안에 저장
            AssetDatabase.AddObjectToAsset(outcomeSO, data);
            
            var weighted = new GatherableDataSO.WeightedOutcome();
            weighted.outcome = outcomeSO;
            weighted.probability = prob;
            weighted.resultText = text;

            data.outcomes.Add(weighted);
        }
    }

    private static (string, int) ParseStatPenalty(string text)
    {
        int value = 1;
        string stat = "STR"; 

        var match = Regex.Match(text, @"\d+");
        if (match.Success) int.TryParse(match.Value, out value);

        if (text.Contains("생명")) stat = "HP";
        else if (text.Contains("근력")) stat = "STR";
        else if (text.Contains("민첩")) stat = "AGI";
        else if (text.Contains("총명")) stat = "CLV";
        else if (text.Contains("신체")) stat = "BDY";
        else if (text.Contains("정신")) stat = "MND";
        else if (text.Contains("통찰")) stat = "INS";

        return (stat, value);
    }
}
