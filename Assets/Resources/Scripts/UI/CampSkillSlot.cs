using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CampSkillSlot : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Text nameText;
    [SerializeField] private Button skillButton;
    [SerializeField] private GameObject selectionHighlight; // 현재 선택된 스킬 표시용 테두리

    [Header("Training UI")]
    [SerializeField] private Transform trainingContainer; // 훈련 버튼들이 들어갈 부모 (Horizontal Layout)
    [SerializeField] private GameObject trainingSlotPrefab; // 위에서 만든 CampTrainingSlot 프리팹

    private SkillAsset mySkill;
    private UnitData myUnit;
    private CampSkillPage parentPage;
    private int currentFocusedTrainingIndex = -1;

    private List<CampTrainingSlot> trainingSlots = new List<CampTrainingSlot>();
    public SkillAsset GetSkill() => mySkill;        // 외부에서 이 슬롯이 어떤 스킬인지 확인용

    // 초기화 함수
    public void Setup(UnitData unit, SkillAsset skill, CampSkillPage page)
    {
        myUnit = unit;
        mySkill = skill;
        parentPage = page;

        if (mySkill != null)
        {
            nameText.text = mySkill.displayName;
        }

        // 스킬 버튼 클릭 (설명창 갱신용)
        skillButton.onClick.RemoveAllListeners();
        skillButton.onClick.AddListener(() => parentPage.OnSlotClicked(this, mySkill, unit));

        // 훈련 슬롯 생성
        CreateTrainingSlots();

        // 훈련 상태(색상) 갱신
        RefreshTrainingVisuals();

        SetSelected(false);

        // 하이라이트 초기화
        if (selectionHighlight) selectionHighlight.SetActive(false);
    }

    private void CreateTrainingSlots()
    {
        // 기존 슬롯 제거(초기화)
        foreach (Transform child in trainingContainer) Destroy(child.gameObject);
        trainingSlots.Clear();

        // SkillAsset에서 실제 훈련 데이터 가져오기
        if (mySkill == null || mySkill.trainingRoutes == null) return;

        // trainingRoutes 배열 길이만큼 반복
        for (int i = 0; i < mySkill.trainingRoutes.Length; i++)
        {
            // 데이터 가져오기
            var routeInfo = mySkill.trainingRoutes[i];

            // DB에서 잠금 여부 확인
            // 비용이 0이면 무조건 해금(false), 아니면 DB 확인함
            bool isLocked = true;
            if (routeInfo.trainingCost <= 0)
            {
                isLocked = false; // 무료 훈련은 항상 열림
            }
            else if (TrainingDB.Instance != null)
            {
                // DB에 기록이 있으면 그 값을 따름
                isLocked = !TrainingDB.Instance.IsUnlocked(myUnit, mySkill, i);
            }

            GameObject go = Instantiate(trainingSlotPrefab, trainingContainer);
            CampTrainingSlot tSlot = go.GetComponent<CampTrainingSlot>();

            string tName = string.IsNullOrEmpty(routeInfo.title) ? $"훈련 {i + 1}" : routeInfo.title;

            // Setup 호출 (비용 전달)
            tSlot.Setup(i, tName, routeInfo.trainingCost, isLocked, this);
            trainingSlots.Add(tSlot);
        }
    }
    public void SimulateClick()
    {
        if (skillButton != null)
        {
            skillButton.onClick.Invoke();
        }
    }

    // 페이지가 "너 선택됨/해제됨" 알려줄 때 호출
    public void SetSelected(bool isSelected)
    {
        if (selectionHighlight) selectionHighlight.SetActive(isSelected);
        // 혹은 버튼 색깔을 바꾸거나 interactable을 끄거나 등등 시각 효과
    }

    // 잠긴 훈련이 눌렸을 때 Page로 토스
    public void OnLockedTrainingSelected(int index, int cost, Transform slotTransform)
    {
        // 전체 초기화
        parentPage.DeselectAllHighlights();

        parentPage.OnLockedTrainingClicked(myUnit, mySkill, index, cost, slotTransform);

        RefreshTrainingVisuals();
    }

    // 훈련 슬롯이 해금 됐을 때 호출
    public void OnTrainingUnlocked(int index)
    {
        // DB에 해금 사실 기록
        if (TrainingDB.Instance != null)
        {
            TrainingDB.Instance.UnlockRoute(myUnit, mySkill, index);
        }

        // 해금됐으니 얘를 포커스 잡음
        currentFocusedTrainingIndex = index;

        // UI 전체 다시 그리기 (잠금 풀린 것 반영)
        CreateTrainingSlots();
        RefreshTrainingVisuals();

        if (index < trainingSlots.Count)
        {
            // 새로 생성된 훈련 슬롯을 바로 선택
            OnTrainingSelected(index, trainingSlots[index].transform);
        }
    }

    // 훈련 버튼이 눌렸을 때 실행
    public void OnTrainingSelected(int index, Transform slotTransform)
    {
        // 전체 초기화 요청 (다른 스킬, 다른 훈련 하이라이트 다 끄기)
        parentPage.DeselectAllHighlights();

        // 포커스 인덱스 갱신 (내 것만 켬)
        currentFocusedTrainingIndex = index;

        // 페이지에 알림
        parentPage.OnUnlockedTrainingClicked(myUnit, mySkill, index, slotTransform);

        // UI 갱신 (여기서 하이라이트가 켜짐)
        RefreshTrainingVisuals();
    }
    private void RefreshTrainingVisuals()
    {
        if (TrainingDB.Instance == null) return;

        // 현재 저장된 루트 인덱스 가져오기 (-1: 없음, 0~2: 해당 루트)
        int currentRoute = TrainingDB.Instance.GetRoute(myUnit, mySkill);

        foreach (var slot in trainingSlots)
        {
            // 포커스 인덱스(currentFocusedTrainingIndex)를 같이 넘김
            slot.UpdateVisualState(currentRoute, currentFocusedTrainingIndex);
        }
    }
    // 훈련 포커스를 초기화하고 비주얼을 갱신하는 함수
    public void ResetTrainingFocus()
    {
        currentFocusedTrainingIndex = -1;
        RefreshTrainingVisuals(); // 이러면 인덱스가 -1이 되어서 모든 훈련 하이라이트가 꺼짐
    }

    // 리스트 갱신 후, 특정 인덱스의 훈련 슬롯 위치(Transform)를 반환하는 함수
    public Transform GetTrainingSlotTransform(int index)
    {
        if (trainingSlots != null && index >= 0 && index < trainingSlots.Count)
        {
            return trainingSlots[index].transform;
        }
        // 예외 처리: 못 찾으면 그냥 스킬 슬롯 자체 리턴
        return this.transform;
    }
}