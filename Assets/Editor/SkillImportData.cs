using System;

namespace Lemegeton.Editor
{
    public enum SkillRowType { Active, Training }
    
    public enum SkillFormulaType 
    { 
        None,
        // Generic Action
        Damage_Phys, 
        Damage_Magic, 
        Heal, 
        Heal_Turn, // Turn Start Heal

        // Status Effects
        Status_Bleed, 
        Status_Debuff, // Fear, Suppress, etc. (Target)
        Status_Burn, 
        Status_Buff, // Stealth, etc. (Self/Target)
        
        // Stat Modifiers
        Buff_Stat, // Strength Up, etc.
        Debuff_Stat, // Agility Down, etc.
        Buff_Resist, 
        Buff_Special, // HeatShield etc.
        Buff_Immune, // Added missing value

        // Logic / Mechanics
        Field_Create,       // Beast Domain
        Resource_Gain,      // Refund on Kill
        Resource_Drain,     // Rage Drain
        Cost_Reduce,        // Cost Override
        
        // Passive Rules
        Passive_Immune,     // No Agi Penalty
        Passive_Action,     // Free Action, Continuous Action
        Modify_Range,       // Area Override
        Post_Move,          // Post Move
        Remove_Buff,        // Remove Stealth
        Aggro_Down,         // Reduce Hostility
        Aggro_Up,           // Increase Hostility (Taunt/Spike)
        Debuff_Stack,       // Stackable (Weakness)
        Status_Cleanse,     // Cleanse
    }

    [Serializable]
    public class SkillCSVRow
    {
        public int ID;
        public string Type; // "Active" or "Training"
        public int ParentID;
        public string Name;
        // Cost
        public string CostType;
        public int CostValue;
        // Target
        public string TargetType;
        public int Range;
        // Formula
        public string FormulaType;
        public float Value1;
        public string ValueStr;
        // Text
        public string Description;
        // Optional
        public string OverrideDescription;

        public override string ToString()
        {
            return $"[{ID}] {Name} ({Type}) - {FormulaType}";
        }
    }
}
