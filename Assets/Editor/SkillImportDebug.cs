using UnityEngine;
using UnityEditor;

public class SkillImportDebug
{
    [MenuItem("Tools/Debug/Test Skill Import (Local)")]
    public static void RunTest()
    {
        string csv = @"ID,Type,ParentID,Name,CostType,CostValue,TargetType,Range,FormulaType,Value1,ValueStr,Description
9901,Active,0,TestFireball,MP,10,Single,3,Status_Burn,0,,Test Fireball Skill
990101,Training,9901,Add Ignition,,0,,,Status_Burn,3,,Adds 3 stacks of Ignition
9902,Active,0,TestHeal,MP,15,Single,4,Heal,20,,Test Heal Skill
990201,Training,9902,Reduce Cost,,0,,,Cost_Reduce,5,,Override cost to 5";

        Debug.Log("Running Local Skill Import Test...");
        SkillDataImporter.ProcessCSV(csv);
    }
}
