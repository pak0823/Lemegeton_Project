using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "ItemLibrary", menuName = "Data/ItemLibrary")]
public class ItemLibrary : ScriptableObject
{
    // 에디터에서 모든 ItemData(SO)를 여기에 드래그해서 넣어라
    public List<ItemData> allItems = new List<ItemData>();

    // ID로 아이템 데이터를 빠르게 찾기 위한 사전 (런타임용)
    private Dictionary<string, ItemData> _itemDictionary;

    public void Init()
    {
        // 리스트를 딕셔너리로 변환해서 검색 속도를 O(1)로 최적화
        _itemDictionary = allItems.ToDictionary(item => item.itemID, item => item);
    }

    public ItemData GetItem(string id)
    {
        if (_itemDictionary == null) Init();

        if (_itemDictionary.TryGetValue(id, out var data))
        {
            return data;
        }

        Debug.LogWarning($"[ItemLibrary] ID '{id}'를 가진 아이템을 찾을 수 없습니다.");
        return null;
    }
}