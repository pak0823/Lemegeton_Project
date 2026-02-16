using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Linq;
using Lemegeton.Editor;
using Unity.EditorCoroutines.Editor;

public class SkillDataImporter : EditorWindow
{
    // GID: 258366528
    private const string SHEET_URL = "https://docs.google.com/spreadsheets/d/e/2PACX-1vR-8wA50W8w_PsLGgoW9Vs-PbhKlEQeF0avqxG4AGfTkXklONFKXhd0_46gynEq3jgE2hMXNrJUcyRc/pub?gid=1909887612&single=true&output=csv";
    private const string SAVE_PATH = "Assets/03_Data/Skills/Imported";

    [MenuItem("Tools/Lemegeton/Data/Import Skills")]
    public static void SyncSkills()
    {
        EditorCoroutineUtility.StartCoroutineOwnerless(ImportRoutine());
    }

    public static IEnumerator ImportRoutine()
    {
        Debug.Log($"[SkillImporter] Downloading CSV... {SHEET_URL}");
        using (UnityWebRequest www = UnityWebRequest.Get(SHEET_URL))
        {
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                ProcessCSV(www.downloadHandler.text);
            }
            else
            {
                Debug.LogError($"[SkillImporter] Failed: {www.error}");
            }
        }
    }



    public static void ProcessCSV(string csvData)
    {
        var rows = ParseCSV(csvData);
        var groups = GroupRows(rows);
        int updated = 0;
        int created = 0;

        VerifyDirectory(SAVE_PATH);

        foreach (var group in groups)
        {
            if (ProcessGroup(group)) updated++;
            else created++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[SkillImporter] Complete. Processed {groups.Count} skills.");
    }

    private static bool ProcessGroup(SkillGroup group)
    {
        // 1. 에셋 타입 결정
        System.Type targetType = DetermineClassType(group.ActiveSettings);
        if (targetType == null)
        {
            Debug.LogError($"[SkillImporter] Cannot determine class type for {group.ID} ({group.Name})");
            return false;
        }

        // 2. 에셋 로드 또는 생성
        string assetPath = $"{SAVE_PATH}/Skill_{group.ID}.asset";
        SkillAsset asset = AssetDatabase.LoadAssetAtPath<SkillAsset>(assetPath);
        bool isNew = false;
        
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance(targetType) as SkillAsset;
            AssetDatabase.CreateAsset(asset, assetPath);
            isNew = true;
        }
        else if (asset.GetType() != targetType)
        {
            Debug.LogWarning($"[SkillImporter] Recreating {group.ID} due to type change: {asset.GetType().Name} -> {targetType.Name}");
            AssetDatabase.DeleteAsset(assetPath);
            asset = ScriptableObject.CreateInstance(targetType) as SkillAsset;
            AssetDatabase.CreateAsset(asset, assetPath);
            isNew = true;
        }

        // 3. 필드 주입
        asset.id = group.ID.ToString();
        asset.displayName = group.Name;
        asset.description = group.Description;

        // 액티브 필드
        InjectActiveFields(asset, group.ActiveSettings);

        // 훈련 필드
        // 이전 훈련 루트 초기화? 현재는 덮어쓰기 가정.
        // 참고: SkillAsset.trainingRoutes가 배열인지 리스트인지 확인 필요. ParametricDamageSkill 확인.
        // ParametricDamageSkill은 SkillAsset을 상속받으므로 `trainingRoutes` 필드 확인 필요.
        // SkillAsset 정의가 완전히 보이지 않지만 보통 `TrainingParams[] trainingRoutes`를 가짐.
        // 표준 SkillAsset이 이를 가지고 있거나 특정 필드에 주입한다고 가정.
        // ParametricDamageSkill에는 `trainingApplyBleed` 같은 필드가 있음.
        
        // 실제로 trainingRoutes는 보통 제목/설명을 저장함. 
        // UI 툴팁을 위해 `trainingRoutes` 배열을 채워야 할 수 있음, 
        // 그리고 로직을 위한 불리언 플래그 설정.
        
        List<TrainingRouteInfo> routes = new List<TrainingRouteInfo>();
        
        // 각 훈련 행 처리 (최대 3개 루트: 0, 1, 2)
        // Group.Trainings는 List<SkillCSVRow>임
        
        // 기존 플래그 초기화 (선택 사항이지만 깔끔함을 위해 좋음)
        // 하지만 현재 데이터를 기반으로 리플렉션을 통해 설정하므로,
        // 주의하지 않으면 오래된 true 값이 남을 수 있음. 
        // 이상적: 모든 "training..." 불리언을 false로 초기화? 모든 필드를 알지 못하면 너무 위험함.
        
        for (int i = 0; i < group.Trainings.Count; i++)
        {
            var tRow = group.Trainings[i];
            // 툴팁용 메타데이터 추가
            routes.Add(new TrainingRouteInfo 
            { 
                title = tRow.Name.Replace(" 훈련", ""), 
                description = tRow.Description,
                overrideSkillDescription = tRow.OverrideDescription 
            });

            // 로직 필드 주입 (예: trainingApplyBleed = true)
            InjectTrainingLogic(asset, tRow, i);
        }

        // 리플렉션으로 `trainingRoutes` 설정 (기반 클래스에 존재하는 경우)
        FieldInfo routesField = typeof(SkillAsset).GetField("trainingRoutes");
        if (routesField != null)
        {
            routesField.SetValue(asset, routes.ToArray());
        }

        EditorUtility.SetDirty(asset);
        return !isNew;
    }

    private static System.Type DetermineClassType(SkillCSVRow row)
    {
        // 특수 케이스
        if (row.FormulaType == "Field_Create" && row.ValueStr == "BeastField") return typeof(SelfBeastDomainSkill);
        if (row.FormulaType == "Field_Create" && row.ValueStr == "Smoke") return typeof(SmokeBombSkill);
        if (row.FormulaType == "Status_Buff" && row.ValueStr == "Stealth") return typeof(SelfAmbushSkill);
        if (row.FormulaType == "Status_Buff" && row.ValueStr == "Guard") return typeof(SelfVigilanceSkill);
        if (row.FormulaType == "Status_Buff" && row.ValueStr == "Isolation") return typeof(SelfIsolationTimedSkill);
        
        // 일반 케이스
        SkillFormulaType ft = ParseFormulaType(row.FormulaType);
        switch (ft)
        {
            case SkillFormulaType.Heal:
            case SkillFormulaType.Heal_Turn:
                return typeof(ParametricHealSkill);
            
            case SkillFormulaType.Buff_Stat:
            case SkillFormulaType.Debuff_Stat:
            case SkillFormulaType.Status_Cleanse:
                return typeof(SelfStateCleanseSkill);
            case SkillFormulaType.Aggro_Up:
                return typeof(HostilitySpikeSkill);
            case SkillFormulaType.Buff_Resist:
            case SkillFormulaType.Buff_Special:
            case SkillFormulaType.Buff_Immune:
                // 데미지가 있는지 확인? Power > 0이면 ParametricDamage일 수 있음?
                // 하지만 보통은 Support.
                if (row.Value1 > 0 && (row.TargetType == "Enemy" || row.TargetType == "All")) 
                    return typeof(ParametricDamageSkill); // 하이브리드?
                return typeof(ParametricSupportSkill);

            case SkillFormulaType.Damage_Phys:
            case SkillFormulaType.Damage_Magic:
            case SkillFormulaType.Status_Bleed: // 액티브 출혈 공격
            case SkillFormulaType.Status_Burn:
                return typeof(ParametricDamageSkill);
                
            default:
                return typeof(ParametricDamageSkill);
        }
    }

    private static void InjectActiveFields(SkillAsset asset, SkillCSVRow row)
    {
        // 공통
        asset.cost = row.CostValue;
        if (System.Enum.TryParse(row.CostType, out SkillCostResource cr)) asset.costResource = cr;
        
        // 타겟 및 사거리 로직
        if (row.TargetType == "Self") 
        {
            SetField(asset, "areaPreset", AreaPreset.Single);
            SetField(asset, "targetAlignment", SkillTargetAlignment.Self);
            SetField(asset, "targetMode", SkillTargetMode.Unit);
        }
        else if (row.TargetType == "Single")
        {
            SetField(asset, "areaPreset", AreaPreset.Single);
            SetField(asset, "targetAlignment", SkillTargetAlignment.Enemy);
            SetField(asset, "targetMode", SkillTargetMode.Unit);
        }
        else if (row.TargetType == "Tile")
        {
            // 타일 타겟팅은 보통 'Any' 정렬(또는 특정 로직)을 의미함
            // 연막탄은 타일 타겟팅을 사용함.
            SetField(asset, "targetMode", SkillTargetMode.Tile);
            SetField(asset, "targetAlignment", SkillTargetAlignment.Any); 
            // AreaPreset은 특정 스킬 로직에 의존하거나 기본값 Single(1 타일)일 수 있음
            SetField(asset, "areaPreset", AreaPreset.Single);
        }
        else if (row.TargetType == "All")
        {
             SetField(asset, "targetAlignment", SkillTargetAlignment.Enemy);
             SetField(asset, "targetMode", SkillTargetMode.Unit);
        }

        // 사거리에 따른 애니메이션 및 GapClose 자동 감지
        // 사거리 > 1이면 원거리/투사체 -> Gap Close 없음
        // 사거리 <= 1이면 근접 -> Gap Close 있음
        if (row.Range > 1)
        {
            SetField(asset, "animKind", SkillAnimKind.Ranged);
            SetField(asset, "useGapCloseJump", false);
        }
        else
        {
            SetField(asset, "animKind", SkillAnimKind.Melee);
            SetField(asset, "useGapCloseJump", true);
        }

        // 파워 (해당되는 경우)
        SetField(asset, "power", row.Value1);
        
        // 공식에 따른 세부 사항
        SkillFormulaType ft = ParseFormulaType(row.FormulaType);
        if (ft == SkillFormulaType.Damage_Phys) SetField(asset, "school", DamageSchool.Physical);
        if (ft == SkillFormulaType.Damage_Magic) SetField(asset, "school", DamageSchool.Magical);
        
        if (row.FormulaType == "Field_Create" && asset is SelfBeastDomainSkill bd)
        {
            bd.durationTurns = (int)row.Value1;
            // CSV에서 반경을 알 수 없음, 기본값 2
        }
        
        if (asset is HostilitySpikeSkill hs) hs.referenceMultiplier = row.Value1;
        if (asset is SelfVigilanceSkill vs) vs.durationTurns = (int)row.Value1;
        if (asset is SelfIsolationTimedSkill iso) iso.baseDefenseDurationTurns = (int)row.Value1;
    }

    private static void InjectTrainingLogic(SkillAsset asset, SkillCSVRow row, int routeIndex)
    {
        SkillFormulaType ft = ParseFormulaType(row.FormulaType);
        
        // 필드 매핑 전략:
        // "기능"을 식별하고 설정:
        // 1. 불리언 활성화 (예: trainingApplyBleed = true)
        // 2. 루트 인덱스 (예: routeForBleed = routeIndex)
        // 3. 값 (예: trainingBleedStacks = Value1)
        
        switch (ft)
        {
            case SkillFormulaType.Status_Bleed:
                SetTraining(asset, routeIndex, "Bleed", true, "trainingApplyBleed", "routeForBleed");
                SetField(asset, "trainingBleedStacks", (int)row.Value1);
                break;
            case SkillFormulaType.Status_Burn:
                SetTraining(asset, routeIndex, "Ignition", true, "trainingApplyIgnition", "routeForIgnition");
                SetField(asset, "trainingIgnitionStacks", (int)row.Value1);
                break;
            case SkillFormulaType.Status_Debuff:
                if (row.ValueStr == "Fear")
                {
                    SetTraining(asset, routeIndex, "Fear", true, "trainingApplyFear", "routeForFear");
                }
                else if (row.ValueStr == "Suppress")
                {
                    // "Suppress"는 trainingSuppressionOnHit (int)를 값과 플래그(>0) 둘 다로 사용하므로 불리언 필드가 없음.
                    // SetTraining은 int 필드에 불리언 체크를 설정하려고 하면 실패함.
                    SetField(asset, "routeForSuppression", routeIndex);
                    SetField(asset, "trainingSuppressionOnHit", (int)row.Value1);
                }
                break;
            case SkillFormulaType.Buff_Stat:
                if (row.ValueStr == "STR")
                {
                    SetTraining(asset, routeIndex, "SelfAtkBuff", true, "trainingUseSelfAtkBuff", "routeForSelfAtkBuff");
                    SetField(asset, "trainingSelfAtkBuffAmount", row.Value1);
                }
                break;
            case SkillFormulaType.Debuff_Stat:
                if (row.ValueStr == "AGI")
                {
                    SetTraining(asset, routeIndex, "AgiDebuff", true, "trainingApplyAgiDebuff", "routeForAgiDebuff");
                }
                break;
            case SkillFormulaType.Cost_Reduce:
                SetTraining(asset, routeIndex, "CostOverride", true, "trainingUseCostOverride", "routeForCostOverride");
                SetField(asset, "trainingCostOverride", (int)row.Value1);
                break;

            case SkillFormulaType.Aggro_Up:
            case SkillFormulaType.Aggro_Down:
                // ParametricHeal / Support / SelfAmbush 모두 `trainingReduceHostility` 또는 `trainingHostilityDown`을 가짐
                // 리팩토링으로 이것들이 완전히 통합되지 않음 (Ambush는 HostilityDown, 다른 것들은 ReduceHostility).
                // 둘 다 시도.
                if (!SetTraining(asset, routeIndex, "HostilityDown", true, "trainingHostilityDown", "routeForHostilityDown"))
                {
                    if (!SetTraining(asset, routeIndex, "ReduceHostility", true, "trainingReduceHostility", "routeForReduceHostility"))
                    {
                         // HostilitySpike / Vigilance: 증가 (UseHostilitySpike / ApplyHostilityDelta?)
                         // HostilitySpike는 `trainingApplyDefenseStacks` 등을 사용함. 주요 기능이 적대감임.
                         // 하지만 훈련이 더 많은 적대감을 추가한다면? 
                         // HostilitySpike에 `trainingUseHostilitySpike`가 있나? 아니, 그건 Vigilance임.
                         // HostilitySpike는 순수 적대감임. 훈련은 방어력을 추가함.
                         // Vigilance는 `trainingUseHostilitySpike`를 가짐.
                         SetTraining(asset, routeIndex, "UseHostilitySpike", true, "trainingUseHostilitySpike", "routeForHostilitySpike");
                    }
                }
                SetField(asset, "hostilityMultiplier", row.Value1); // Ambush
                SetField(asset, "trainingHostilityMultiplier", row.Value1); // Others
                SetField(asset, "hostilityReferenceMultiplier", row.Value1); // Vigilance
                SetField(asset, "trainingHostilityDelta", row.Value1); // Retreat
                SetField(asset, "referenceMultiplier", row.Value1); // HostilitySpike
                break;
            case SkillFormulaType.Resource_Gain: // 환급
                if (row.ValueStr == "Kill")
                {
                    SetTraining(asset, routeIndex, "RefundOnKill", true, "trainingRefundOnKill", "routeForRefundOnKill");
                }
                break;
            case SkillFormulaType.Buff_Resist: // 야수 도메인
                SetTraining(asset, routeIndex, "ApplyResistanceOnCast", true, "trainingApplyResistanceOnCast", "routeForApplyResistanceOnCast");
                SetField(asset, "resistanceStacksOnCast", (int)row.Value1);
                break;
             case SkillFormulaType.Resource_Drain: // 분노 감소 (야수 도메인)
                 SetTraining(asset, routeIndex, "ApplyRageDrainOnTurnStart", true, "trainingApplyRageDrainOnTurnStart", "routeForApplyRageDrainOnTurnStart");
                 break;
            case SkillFormulaType.Passive_Action: // 자유 행동
                SetTraining(asset, routeIndex, "UseFreeAction", true, "trainingUseFreeAction", "routeForFreeAction");
                break;
            case SkillFormulaType.Heal_Turn: // 매복 치유
                 SetTraining(asset, routeIndex, "ApplyHealOnTurnStart", true, "trainingApplyHealOnTurnStart", "routeForApplyHealOnTurnStart");
                 break;
            case SkillFormulaType.Passive_Immune: // 매복 민첩 패널티 없음
                 if (row.ValueStr == "Debuff") 
                 {
                     SetTraining(asset, routeIndex, "NoAgiPenalty", true, "trainingNoAgiPenalty", "routeForNoAgiPenalty");
                 }
                 break;
            case SkillFormulaType.Modify_Range:
                 SetTraining(asset, routeIndex, "AreaOverride", true, "trainingUseAreaOverride", "routeForAreaOverride");
                 // Value1은 float -> enum 매핑 시 프리셋 선택에 사용되거나, 프리셋 이름으로 ValueStr 사용 가능.
                 // 현재는 기본 또는 특정 프리셋 로직 가정.
                 // ValueStr="Vertical3"가 AreaPreset.LineDiagU3 등으로 매핑된다고 가정.
                 if (System.Enum.TryParse(row.ValueStr, out AreaPreset preset)) SetField(asset, "trainingAreaPreset", preset);
                 else SetField(asset, "trainingAreaPreset", AreaPreset.LineDiagU3); // 기본 폴백?
                 break;
            case SkillFormulaType.Post_Move:
                 SetTraining(asset, routeIndex, "PostMove", true, "trainingUsePostMove", "routeForPostMove");
                 SetField(asset, "trainingPostMoveRange", (int)row.Value1);
                 break;
        }
    }

    private static bool SetTraining(object target, int routeIndex, string logName, bool enableVal, string boolField, string routeField)
    {
        FieldInfo bf = target.GetType().GetField(boolField);
        FieldInfo rf = target.GetType().GetField(routeField);
        
        if (bf != null && rf != null)
        {
            bf.SetValue(target, enableVal);
            rf.SetValue(target, routeIndex);
            return true;
        }
        return false;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo f = target.GetType().GetField(fieldName);
        if (f != null)
        {
            // 타입 변환 시도
            try {
                if (f.FieldType == typeof(int) && value is float) f.SetValue(target, (int)(float)value);
                else if (f.FieldType == typeof(float) && value is int) f.SetValue(target, (float)(int)value);
                else f.SetValue(target, value);
            } catch { }
        }
    }

    private static SkillFormulaType ParseFormulaType(string str)
    {
        if (System.Enum.TryParse(str, out SkillFormulaType res)) return res;
        return SkillFormulaType.None;
    }

    // --- 데이터 구조 ---
    private class SkillGroup
    {
        public int ID;
        public string Name;
        public string Description;
        public SkillCSVRow ActiveSettings;
        public List<SkillCSVRow> Trainings = new List<SkillCSVRow>();
    }

    private static List<SkillGroup> GroupRows(List<SkillCSVRow> rows)
    {
        var dict = new Dictionary<int, SkillGroup>();
        
        // 1. 액티브에서 그룹 생성
        foreach (var r in rows)
        {
            if (r.Type == "Active")
            {
                if (!dict.ContainsKey(r.ID))
                {
                    dict.Add(r.ID, new SkillGroup { ID = r.ID, Name = r.Name, Description = r.Description, ActiveSettings = r });
                }
            }
        }
        
        // 2. 훈련 할당
        foreach (var r in rows)
        {
            if (r.Type == "Training")
            {
                if (dict.ContainsKey(r.ParentID))
                {
                    dict[r.ParentID].Trainings.Add(r);
                }
            }
        }
        
        return new List<SkillGroup>(dict.Values);
    }

    // --- CSV 파서 ---
    private static List<SkillCSVRow> ParseCSV(string text)
    {
        var list = new List<SkillCSVRow>();
        var lines = Regex.Split(text, @"\r\n|\n\r|\n|\r");
        
        for (int i = 1; i < lines.Length; i++) // 헤더 건너뛰기
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            // 따옴표 처리를 위한 CSV 분할 정규식
            var matches = Regex.Matches(line, "(?:^|,)(?:\"(?<val>[^\"]*)\"|(?<val>[^,]*))");
            var values = new List<string>();
            foreach (Match m in matches) values.Add(m.Groups["val"].Value);
            
            if (values.Count < 5) continue; // 최소 컬럼 수
            
            try {
                var row = new SkillCSVRow();
                row.ID = int.Parse(values[0]);
                row.Type = values[1];
                row.ParentID = int.Parse(values[2]);
                row.Name = values[3];
                
                row.CostType = values[4];
                int.TryParse(values[5], out row.CostValue);
                
                row.TargetType = values[6];
                int.TryParse(values[7], out row.Range);
                
                row.FormulaType = values[8];
                float.TryParse(values[9], out row.Value1);
                row.ValueStr = values[10];
                
                row.Description = values[11]; // 설명은 비어있거나 유효할 수 있음
                
                if (values.Count > 12)
                {
                    row.OverrideDescription = values[12];
                }
                
                list.Add(row);
            } catch (System.Exception ex) {
                Debug.LogWarning($"[SkillImporter] Parse Error Line {i}: {ex.Message}");
            }
        }
        return list;
    }

    private static void VerifyDirectory(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string[] folders = path.Split('/');
        string current = folders[0];
        for (int i = 1; i < folders.Length; i++)
        {
            string next = current + "/" + folders[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, folders[i]);
            current = next;
        }
    }
}
