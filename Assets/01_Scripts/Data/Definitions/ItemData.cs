using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Data/ItemData")]
public class ItemData : ScriptableObject
{
    [Header("고유 ID (Primary Key)")]
    public string itemID;        // 예: "MAT_WOOD", "POTION_HP_S"

    [Header("기본 정보")]
    public string itemName;      // 표시 이름
    //public Sprite itemIcon;      // UI 표시용 아이콘
    [TextArea]
    public string itemDescription;   // 아이템 설명

    [Header("Addressables Atlas 설정")]
    public string atlasAddress;  // Atlas 에셋의 Address (예: ItemAtlas)
    public string spriteName;    // Atlas 내부의 Sprite 이름 (예: icon_potion)

    [Header("속성")]
    public ItemType itemType;    // 아이템 종류
    public int maxStack = 6;    // 한 슬롯에 몇 개까지 겹쳐지는지

    // 런타임에 이 문자열로 이미지를 로드: "ItemAtlas[icon_potion]"
    public string GetAtlasKey() => $"{atlasAddress}[{spriteName}]";
}