using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance {  get; private set; }

    [Header("Temporary Currency")]
    public int gold = 1000; // 테스트용 초기 자금

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 재화가 충분한지 확인
    public bool HasAmount(int amount)
    {
        return gold >= amount;
    }

    // 재화 소비 (성공 시 true 리턴)
    public bool Consume(int amount)
    {
        if (gold >= amount)
        {
            gold -= amount;
            Debug.Log($"[Currency] {amount} 소모됨. 남은 재화: {gold}");
            return true;
        }
        else
        {
            Debug.Log($"[Currency] 재화 부족! (필요: {amount}, 보유: {gold})");
            return false;
        }
    }
}