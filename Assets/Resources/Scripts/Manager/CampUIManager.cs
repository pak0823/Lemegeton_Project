using UnityEngine;
using UnityEngine.UI;

public class CampUIManager : MonoBehaviour
{
    public static CampUIManager Instance;

    [Header("현재 선택된 유닛 (배치 대기 중)")]
    public UnitData selectedUnit;

    [Header("UI References")]
    public ToggleGroup charToggleGroup; // 상단 캐릭터 토글 그룹

    void Awake()
    {
        Instance = this;
    }

    // 상단 탭에서 캐릭터 토글을 눌렀을 때 호출될 함수
    public void OnSelectCharacter(UnitData unit)
    {
        selectedUnit = unit;
        Debug.Log($"배치 모드: {unit.DisplayName} 선택됨");
    }

    // 배치 완료 후 처리가 필요하면 여기에 작성 (예: 선택 해제 등)
}