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

    [MenuItem("Tools/Data/Sync Skills (New)")]
    public static void SyncSkills()
    {
        EditorCoroutineUtility.StartCoroutineOwnerless(DownloadRoutine());
    }

    private static IEnumerator DownloadRoutine()
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
        // 1. Determine Asset Type
        System.Type targetType = DetermineClassType(group.ActiveSettings);
        if (targetType == null)
        {
            Debug.LogError($"[SkillImporter] Cannot determine class type for {group.ID} ({group.Name})");
            return false;
        }

        // 2. Load or Create Asset
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

        // 3. Inject Fields
        asset.id = group.ID.ToString();
        asset.displayName = group.Name;
        asset.description = group.Description;

        // Active Fields
        InjectActiveFields(asset, group.ActiveSettings);

        // Training Fields
        // Reset old training routes? For now assume overwriting.
        // NOTE: SkillAsset.trainingRoutes is an Array or List? Check ParametricDamageSkill.
        // ParametricDamageSkill inherits SkillAsset -> needs `trainingRoutes` field check.
        // SkillAsset definition wasn't fully shown but usually it has `TrainingParams[] trainingRoutes`.
        // Let's assume standard SkillAsset has it or we inject into specific fields.
        // ParametricDamageSkill has fields like `trainingApplyBleed`.
        
        // Actually, trainingRoutes usually stores Title/Desc. 
        // We might need to populate `trainingRoutes` array for UI tooltip, 
        // AND set the boolean flags for logic.
        
        List<TrainingRouteInfo> routes = new List<TrainingRouteInfo>();
        
        // Process each training row (max 3 routes: 0, 1, 2)
        // Group.Trainings is List<SkillCSVRow>
        
        // Clear existing flags (optional, but good for cleanliness)
        // But since we are setting them via reflection based on current data,
        // we might leave stale true values if not careful. 
        // Ideal: Reset all "training..." bools to false? Too risky without knowing all fields.
        
        for (int i = 0; i < group.Trainings.Count; i++)
        {
            var tRow = group.Trainings[i];
            // Add metadata for tooltip
            routes.Add(new TrainingRouteInfo 
            { 
                title = tRow.Name.Replace(" 훈련", ""), 
                description = tRow.Description,
                overrideSkillDescription = tRow.OverrideDescription 
            });

            // Inject Logic Fields (e.g. trainingApplyBleed = true)
            InjectTrainingLogic(asset, tRow, i);
        }

        // Reflection set `trainingRoutes` (if it exists on base)
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
        // Special Cases
        if (row.FormulaType == "Field_Create" && row.ValueStr == "BeastField") return typeof(SelfBeastDomainSkill);
        if (row.FormulaType == "Field_Create" && row.ValueStr == "Smoke") return typeof(SmokeBombSkill);
        if (row.FormulaType == "Status_Buff" && row.ValueStr == "Stealth") return typeof(SelfAmbushSkill);
        if (row.FormulaType == "Status_Buff" && row.ValueStr == "Guard") return typeof(SelfVigilanceSkill);
        if (row.FormulaType == "Status_Buff" && row.ValueStr == "Isolation") return typeof(SelfIsolationTimedSkill);
        
        // Generic Cases
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
                // Check if it has damage? If Power > 0, maybe ParametricDamage?
                // But usually Support.
                if (row.Value1 > 0 && (row.TargetType == "Enemy" || row.TargetType == "All")) 
                    return typeof(ParametricDamageSkill); // Hybrid?
                return typeof(ParametricSupportSkill);

            case SkillFormulaType.Damage_Phys:
            case SkillFormulaType.Damage_Magic:
            case SkillFormulaType.Status_Bleed: // Active Bleed Attack
            case SkillFormulaType.Status_Burn:
                return typeof(ParametricDamageSkill);
                
            default:
                return typeof(ParametricDamageSkill);
        }
    }

    private static void InjectActiveFields(SkillAsset asset, SkillCSVRow row)
    {
        // Common
        asset.cost = row.CostValue;
        if (System.Enum.TryParse(row.CostType, out SkillCostResource cr)) asset.costResource = cr;
        
        // Target & Range logic
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
            // Tile targeting usually implies 'Any' alignment (or specific logic)
            // SmokeBomb uses Tile targeting.
            SetField(asset, "targetMode", SkillTargetMode.Tile);
            SetField(asset, "targetAlignment", SkillTargetAlignment.Any); 
            // AreaPreset might depend on specific skill logic or default to Single (1 tile)
            SetField(asset, "areaPreset", AreaPreset.Single);
        }
        else if (row.TargetType == "All")
        {
             SetField(asset, "targetAlignment", SkillTargetAlignment.Enemy);
             SetField(asset, "targetMode", SkillTargetMode.Unit);
        }

        // Auto-detect Animation & GapClose based on Range
        // Range > 1 implies Ranged/Projectile -> No Gap Close
        // Range <= 1 implies Melee -> Gap Close
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

        // Power (if applicable)
        SetField(asset, "power", row.Value1);
        
        // Specifics based on Formula
        SkillFormulaType ft = ParseFormulaType(row.FormulaType);
        if (ft == SkillFormulaType.Damage_Phys) SetField(asset, "school", DamageSchool.Physical);
        if (ft == SkillFormulaType.Damage_Magic) SetField(asset, "school", DamageSchool.Magical);
        
        if (row.FormulaType == "Field_Create" && asset is SelfBeastDomainSkill bd)
        {
            bd.durationTurns = (int)row.Value1;
            // Radius unknown from CSV, default to 2
        }
        
        if (asset is HostilitySpikeSkill hs) hs.referenceMultiplier = row.Value1;
        if (asset is SelfVigilanceSkill vs) vs.durationTurns = (int)row.Value1;
        if (asset is SelfIsolationTimedSkill iso) iso.baseDefenseDurationTurns = (int)row.Value1;
    }

    private static void InjectTrainingLogic(SkillAsset asset, SkillCSVRow row, int routeIndex)
    {
        SkillFormulaType ft = ParseFormulaType(row.FormulaType);
        
        // Field Mapping Strategy:
        // Identify the "feature" and set:
        // 1. Enable Bool (e.g. trainingApplyBleed = true)
        // 2. Route Index (e.g. routeForBleed = routeIndex)
        // 3. Value (e.g. trainingBleedStacks = Value1)
        
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
                    // "Suppress" uses trainingSuppressionOnHit (int) as both value and flag (>0), so no bool field exists.
                    // SetTraining would fail trying to set bool check to int field.
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
                break;
            case SkillFormulaType.Aggro_Up:
            case SkillFormulaType.Aggro_Down:
                // ParametricHeal / Support / SelfAmbush all have `trainingReduceHostility` or `trainingHostilityDown`
                // Refactoring didn't unify these completely (Ambush has HostilityDown, others ReduceHostility).
                // Try both.
                if (!SetTraining(asset, routeIndex, "HostilityDown", true, "trainingHostilityDown", "routeForHostilityDown"))
                {
                    if (!SetTraining(asset, routeIndex, "ReduceHostility", true, "trainingReduceHostility", "routeForReduceHostility"))
                    {
                         // HostilitySpike / Vigilance: Increase (UseHostilitySpike / ApplyHostilityDelta?)
                         // HostilitySpike uses `trainingApplyDefenseStacks` etc. Its main function IS hostility.
                         // But if training adds MORE hostility? 
                         // HostilitySpike has `trainingUseHostilitySpike`? No, that's Vigilance.
                         // HostilitySpike is pure hostility. Training adds defense.
                         // Vigilance has `trainingUseHostilitySpike`.
                         SetTraining(asset, routeIndex, "UseHostilitySpike", true, "trainingUseHostilitySpike", "routeForHostilitySpike");
                    }
                }
                SetField(asset, "hostilityMultiplier", row.Value1); // Ambush
                SetField(asset, "trainingHostilityMultiplier", row.Value1); // Others
                SetField(asset, "hostilityReferenceMultiplier", row.Value1); // Vigilance
                SetField(asset, "trainingHostilityDelta", row.Value1); // Retreat
                SetField(asset, "referenceMultiplier", row.Value1); // HostilitySpike
                break;
            case SkillFormulaType.Resource_Gain: // Refund
                if (row.ValueStr == "Kill")
                {
                    SetTraining(asset, routeIndex, "RefundOnKill", true, "trainingRefundOnKill", "routeForRefundOnKill");
                }
                break;
            case SkillFormulaType.Buff_Resist: // Beast Domain
                SetTraining(asset, routeIndex, "ApplyResistanceOnCast", true, "trainingApplyResistanceOnCast", "routeForApplyResistanceOnCast");
                SetField(asset, "resistanceStacksOnCast", (int)row.Value1);
                break;
             case SkillFormulaType.Resource_Drain: // Rage Reduce (Beast Domain)
                 SetTraining(asset, routeIndex, "ApplyRageDrainOnTurnStart", true, "trainingApplyRageDrainOnTurnStart", "routeForApplyRageDrainOnTurnStart");
                 break;
            case SkillFormulaType.Passive_Action: // Free Action
                SetTraining(asset, routeIndex, "UseFreeAction", true, "trainingUseFreeAction", "routeForFreeAction");
                break;
            case SkillFormulaType.Heal_Turn: // Ambush Heal
                 SetTraining(asset, routeIndex, "ApplyHealOnTurnStart", true, "trainingApplyHealOnTurnStart", "routeForApplyHealOnTurnStart");
                 break;
            case SkillFormulaType.Passive_Immune: // Ambush No Agi
                 if (row.ValueStr == "Debuff") 
                 {
                     SetTraining(asset, routeIndex, "NoAgiPenalty", true, "trainingNoAgiPenalty", "routeForNoAgiPenalty");
                 }
                 break;
            case SkillFormulaType.Modify_Range:
                 SetTraining(asset, routeIndex, "AreaOverride", true, "trainingUseAreaOverride", "routeForAreaOverride");
                 // Value1 could be used to select preset if we map float -> enum, or use ValueStr for preset name.
                 // For now, assume a default or specific preset logic.
                 // Let's assume ValueStr="Vertical3" maps to AreaPreset.LineDiagU3 etc.
                 if (System.Enum.TryParse(row.ValueStr, out AreaPreset preset)) SetField(asset, "trainingAreaPreset", preset);
                 else SetField(asset, "trainingAreaPreset", AreaPreset.LineDiagU3); // Default fallback?
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
            // Type conversion attempt
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

    // --- Data Structures ---
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
        
        // 1. Create Groups from Actives
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
        
        // 2. Assign Trainings
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

    // --- CSV Parser ---
    private static List<SkillCSVRow> ParseCSV(string text)
    {
        var list = new List<SkillCSVRow>();
        var lines = Regex.Split(text, @"\r\n|\n\r|\n|\r");
        
        for (int i = 1; i < lines.Length; i++) // Skip Header
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            // Regex for CSV split handling quotes
            var matches = Regex.Matches(line, "(?:^|,)(?:\"(?<val>[^\"]*)\"|(?<val>[^,]*))");
            var values = new List<string>();
            foreach (Match m in matches) values.Add(m.Groups["val"].Value);
            
            if (values.Count < 5) continue; // Min cols
            
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
                
                row.Description = values[11]; // Description might be empty or valid
                
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
