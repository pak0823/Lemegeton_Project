using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CampSkillSlot : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Text nameText;
    [SerializeField] private Button skillButton;
    [SerializeField] private GameObject selectionHighlight; // 현재 선택된 스킬 표시용 테두리

    [Header("Training UI")]
    [SerializeField] private Transform trainingContainer; // 훈련 버튼들이 들어갈 부모 (Horizontal Layout)
    [SerializeField] private GameObject trainingSlotPrefab; // 위에서 만든 CampTrainingSlot 프리팹

    private SkillAsset mySkill;
    private UnitData myUnit;
    private CampSkillPage parentPage;

    private List<CampTrainingSlot> trainingSlots = new List<CampTrainingSlot>();

    // 초기화 함수
    public void Setup(UnitData unit, SkillAsset skill, CampSkillPage page)
    {
        myUnit = unit;
        mySkill = skill;
        parentPage = page;

        if (mySkill != null)
        {
            //if (mySkill.icon != null) iconImage.sprite = mySkill.icon;
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
    public void OnLockedTrainingSelected(int index, int cost)
    {
        parentPage.OnSlotClicked(this, mySkill, myUnit);

        // 페이지에게 "잠긴 훈련 클릭됨" 보고 -> 하단 UI 갱신 요청
        parentPage.OnLockedTrainingClicked(myUnit, mySkill, index, cost);
    }

    // 훈련 슬롯이 해금 됐을 때 호출
    public void OnTrainingUnlocked(int index)
    {
        // DB에 해금 사실 기록
        if (TrainingDB.Instance != null)
        {
            TrainingDB.Instance.UnlockRoute(myUnit, mySkill, index);
        }

        // UI 전체 다시 그리기 (잠금 풀린 것 반영)
        CreateTrainingSlots();
        RefreshTrainingVisuals();

        // 해금되자마자 바로 선택까지 시켜주기
        OnTrainingSelected(index);
    }

    // 훈련 버튼이 눌렸을 때 실행
    public void OnTrainingSelected(int index)
    {
        // 스킬 선택 포커스 이동 (화살표 이동 등)
        parentPage.OnSlotClicked(this, mySkill, myUnit);

        parentPage.OnUnlockedTrainingClicked(myUnit, mySkill, index);

        // 훈련 설명창 즉시 갱신
        if (mySkill != null && mySkill.trainingRoutes != null && index < mySkill.trainingRoutes.Length)
        {
            var routeInfo = mySkill.trainingRoutes[index];
            parentPage.UpdateTrainingDescription(routeInfo.title, routeInfo.description);
        }

        // UI 갱신 (색상 변경)
        RefreshTrainingVisuals();
    }
    private void RefreshTrainingVisuals()
    {
        if (TrainingDB.Instance == null) return;

        // 현재 저장된 루트 인덱스 가져오기 (-1: 없음, 0~2: 해당 루트)
        int currentRoute = TrainingDB.Instance.GetRoute(myUnit, mySkill);

        foreach (var slot in trainingSlots)
        {
            slot.UpdateVisualState(currentRoute);
        }
    }
}