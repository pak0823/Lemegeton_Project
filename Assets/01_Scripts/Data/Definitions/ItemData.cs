using UnityEngine;



[CreateAssetMenu(fileName = "New Item", menuName = "Data/Definition/Item")]

public class ItemData : ScriptableObject

{

    [Header("고유 ID (Primary Key)")]

    public string itemID;



    [Header("기본 정보")]

    public string itemName;      // 표시 이름

    [TextArea]

    public string itemDescription;   // 아이템 설명



    [Header("Addressables Atlas 설정")]

    public string atlasAddress;  // Atlas 에셋의 Address (예: ItemAtlas)

    public string spriteName;    // Atlas 내부의 Sprite 이름 (예: icon_potion)



    [Header("속성")]

    public ItemType itemType;    // 아이템 종류

    public int maxStack = 6;    // 한 슬롯에 몇 개까지 겹쳐지는지



    [Header("소비 효과 (Optional)")]
    public Project.Data.ItemEffectSO useContextEffect; // 소비 아이템일 경우 할당

    // 런타임에 이 문자열로 이미지를 로드: "ItemAtlas[icon_potion]"

    public string GetAtlasKey()

    {

        // 로그를 통해 실제로 어떤 값이 들어있는지 강제로 확인

        //Debug.Log($"[Debug] Item: {itemName}, Atlas: '{atlasAddress}', Sprite: '{spriteName}'");



        if (string.IsNullOrEmpty(atlasAddress) || string.IsNullOrEmpty(spriteName))

        {

            return string.Empty; // 여기서 빈 값이 반환되어 Key=[] 에러가 발생함 

        }

        return $"{atlasAddress}[{spriteName}]";

    }

}