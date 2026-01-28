using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CampSkillPage : MonoBehaviour
{
    [Header("List Area")]
    [SerializeField] private Transform listContent; // Scroll View의 Content 연결
    [SerializeField] private GameObject skillSlotPrefab; // 위에서 만든 슬롯 프리팹
    [SerializeField] private GameObject subSkillSlotPrefab; // 파생 스킬용 작은 슬롯

    [Header("Description UI")]
    [SerializeField] private Text infoTitleText;   // 제목
    [SerializeField] private Text infoDescText;   // 설명

    [Header("Unlock UI")]
    [SerializeField] private GameObject unlockPanelRoot; // 재화/해금 버튼 묶음 그룹
    [SerializeField] private Text currencyText;          // "현재보유 / 필요비용" 표시 텍스트
    [SerializeField] private Button unlockButton;        // 육각형 자물쇠 버튼

    [Header("Apply UI")]
    [SerializeField] private GameObject applyPanelRoot; // 체크/X 버튼 그룹
    [SerializeField] private Button btnApply;           // 체크(V) 버튼
    [SerializeField] private Button btnCancel;          // 취소(X) 버튼

    [Header("Selection UI")]
    [SerializeField] private RectTransform selectionArrow; // 화살표 이미지
    [SerializeField] private Vector2 arrowOffset = new Vector2(-20f, 0f); // 화살표 위치 보정값
    private Transform currentArrowTarget;   // 화살표가 따라다녀야 할 타겟의 Transform을 저장

    // 생성된 슬롯들 관리용 리스트
    private List<MonoBehaviour> allSlots = new List<MonoBehaviour>();
    // 오브젝트 풀링을 위한 비활성 슬롯 스택
    private Stack<CampSkillSlot> inactiveSkillSlots = new Stack<CampSkillSlot>();
    private Stack<CampSubSkillSlot> inactiveSubSlots = new Stack<CampSubSkillSlot>();

    private CampSkillSlot currentSelectedSlot;

    // 현재 해금하려고 선택한 훈련 정보 임시 저장
    private UnitData targetUnit;
    private SkillAsset targetSkill;
    private int targetRouteIndex = -1;
    private int targetCost = 0;

    private void LateUpdate()
    {
        // 화살표가 켜져 있고, 타겟이 존재할 때만 따라다님
        if (selectionArrow != null && selectionArrow.gameObject.activeSelf && currentArrowTarget != null)
        {
            UpdateArrowPos();
        }
    }

    private void OnEnable()
    {
        // 탭이 켜질 때마다 UI 갱신
        RefreshUI();

        // 해금 버튼 리스너 연결
        if (unlockButton)
        {
            unlockButton.onClick.RemoveAllListeners();
            unlockButton.onClick.AddListener(OnUnlockButtonClicked);
        }
        // 적용/취소 버튼 리스너 연결
        if (btnApply)
        {
            btnApply.onClick.RemoveAllListeners();
            btnApply.onClick.AddListener(OnApplyButtonClicked);
        }
        if (btnCancel)
        {
            btnCancel.onClick.RemoveAllListeners();
            btnCancel.onClick.AddListener(OnCancelButtonClicked);
        }
    }

    // CampUIManager에서 호출하거나 OnEnable에서 실행
    public void RefreshUI(bool autoSelectFirst = true)
    {
        // 유닛 정보 가져오기
        if (CampUIManager.Instance == null) return;
        UnitData unit = CampUIManager.Instance.selectedUnit;

        // 기존 목록 청소
        ClearList();
        ClearDescription();

        // 해금 UI 초기화
        if (unlockPanelRoot) unlockPanelRoot.SetActive(false);
        // 적용/취소 UI도 초기화
        if (applyPanelRoot) applyPanelRoot.SetActive(false);

        if (unit == null) return;

        // 스킬 목록 생성 (UnitData에 skills 배열이 있다고 가정)
        if (unit.skills != null)
        {
            foreach (var skill in unit.skills)
            {
                if (skill == null) continue;

                // 메인 스킬 슬롯 생성 (풀링 사용)
                CampSkillSlot slot = GetSkillSlot();
                slot.Setup(unit, skill, this);
                allSlots.Add(slot);

                // 만약 이 스킬이 '상태 조건부 스킬'이라면 파생 스킬들을 하단에 추가
                if (skill is StateConditionalSkillMulti multiSkill)
                {
                    // rules 리스트를 순회하며 파생 스킬 추출
                    foreach (var rule in multiSkill.rules)
                    {
                        if (rule.skill != null)
                        {
                            CreateSubSlot(unit, rule.skill, multiSkill);
                        }
                    }

                    // 기본 스킬(Default Skill)도 파생으로 보여줄지 결정 (보통은 메인이 기본 스킬 역할이니 생략)
                }
            }
        }

        // autoSelectFirst가 true일 때만 첫 번째 슬롯 자동 선택
        if (autoSelectFirst && allSlots.Count > 0 && allSlots[0] is CampSkillSlot)
        {
            StartCoroutine(AutoSelectFirstSlot());
        }
    }
    // 파생 슬롯 생성 함수
    private void CreateSubSlot(UnitData unit, SkillAsset subSkill, SkillAsset parentSkill)
    {
        // 파생 슬롯 생성 (풀링 사용)
        CampSubSkillSlot subSlot = GetSubSkillSlot();

        if (subSlot != null)
        {
            subSlot.Setup(unit, subSkill, parentSkill, this);
            allSlots.Add(subSlot);
        }
    }

    // 스킬 슬롯 가져오기 (풀링)
    private CampSkillSlot GetSkillSlot()
    {
        CampSkillSlot slot;
        if (inactiveSkillSlots.Count > 0)
        {
            slot = inactiveSkillSlots.Pop();
            slot.gameObject.SetActive(true);
            // 순서 보장을 위해 맨 아래로 이동
            slot.transform.SetAsLastSibling();
        }
        else
        {
            GameObject go = Instantiate(skillSlotPrefab, listContent);
            slot = go.GetComponent<CampSkillSlot>();
        }
        return slot;
    }

    // 파생 스킬 슬롯 가져오기 (풀링)
    private CampSubSkillSlot GetSubSkillSlot()
    {
        CampSubSkillSlot slot;
        if (inactiveSubSlots.Count > 0)
        {
            slot = inactiveSubSlots.Pop();
            slot.gameObject.SetActive(true);
            slot.transform.SetAsLastSibling();
        }
        else
        {
            GameObject go = Instantiate(subSkillSlotPrefab, listContent);
            slot = go.GetComponent<CampSubSkillSlot>();
        }
        return slot;
    }

    // 텍스트 갱신 헬퍼 함수
    private void UpdateDescriptionUI(string title, string desc)
    {
        if (infoTitleText) infoTitleText.text = title;
        if (infoDescText) infoDescText.text = desc;
    }

    // 스킬 에셋으로 현재 생성된 슬롯 UI를 찾아내는 함수
    private CampSkillSlot FindSkillSlot(SkillAsset targetSkill)
    {
        if (targetSkill == null) return null;

        foreach (var s in allSlots)
        {
            // CampSkillSlot이면서 스킬 에셋이 일치하는 놈을 찾음
            if (s is CampSkillSlot slot && slot.GetSkill() == targetSkill)
            {
                return slot;
            }
        }
        return null;
    }

    // 슬롯이 클릭되었을 때 호출되는 함수
    public void OnSlotClicked(CampSkillSlot clickedSlot, SkillAsset skill, UnitData unit)
    {
        DeselectAllHighlights(); // 전체 끄기

        // 슬롯 하이라이트 처리
        if (currentSelectedSlot != null) currentSelectedSlot.SetSelected(false);
        currentSelectedSlot = clickedSlot;
        if (currentSelectedSlot != null) currentSelectedSlot.SetSelected(true);

        // 화살표 이동
        MoveSelectionArrow(clickedSlot.transform);

        // 설명 텍스트 결정 로직
        string titleToShow = skill.displayName;
        string descToShow = skill.description; // 기본 설명

        // DB에서 현재 이 스킬에 적용된 훈련이 있는지 확인
        int activeRoute = -1;
        if (TrainingDB.Instance != null)
        {
            activeRoute = TrainingDB.Instance.GetRoute(unit, skill);
        }

        // 적용된 훈련이 있고, 그 훈련 데이터에 '덮어쓸 설명'이 있다면 교체
        if (activeRoute != -1 && skill.trainingRoutes != null && activeRoute < skill.trainingRoutes.Length)
        {
            var route = skill.trainingRoutes[activeRoute];

            // 데이터에 overrideSkillDescription이 비어있지 않다면 그걸 사용
            if (!string.IsNullOrEmpty(route.overrideSkillDescription))
            {
                descToShow = route.overrideSkillDescription;
            }
        }

        // UI 갱신
        UpdateDescriptionUI(titleToShow, descToShow);

        // 해금/적용 UI 숨기기 (스킬 자체를 눌렀을 때는 훈련 조작 버튼 숨김)
        if (unlockPanelRoot) unlockPanelRoot.SetActive(false);
        if (applyPanelRoot) applyPanelRoot.SetActive(false);

        // 선택 정보 초기화
        targetRouteIndex = -1;
    }

    // 파생 슬롯 클릭 시 처리
    public void OnSubSlotClicked(CampSubSkillSlot clickedSlot, SkillAsset subSkill, SkillAsset parentSkill, UnitData unit)
    {
        // 하이라이트 갱신
        DeselectAllHighlights(); // 전체 끄기
        clickedSlot.SetSelected(true);

        // 화살표 이동
        MoveSelectionArrow(clickedSlot.GetTextTransform());

        // 설명 갱신 (파생 스킬 기준)
        UpdateSkillDescriptionWithTraining(subSkill, unit);

        // 적용/해금 UI 끄기 (파생 스킬 자체는 훈련 조작 불가)
        if (unlockPanelRoot) unlockPanelRoot.SetActive(false);
        if (applyPanelRoot) applyPanelRoot.SetActive(false);
    }

    private IEnumerator AutoSelectFirstSlot()
    {
        // 한 프레임 대기 (UI 레이아웃 계산이 끝날 때까지 기다림)
        yield return null;
        // 혹은 레이아웃 강제 업데이트가 필요하다면:
        // Canvas.ForceUpdateCanvases(); 

        if (allSlots.Count > 0 && allSlots[0] is CampSkillSlot firstSlot)
        {
            firstSlot.SimulateClick();
        }
    }

    // 화살표 이동 함수
    private void MoveSelectionArrow(Transform targetTransform)
    {
        if (selectionArrow == null || targetTransform == null) return;

        selectionArrow.gameObject.SetActive(true);

        // 타겟을 변수에 저장 (이제 Update에서 얘를 계속 쳐다봄)
        currentArrowTarget = targetTransform;

        // 즉시 위치 갱신
        UpdateArrowPos();
    }
    // 실제 화살표 위치 계산 로직
    private void UpdateArrowPos()
    {
        if (selectionArrow == null || currentArrowTarget == null) return;

        RectTransform targetRect = currentArrowTarget.GetComponent<RectTransform>();
        if (targetRect != null)
        {
            Vector3[] corners = new Vector3[4];
            targetRect.GetWorldCorners(corners);

            // 좌하단(0)과 좌상단(1)의 중간점
            Vector3 leftEdgeCenter = (corners[0] + corners[1]) / 2f;

            Vector3 finalPos = leftEdgeCenter;
            finalPos.x += arrowOffset.x;
            finalPos.y += arrowOffset.y;

            selectionArrow.position = finalPos;
        }
        else
        {
            selectionArrow.position = currentArrowTarget.position;
        }
    }

    // 훈련 버튼 클릭 시 (잠김/해금 공통)
    // 훈련 버튼을 누르면 그 훈련의 이름과 효과 설명을 보여준다.
    private void ShowTrainingInfo(string title, string desc)
    {
        // 이제 스킬 설명창 위치에 훈련 설명을 띄운다
        UpdateDescriptionUI(title, desc);
    }

    // 잠긴 훈련 클릭 시 호출 (CampSkillSlot -> Page)
    public void OnLockedTrainingClicked(UnitData unit, SkillAsset skill, int index, int cost, Transform slotTransform)
    {
        MoveSelectionArrow(slotTransform);

        // 훈련 설명 표시
        if (skill.trainingRoutes != null && index < skill.trainingRoutes.Length)
        {
            var route = skill.trainingRoutes[index];
            ShowTrainingInfo(route.title, route.description);
        }

        // 해금 타겟 설정
        targetUnit = unit;
        targetSkill = skill;
        targetRouteIndex = index;
        targetCost = cost;

        // 해금 UI 표시 및 갱신
        if (unlockPanelRoot) unlockPanelRoot.SetActive(true);
        if (applyPanelRoot) applyPanelRoot.SetActive(false);
        UpdateUnlockUI();
    }

    // 해금된 훈련 클릭 시 호출 (CampSkillSlot -> Page)
    public void OnUnlockedTrainingClicked(UnitData unit, SkillAsset skill, int index, Transform slotTransform)
    {
        MoveSelectionArrow(slotTransform);

        // 훈련 설명 표시
        if (skill != null && skill.trainingRoutes != null && index < skill.trainingRoutes.Length)
        {
            var route = skill.trainingRoutes[index];
            ShowTrainingInfo(route.title, route.description);
        }

        // 타겟 정보 저장
        targetUnit = unit;
        targetSkill = skill;
        targetRouteIndex = index;

        // 해금 UI는 끄고, 적용 UI를 켠다
        if (unlockPanelRoot) unlockPanelRoot.SetActive(false);
        if (applyPanelRoot) applyPanelRoot.SetActive(true);

        // 버튼 상태 갱신 (V / X)
        int currentActiveRoute = -1;
        if (TrainingDB.Instance != null)
        {
            currentActiveRoute = TrainingDB.Instance.GetRoute(unit, skill);
        }

        if (currentActiveRoute == index)
        {
            if (btnApply) btnApply.gameObject.SetActive(false);
            if (btnCancel) btnCancel.gameObject.SetActive(true);
        }
        else
        {
            if (btnApply) btnApply.gameObject.SetActive(true);
            if (btnCancel) btnCancel.gameObject.SetActive(false);
        }
    }
    // 스킬 설명 갱신 로직 (중복 제거용)
    private void UpdateSkillDescriptionWithTraining(SkillAsset skill, UnitData unit)
    {
        string title = skill.displayName;
        string desc = skill.description;

        int activeRoute = -1;
        if (TrainingDB.Instance != null) activeRoute = TrainingDB.Instance.GetRoute(unit, skill);

        if (activeRoute != -1 && skill.trainingRoutes != null && activeRoute < skill.trainingRoutes.Length)
        {
            var route = skill.trainingRoutes[activeRoute];
            if (!string.IsNullOrEmpty(route.overrideSkillDescription))
            {
                desc = route.overrideSkillDescription;
            }
        }
        UpdateDescriptionUI(title, desc);
    }
    private void UpdateUnlockUI()
    {
        if (CurrencyManager.Instance == null) return;

        int currentGold = CurrencyManager.Instance.gold;

        // 텍스트 갱신: "보유량 / 필요량" (예: 88 / 8)
        // 색상 처리: 돈 부족하면 빨간색 등
        string colorTag = currentGold >= targetCost ? "<color=#00FF00>" : "<color=#FF0000>";
        if (currencyText) currencyText.text = $"{colorTag}{currentGold}</color> / {targetCost}";

        // 버튼 활성/비활성 (돈 없으면 버튼 못 누르게 할지, 누르고 메시지 띄울지 선택)
        // if (unlockButton) unlockButton.interactable = currentGold >= targetCost;
    }

    // 자물쇠 버튼을 눌렀을 때 실제 해금 시도
    private void OnUnlockButtonClicked()
    {
        if (targetRouteIndex == -1 || CurrencyManager.Instance == null) return;

        // RefreshUI를 하면 target 변수들이 날아갈 수 있으므로 지역 변수에 백업
        int tempIndex = targetRouteIndex;
        UnitData tempUnit = targetUnit;
        SkillAsset tempSkill = targetSkill;

        // 돈 확인 및 소모
        if (CurrencyManager.Instance.Consume(targetCost))
        {
            // DB 해금
            if (TrainingDB.Instance != null)
            {
                TrainingDB.Instance.UnlockRoute(targetUnit, targetSkill, targetRouteIndex);
            }

            // UI 갱신
            RefreshUI(false);
            if (unlockPanelRoot) unlockPanelRoot.SetActive(false);

            // 코루틴으로 상태 복구
            StartCoroutine(RestoreStateCoroutine(tempUnit, tempSkill, tempIndex));
        }
        else
        {
            Debug.Log("재화 부족!");
            // 여기에 "재화가 부족합니다" 팝업 띄우기
        }
    }
    // 버튼 클릭: 훈련 적용
    private void OnApplyButtonClicked()
    {
        if (targetRouteIndex == -1 || TrainingDB.Instance == null) return;

        // 현재 값을 미리 지역 변수에 백업
        int tempIndex = targetRouteIndex;
        UnitData tempUnit = targetUnit;
        SkillAsset tempSkill = targetSkill;

        // DB에 저장
        TrainingDB.Instance.SetRoute(tempUnit, tempSkill, tempIndex);

        // UI 갱신 (이 과정에서 멤버 변수 targetRouteIndex가 -1이 됨)
        RefreshUI(false);

        StartCoroutine(RestoreStateCoroutine(tempUnit, tempSkill, tempIndex));
    }

    // [X] 버튼 클릭: 훈련 해제
    private void OnCancelButtonClicked()
    {
        if (TrainingDB.Instance == null) return;

        int tempIndex = targetRouteIndex;
        UnitData tempUnit = targetUnit;
        SkillAsset tempSkill = targetSkill;

        // DB에서 해제 (-1)
        TrainingDB.Instance.SetRoute(targetUnit, targetSkill, -1);

        // UI 갱신 (흰색으로 변함)
        RefreshUI(false);

        StartCoroutine(RestoreStateCoroutine(tempUnit, tempSkill, tempIndex));
    }
    private IEnumerator RestoreStateCoroutine(UnitData unit, SkillAsset skill, int routeIndex)
    {
        // UI 레이아웃이 정렬될 때까지 1프레임 대기
        yield return null;

        // 해당 스킬 슬롯 찾기
        CampSkillSlot slot = FindSkillSlot(skill);

        if (slot != null)
        {
            // 훈련 슬롯의 위치 찾기 (이제 레이아웃 계산이 끝나서 정확한 위치가 나옴)
            Transform tTransform = slot.GetTrainingSlotTransform(routeIndex);

            // 슬롯에게 "이 훈련이 선택되었다"고 알림
            // 이 함수 안에서 DeselectAllHighlights()가 호출되어 메인 스킬 하이라이트는 꺼지고,
            // 훈련 슬롯 하이라이트가 켜지며, 화살표도 이동함.
            slot.OnTrainingSelected(routeIndex, tTransform);
        }
    }

    public void DeselectAllHighlights()
    {
        foreach (var s in allSlots)
        {
            if (s is CampSkillSlot main)
            {
                main.SetSelected(false);       // 스킬 슬롯 본체 끄기
                main.ResetTrainingFocus();     // 자식 훈련 슬롯들 끄기
            }
            if (s is CampSubSkillSlot sub)
            {
                sub.SetSelected(false);        // 파생 스킬 끄기
            }
        }
    }
    public void ClearDescription()
    {
        UpdateDescriptionUI("", "스킬을 선택하세요.");
        if (selectionArrow)
        {
            selectionArrow.gameObject.SetActive(false);
            currentArrowTarget = null; // 타겟 해제
        }
    }

    // 리스트 초기화 (풀링 반환)
    private void ClearList()
    {
        foreach (var component in allSlots)
        {
            if (component == null) continue;

            if (component is CampSkillSlot skillSlot)
            {
                skillSlot.gameObject.SetActive(false);
                inactiveSkillSlots.Push(skillSlot);
            }
            else if (component is CampSubSkillSlot subSlot)
            {
                subSlot.gameObject.SetActive(false);
                inactiveSubSlots.Push(subSlot);
            }
            else
            {
                // 혹시 모를 다른 타입은 그냥 파괴
                Destroy(component.gameObject);
            }
        }
            allSlots.Clear();
        currentSelectedSlot = null;
    }
}