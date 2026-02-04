using UnityEngine;

[CreateAssetMenu(menuName = "Battle/TraitAsset", fileName = "Trait_New")]
public class TraitAsset : ScriptableObject
{
    [Header("Display")]
    public string displayName; // 성격 이름 (예: 다혈질)
    [TextArea] public string description; // 성격 설명

    // 추후 성격 효과(스탯 보정 등) 로직이 필요하면 여기에 추가
}