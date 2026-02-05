public interface IInventory
{
    /// <summary>
    /// 아이템을 인벤토리에 추가합니다.
    /// </summary>
    /// <param name="id">아이템 ID</param>
    /// <param name="amount">수량</param>
    void AddItem(string id, int amount);

    /// <summary>
    /// 아이템을 소모합니다. 보유량이 부족하면 false를 반환합니다.
    /// </summary>
    /// <param name="id">아이템 ID</param>
    /// <param name="amount">수량</param>
    /// <returns>성공 여부</returns>
    bool ConsumeItem(string id, int amount);

    /// <summary>
    /// 특정 아이템의 현재 보유량을 반환합니다.
    /// </summary>
    /// <param name="id">아이템 ID</param>
    /// <returns>보유 수량</returns>
    int GetItemCount(string id);
    
    /// <summary>
    /// 아이템을 추가할 수 있는지 확인합니다. (공간 부족 시 false)
    /// </summary>
    bool CanAddItem(string id, int count);
    
    /// <summary>
    /// 인벤토리에 여유 공간이 있는지 확인합니다.
    /// </summary>
    bool HasSpace();
}
