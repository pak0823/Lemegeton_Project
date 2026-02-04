using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // 클릭 이벤트용

public class FormationSlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("Settings")]
    public int slotIndex; // 0 ~ 18 (각 슬롯마다 다르게 설정해야 함)
                          // 맨 왼쪽 위에서부터 0, 1, 2 순으로 배치
    public Image unitImage; // 캐릭터 이미지 (자식 오브젝트)

    private UnitData currentUnit;
    private CanvasGroup canvasGroup;

    void Awake()
    {
        if (unitImage == null)
        {
            // 이미지 찾는 로직 유지
            if (unitImage == null && transform.childCount > 0)
                unitImage = transform.GetChild(0).GetComponent<Image>();

            // CanvasGroup 없으면 추가 (드래그 중 반투명 효과용)
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    void Start()
    {
        // 시작할 때 저장된 정보 불러오기
        RefreshUI();

        // 매니저의 데이터 변경 이벤트 구독
        if (PlayerDataManager.Instance != null)
            PlayerDataManager.Instance.OnFormationChanged += RefreshUI;
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제
        if (PlayerDataManager.Instance != null)
            PlayerDataManager.Instance.OnFormationChanged -= RefreshUI;
    }

    // UI 갱신
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
        // 드래그 중이었다면 클릭 이벤트 무시
        if (eventData.dragging) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;

        // 배치 모드(상단 토글 선택 상태)라면 배치 수행
        if (CampUIManager.Instance != null && CampUIManager.Instance.selectedUnit != null)
        {
            UnitData targetUnit = CampUIManager.Instance.selectedUnit;
            PlayerDataManager.Instance.SetFormation(slotIndex, targetUnit);
        }

        // 배치 모드가 아니고 유닛이 있으면? -> (선택적으로) 클릭해서 정보 보여주기나 해제 로직 등 추가 가능


        //// 데이터 매니저에 "이 자리에 이 유닛 배치해" 명령
        //// (SetFormation 내부에서 중복 배치 처리까지 되어 있음)
        //PlayerDataManager.Instance.SetFormation(slotIndex, targetUnit);

        //// UI 갱신 (모든 슬롯을 갱신해야 중복된 유닛이 사라지는 게 보임)
        //// 비효율적이지만 지금은 가장 확실한 방법: 모든 슬롯을 찾아서 Refresh 때리기
        //var allSlots = transform.parent.GetComponentsInChildren<FormationSlotUI>();
        //foreach (var slot in allSlots)
        //{
        //    slot.RefreshUI();
        //}

        //Debug.Log($"{slotIndex}번 슬롯에 {targetUnit.DisplayName} 배치 완료!");
    }

    // --- 드래그 & 드롭 구현 ---

    // 드래그 시작
    public void OnBeginDrag(PointerEventData eventData)
    {
        // 빈 슬롯은 드래그 못 함
        if (currentUnit == null) return;

        // 매니저에게 고스트 이미지 띄우라고 요청
        if (CampUIManager.Instance != null)
            CampUIManager.Instance.StartDrag(unitImage.sprite);

        // 내 이미지는 살짝 투명하게
        canvasGroup.alpha = 0.6f;
        // 레이캐스트를 꺼서 드롭 이벤트가 내 아래(혹은 다른 슬롯)로 통과되게 함 (필수 아님, 상황따라)
        canvasGroup.blocksRaycasts = false;
    }

    // 드래그 중 (매 프레임)
    public void OnDrag(PointerEventData eventData)
    {
        if (currentUnit == null) return;
        if (CampUIManager.Instance != null)
            CampUIManager.Instance.UpdateDragPosition(eventData.position);
    }

    // 드래그 끝 (마우스 뗐을 때)
    public void OnEndDrag(PointerEventData eventData)
    {
        if (CampUIManager.Instance != null)
            CampUIManager.Instance.EndDrag();

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true; // 다시 켜줘야 클릭 됨
    }

    // 드롭 받음 (내가 도착지점일 때)
    public void OnDrop(PointerEventData eventData)
    {
        // 드래그해온 물체(pointerDrag)가 FormationSlotUI인지 확인
        FormationSlotUI sourceSlot = eventData.pointerDrag.GetComponent<FormationSlotUI>();

        if (sourceSlot != null && sourceSlot.currentUnit != null)
        {
            // "저쪽 슬롯(source)에 있던 유닛을 내 자리(this.slotIndex)로 옮겨라"
            // SetFormation 내부 로직이 이미 스왑을 지원하므로 이거 한 방이면 됨.
            PlayerDataManager.Instance.SetFormation(this.slotIndex, sourceSlot.currentUnit);

            // 데이터 매니저가 OnFormationChanged 이벤트를 쏘면,
            // 나랑 저쪽 슬롯 둘 다 RefreshUI가 자동으로 실행됨.
        }
    }
}