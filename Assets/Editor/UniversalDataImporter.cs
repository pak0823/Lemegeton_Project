using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System;
using Unity.EditorCoroutines.Editor;
using System.Reflection;
using System.Linq;

public class UniversalDataImporter : EditorWindow
{
    // -----------------------------------------------------------------------------------
    // [Configuration] 구글 시트 URL (CSV Export 링크)
    // -----------------------------------------------------------------------------------
    // 사용자 입력을 위해 EditorPrefs로 저장하거나 코드 상단에 상수로 배치
    // 여기서는 편의상 상수로 배치하되, 실제로는 EditorWindow UI에서 입력받는 것이 좋습니다.
    private const string SKILL_SHEET_URL = "https://docs.google.com/spreadsheets/d/e/2PACX-1vR-8wA50W8w_PsLGgoW9Vs-PbhKlEQeF0avqxG4AGfTkXklONFKXhd0_46gynEq3jgE2hMXNrJUcyRc/pubhtml?gid=258366528&single=true";
    private const string PASSIVE_SHEET_URL = "INSERT_PASSIVE_CSV_URL_HERE";
    private const string TRAIT_SHEET_URL = "INSERT_TRAIT_CSV_URL_HERE";

    // 저장 경로
    private const string SKILL_SAVE_PATH = "Assets/03_Data/Skills/Imported";
    private const string PASSIVE_SAVE_PATH = "Assets/03_Data/Passives/Imported";
    private const string TRAIT_SAVE_PATH = "Assets/03_Data/Traits/Imported";

    [MenuItem("Tools/Data/Sync Skills")]
    public static void SyncSkills() => StartDownload(SKILL_SHEET_URL, SKILL_SAVE_PATH, (csv) => ProcessSkills(csv));

    [MenuItem("Tools/Data/Sync Passives")]
    public static void SyncPassives() => StartDownload(PASSIVE_SHEET_URL, PASSIVE_SAVE_PATH, (csv) => ProcessPassives(csv));

    [MenuItem("Tools/Data/Sync Traits")]
    public static void SyncTraits() => StartDownload(TRAIT_SHEET_URL, TRAIT_SAVE_PATH, (csv) => ProcessTraits(csv));

    // -----------------------------------------------------------------------------------
    // [Core] 다운로드 로직
    // -----------------------------------------------------------------------------------
    private static void StartDownload(string url, string savePath, Action<string> onComplete)
    {
        if (string.IsNullOrEmpty(url) || url.Contains("INSERT"))
        {
            Debug.LogError("[Importer] URL이 설정되지 않았습니다. 스크립트 상단의 URL 상수를 수정해주세요.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(savePath))
        {
            VerifyDirectory(savePath);
        }

        EditorCoroutineUtility.StartCoroutineOwnerless(DownloadRoutine(url, onComplete));
    }

    private static IEnumerator DownloadRoutine(string url, Action<string> onComplete)
    {
        Debug.Log($"[Importer] Downloading CSV... {url}");
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                onComplete?.Invoke(www.downloadHandler.text);
            }
            else
            {
                Debug.LogError($"[Importer] Failed: {www.error}");
            }
        }
    }

    private static void VerifyDirectory(string path)
    {
        string[] folders = path.Split('/');
        string current = folders[0];
        for (int i = 1; i < folders.Length; i++)
        {
            string next = current + "/" + folders[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, folders[i]);
            }
            current = next;
        }
    }

    // -----------------------------------------------------------------------------------
    // [Core] 데이터 처리 로직 (제네릭은 아님, 타입별 컬럼 매핑이 다르므로)
    // -----------------------------------------------------------------------------------

    // 공통: 모든 에셋을 로드해서 ID 사전 구축
    private static Dictionary<string, T> LoadAssetMap<T>() where T : ScriptableObject
    {
        var dict = new Dictionary<string, T>();
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                // Reflection으로 'id' 필드 접근 (공통 인터페이스가 없으므로)
                FieldInfo idField = typeof(T).GetField("id");
                if (idField != null)
                {
                    string idVal = idField.GetValue(asset) as string;
                    
                    // ID가 비어있으면 파일명으로 추론 시도 (Migration)
                    if (string.IsNullOrEmpty(idVal))
                    {
                        // 예: "Skill_1001" -> "1001"
                        // 간단하게 파일명 전체를 임시 ID로 간주하거나, 별도 로직 적용
                        // 여기서는 id가 비어있으면 사전에 넣지 않음 (새로 생성되거나, 직접 매칭 로직에서 처리)
                    }
                    else
                    {
                        if (!dict.ContainsKey(idVal)) dict.Add(idVal, asset);
                    }
                }
            }
        }
        return dict;
    }

    // [Parser Helper] CSV 파싱 (단순 쉼표 분리, 따옴표 처리 없음 주의)
    private static List<string[]> ParseCSV(string csv)
    {
        var list = new List<string[]>();
        string[] rows = csv.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        // 0~2행은 헤더 등 잡다한 정보라 가정하고 3행부터 데이터라 가정 (ItemDataImporter 참조)
        for (int i = 3; i < rows.Length; i++)
        {
            list.Add(rows[i].Split(','));
        }
        return list;
    }

    // -----------------------------------------------------------------------------------
    // [Impl] Skill Processing
    // -----------------------------------------------------------------------------------
    private static void ProcessSkills(string csv)
    {
        var map = LoadAssetMap<SkillAsset>();
        var rows = ParseCSV(csv);
        int updated = 0, created = 0;

        foreach (var cols in rows)
        {
            if (cols.Length < 3) continue;
            string id = cols[0].Trim(); // A열: ID
            if (string.IsNullOrEmpty(id)) continue;

            SkillAsset asset = null;
            if (map.ContainsKey(id))
            {
                asset = map[id];
            }
            else
            {
                // 없으면 새로 생성 (기본 타입은 일단 SkillAsset을 상속받은 구체 클래스가 필요함)
                // [주의] SkillAsset은 abstract일 수 있으므로, 어떤 파생 클래스를 생성할지 결정해야 함.
                // CSV에 'Type' 컬럼이 있다고 가정하거나, 기본형(예: ParametricDamageSkill)을 사용.
                // 여기서는 가장 일반적인 'ParametricDamageSkill'을 생성한다고 가정.
                
                // 만약 Type 컬럼이 있다면: Type t = Type.GetType(cols[X]); asset = ScriptableObject.CreateInstance(t) as SkillAsset;
                // 일단은 데모용으로 ScriptableObject 생성이 불가능할 수 있으니(Abstract), 확인 필요.
                // SkillAsset은 Abstract class이므로 인스턴스화 불가 -> ParametricDamageSkill 사용
                asset = ScriptableObject.CreateInstance("ParametricDamageSkill") as SkillAsset; 
                
                if (asset == null)
                {
                    Debug.LogError($"[Importer] Failed to create instance for ID {id}. Check Class Name.");
                    continue;
                }

                asset.id = id;
                string path = $"{SKILL_SAVE_PATH}/Skill_{id}.asset";
                AssetDatabase.CreateAsset(asset, path);
                created++;
            }

            // 매핑 (컬럼 인덱스는 시트 구조에 따라 수정 필요)
            // A: ID, B: Name, C: Description, D: Cost, E: Cooldown ...
            if (cols.Length > 1) asset.displayName = cols[1].Trim();
            if (cols.Length > 2) asset.description = cols[2].Trim();
            
            // 추가 필드 매핑
            // Col 3: Power (float)
            if (cols.Length > 3 && float.TryParse(cols[3], out float p))
            {
                asset.power = p;
            }

            // Col 4: Cost (int)
            if (cols.Length > 4 && int.TryParse(cols[4], out int c))
            {
                asset.cost = c;
            }
            
            EditorUtility.SetDirty(asset);
            updated++;
        }
        
        AssetDatabase.SaveAssets();
        Debug.Log($"[Importer] Skills Sync Complete. Created: {created}, Updated: {updated}");
    }

    // -----------------------------------------------------------------------------------
    // [Impl] Passive Processing
    // -----------------------------------------------------------------------------------
    private static void ProcessPassives(string csv)
    {
        var map = LoadAssetMap<PassiveAsset>();
        var rows = ParseCSV(csv);
        int updated = 0;

        foreach (var cols in rows)
        {
            if (cols.Length < 3) continue;
            string id = cols[0].Trim();
            if (string.IsNullOrEmpty(id)) continue;

            PassiveAsset asset = null;
            if (map.ContainsKey(id))
            {
                asset = map[id];
            }
            else
            {
                // PassiveAsset 역시 Abstract일 가능성이 높음. 구체 클래스 필요.
                // 임시로 'BasePassive' 같은게 있다면 사용, 없다면 스킵하거나 특정 타입 지정
                // 여기서는 예시로 'GigantStrengthToBodyPassive' 같은걸 하드코딩 할 수 없으니
                // Type 컬럼(예: B열)을 읽어서 리플렉션으로 생성하는게 좋음.
                
                /*
                string typeName = cols[1].Trim(); 
                asset = ScriptableObject.CreateInstance(typeName) as PassiveAsset;
                */
                
                // 일단 생성이 어렵다면 경고 출력
                // Debug.LogWarning($"[Importer] Passive {id} 신규 생성은 구체 클래스 타입 정보가 필요하여 스킵합니다.");
                // continue;

                // [가정] 만약 단순히 데이터 컨테이너라면 BasicPassive 같은게 있어야 함.
                continue; 
            }

            // 매핑
            asset.id = id;
            if (cols.Length > 2) asset.displayName = cols[2].Trim();
            if (cols.Length > 3) asset.description = cols[3].Trim();

            EditorUtility.SetDirty(asset);
            updated++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Importer] Passives Sync Complete. Updated: {updated}");
    }

    // -----------------------------------------------------------------------------------
    // [Impl] Trait Processing
    // -----------------------------------------------------------------------------------
    private static void ProcessTraits(string csv)
    {
        var map = LoadAssetMap<TraitAsset>();
        var rows = ParseCSV(csv);
        int updated = 0, created = 0;

        foreach (var cols in rows)
        {
            if (cols.Length < 3) continue;
            string id = cols[0].Trim();
            if (string.IsNullOrEmpty(id)) continue;

            TraitAsset asset = null;
            if (map.ContainsKey(id))
            {
                asset = map[id];
            }
            else
            {
                asset = ScriptableObject.CreateInstance<TraitAsset>();
                asset.id = id;
                AssetDatabase.CreateAsset(asset, $"{TRAIT_SAVE_PATH}/Trait_{id}.asset");
                created++;
            }

            // 매핑
            if (cols.Length > 1) asset.displayName = cols[1].Trim();
            if (cols.Length > 2) asset.description = cols[2].Trim();

            EditorUtility.SetDirty(asset);
            updated++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Importer] Traits Sync Complete. Created: {created}, Updated: {updated}");
    }
}
