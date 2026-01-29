using System;

[System.Serializable]
public class InventoryItem
{
    public string itemID; // ItemData의 itemID와 매칭
    public int count;
    public int slotIndex; //인벤토리 슬롯 번호

    public InventoryItem(string id, int count, int index)
    {
        this.itemID = id;
        this.count = count;
        this.slotIndex = index;
    }
}