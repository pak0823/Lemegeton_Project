using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // 클릭 이벤트용

public class FormationSlotUI : MonoBehaviour, IPointerClickHandler
{
    [Header("Settings")]
    public int slotIndex; // 0 ~ 18 (각 슬롯마다 다르게 설정해야 함)
                          // 맨 왼쪽 위에서부터 0, 1, 2 순으로 배치
    public Image unitImage; // 캐릭터 이미지 (자식 오브젝트)

    private UnitData currentUnit;

    void Awake()
    {
        if (unitImage == null)
        {
            // 첫 번째 자식을 가져옴
            if (transform.childCount > 0)
            {
                unitImage = transform.GetChild(0).GetComponent<Image>();
            }

            // 그래도 없으면 에러 로그 (이건 네가 실수한 거니까)
            if (unitImage == null)
            {
                Debug.LogError($"{name} 슬롯에 'UnitIconImage' 자식이 없거나 Image 컴포넌트가 없다! 확인해라.");
            }
        }
    }

    void Start()
    {
        // 시작할 때 저장된 정보 불러오기
        RefreshUI();
    }

    // UI 갱신 (이미지 교체)
    public void RefreshUI()
    {
        if (PlayerDataManager.Instance == null) return;

        currentUnit = PlayerDataManager.Instance.GetUnitAt(slotIndex);

        if (currentUnit != null)
        {
            unitImage.sprite = currentUnit.UnitStandImage; // UnitData에 있는 이미지 사용
            unitImage.enabled = true;
            unitImage.color = Color.white;
        }
        else
        {
            unitImage.sprite = null;
            unitImage.enabled = false; // 없으면 숨김
        }
    }

    // 슬롯 클릭 시 실행
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return; // 좌클릭이 아니면 함수 종료 (우클릭 등 무시)
        }

        // 매니저에서 현재 선택된 유닛 가져오기
        if (CampUIManager.Instance == null) return;

        UnitData targetUnit = CampUIManager.Instance.selectedUnit;

        // 선택된 유닛이 없으면 아무것도 안 함 (혹은 배치 해제 로직)
        if (targetUnit == null)
        {
            Debug.Log("선택된 유닛이 없습니다.");
            return;
        }

        // 데이터 매니저에 "이 자리에 이 유닛 배치해" 명령
        // (SetFormation 내부에서 중복 배치 처리까지 되어 있음)
        PlayerDataManager.Instance.SetFormation(slotIndex, targetUnit);

        // UI 갱신 (모든 슬롯을 갱신해야 중복된 유닛이 사라지는 게 보임)
        // 비효율적이지만 지금은 가장 확실한 방법: 모든 슬롯을 찾아서 Refresh 때리기
        var allSlots = transform.parent.GetComponentsInChildren<FormationSlotUI>();
        foreach (var slot in allSlots)
        {
            slot.RefreshUI();
        }

        Debug.Log($"{slotIndex}번 슬롯에 {targetUnit.DisplayName} 배치 완료!");
    }
}