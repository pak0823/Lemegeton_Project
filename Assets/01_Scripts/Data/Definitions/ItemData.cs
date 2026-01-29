using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Data/ItemData")]
public class ItemData : ScriptableObject
{
    [Header("고유 ID (필수/중복불가)")]
    public string itemID;        // 예: "MAT_WOOD", "POTION_HP_S"

    [Header("기본 정보")]
    public string itemName;      // 표시 이름
    public Sprite itemIcon;      // UI 표시용 아이콘
    [TextArea]
    public string itemDescription;   // 아이템 설명

    [Header("속성")]
    public ItemType itemType;    // 아이템 종류
    public int maxStack = 6;    // 한 슬롯에 몇 개까지 겹쳐지는지
}