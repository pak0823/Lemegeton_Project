using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public class AddressablesBuilder : EditorWindow
{
    [MenuItem("Tools/Build Addressables")]
    public static void BuildContent()
    {
        Debug.Log("Starting Addressables Build...");
        AddressableAssetSettings.CleanPlayerContent();
        AddressableAssetSettings.BuildPlayerContent();
        Debug.Log("Addressables Build Complete!");
    }
}
