using System.Collections;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

public class MasterDataImporter
{
    [MenuItem("Tools/Lemegeton/Data/Import All")]
    public static void ImportAllCallback()
    {
        EditorCoroutineUtility.StartCoroutineOwnerless(ImportAllRoutine());
    }

    private static IEnumerator ImportAllRoutine()
    {
        float startTime = Time.realtimeSinceStartup;
        Debug.Log("<b>[MasterImporter]</b> Starting ALL Imports...");

        // 1. Skills
        Debug.Log("<b>[MasterImporter]</b> Importing Skills...");
        yield return SkillDataImporter.ImportRoutine();
        Debug.Log("<b>[MasterImporter]</b> Skills Imported.");

        // 2. Units (Player/Enemy Units)
        Debug.Log("<b>[MasterImporter]</b> Importing Units...");
        yield return GoogleSheetImporter.ImportRoutine();
        Debug.Log("<b>[MasterImporter]</b> Units Imported.");

        // 3. Items
        Debug.Log("<b>[MasterImporter]</b> Importing Items...");
        yield return ItemDataImporter.ImportRoutine();
        Debug.Log("<b>[MasterImporter]</b> Items Imported.");

        // 4. Gatherables
        Debug.Log("<b>[MasterImporter]</b> Importing Gatherables...");
        yield return GatherableDataImporter.ImportRoutine();
        Debug.Log("<b>[MasterImporter]</b> Gatherables Imported.");

        // Finalize
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        float duration = Time.realtimeSinceStartup - startTime;
        Debug.Log($"<b>[MasterImporter]</b> All Imports Completed in {duration:F2} seconds.");
        EditorUtility.DisplayDialog("Import All", $"All data imported successfully in {duration:F2} seconds.", "OK");
    }
}
