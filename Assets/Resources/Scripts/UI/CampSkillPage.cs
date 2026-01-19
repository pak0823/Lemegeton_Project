using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CampSkillPage : MonoBehaviour
{
    [Header("List Area")]
    [SerializeField] private Transform listContent; // Scroll View의 Content 연결
    [SerializeField] private GameObject skillSlotPrefab; // 위에서 만든 슬롯 프리팹
    [SerializeField] private GameObject subSkillSlotPrefab; // 파생 스킬용 작은 슬롯

    [Header("Skill Description (Left)")]
    [SerializeField] private Text skillNameText;   // 스킬 제목
    [SerializeField] private Text skillDescText;   // 스킬 설명

    [Header("Training Description (Right)")]
    [SerializeField] private Text trainingNameText; // 훈련 제목
    [SerializeField] private Text trainingDescText; // 훈련 설명
    [SerializeField] private GameObject trainingPanelRoot; // 훈련 설명창 자체(꺼둘 때 용도)

    [Header("Unlock UI")]
    [SerializeField] private GameObject unlockPanelRoot; // 재화/해금 버튼 묶음 그룹
    [SerializeField] private Text currencyText;          // "현재보유 / 필요비용" 표시 텍스트
    [SerializeField] private Button unlockButton;        // 육각형 자물쇠 버튼

    [Header("Apply UI")]
    [SerializeField] private GameObject applyPanelRoot; // 체크/X 버튼 그룹
    [SerializeField] private Button btnApply;           // 체크(V) 버튼
    [SerializeField] private Button btnCancel;          // 취소(X) 버튼

    // 생성된 슬롯들 관리용 리스트
    private List<CampSkillSlot> spawnedSlots = new List<CampSkillSlot>();
    private List<MonoBehaviour> allSlots = new List<MonoBehaviour>();
    private CampSkillSlot currentSelectedSlot;

    // 현재 해금하려고 선택한 훈련 정보 임시 저장
    private UnitData targetUnit;
    private SkillAsset targetSkill;
    private int targetRouteIndex = -1;
    private int targetCost = 0;

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
    public void RefreshUI()
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

                // 메인 스킬 슬롯 생성
                GameObject mainSkill = Instantiate(skillSlotPrefab, listContent);
                CampSkillSlot slot = mainSkill.GetComponent<CampSkillSlot>();
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

        // 첫 번째 슬롯 자동 선택 (CampSkillSlot 타입인 경우만)
        if (allSlots.Count > 0 && allSlots[0] is CampSkillSlot firstSlot)
        {
            firstSlot.SimulateClick();
        }
    }
    // 파생 슬롯 생성 함수
    private void CreateSubSlot(UnitData unit, SkillAsset subSkill, SkillAsset parentSkill)
    {
        GameObject go = Instantiate(subSkillSlotPrefab, listContent);
        CampSubSkillSlot subSlot = go.GetComponent<CampSubSkillSlot>();

        if (subSlot != null)
        {
            subSlot.Setup(unit, subSkill, parentSkill, this);
            allSlots.Add(subSlot);
        }
    }

    // 슬롯이 클릭되었을 때 호출되는 함수
    public void OnSlotClicked(CampSkillSlot clickedSlot, SkillAsset skill, UnitData unit)
    {
        DeselectAllSlots(); // 다른 슬롯(파생 포함) 끄기

        // 슬롯 하이라이트 처리
        if (currentSelectedSlot != null) currentSelectedSlot.SetSelected(false);
        currentSelectedSlot = clickedSlot;
        if (currentSelectedSlot != null) currentSelectedSlot.SetSelected(true);

        // 스킬 설명창 갱신
        UpdateSkillDescription(skill);

        // 저장된 훈련 정보 표시
        int savedRoute = -1;
        if (TrainingDB.Instance != null) savedRoute = TrainingDB.Instance.GetRoute(unit, skill);

        if (savedRoute != -1 && skill.trainingRoutes != null && savedRoute < skill.trainingRoutes.Length)
        {
            var route = skill.trainingRoutes[savedRoute];
            UpdateTrainingDescription(route.title, route.description);
        }
        else
        {
            ClearTrainingDescription();
        }

        // 일반 선택 시에는 해금 UI 숨기기
        if (unlockPanelRoot) unlockPanelRoot.SetActive(false);
        // 적용/취소 UI도 같이 숨기기
        if (applyPanelRoot) applyPanelRoot.SetActive(false);

        // 선택 정보 초기화
        targetRouteIndex = -1;
    }

    // 파생 슬롯 클릭 시 처리
    public void OnSubSlotClicked(CampSubSkillSlot clickedSlot, SkillAsset subSkill, SkillAsset parentSkill, UnitData unit)
    {
        // 하이라이트 갱신
        DeselectAllSlots();
        clickedSlot.SetSelected(true);

        // 왼쪽 설명창: 파생 스킬(자식)의 설명 표시
        UpdateSkillDescription(subSkill);

        // 오른쪽 훈련창: 원본 스킬(부모)의 훈련 정보 표시 (훈련은 공유)
        // (부모 스킬의 현재 선택된 훈련 정보를 가져옴)
        if (TrainingDB.Instance != null)
        {
            int savedRoute = TrainingDB.Instance.GetRoute(unit, parentSkill);

            // 훈련 내용 갱신
            if (parentSkill.trainingRoutes != null && savedRoute != -1 && savedRoute < parentSkill.trainingRoutes.Length)
            {
                var route = parentSkill.trainingRoutes[savedRoute];
                UpdateTrainingDescription(route.title, route.description);
            }
            else
            {
                // 선택된 훈련이 없으면 기본 메시지 혹은 부모 스킬의 기본 훈련 설명
                ClearTrainingDescription();
            }
        }

        // 적용/해금 UI 끄기 (파생 스킬 자체는 훈련 조작 불가)
        if (unlockPanelRoot) unlockPanelRoot.SetActive(false);
        if (applyPanelRoot) applyPanelRoot.SetActive(false);
    }

    // 잠긴 훈련 클릭 시 호출 (CampSkillSlot -> Page)
    public void OnLockedTrainingClicked(UnitData unit, SkillAsset skill, int index, int cost)
    {
        // 설명창 업데이트 (잠겨있어도 무슨 훈련인지는 보여줌)
        if (skill.trainingRoutes != null && index < skill.trainingRoutes.Length)
        {
            var route = skill.trainingRoutes[index];
            UpdateTrainingDescription(route.title, route.description);
        }

        // 해금 타겟 설정
        targetUnit = unit;
        targetSkill = skill;
        targetRouteIndex = index;
        targetCost = cost;

        // 해금 UI 표시 및 갱신
        if (unlockPanelRoot) unlockPanelRoot.SetActive(true);
        UpdateUnlockUI();
    }

    // 해금된 훈련 클릭 시 호출 (CampSkillSlot -> Page)
    public void OnUnlockedTrainingClicked(UnitData unit, SkillAsset skill, int index)
    {
        // 인덱스 유효성 검사 (음수이거나 배열 범위 밖이면 중단)
        if (skill == null || skill.trainingRoutes == null || index < 0 || index >= skill.trainingRoutes.Length)
        {
            return;
        }

        // 타겟 정보 저장
        targetUnit = unit;
        targetSkill = skill;
        targetRouteIndex = index;

        // 설명창 갱신
        if (skill.trainingRoutes != null && index < skill.trainingRoutes.Length)
        {
            var route = skill.trainingRoutes[index];
            UpdateTrainingDescription(route.title, route.description);
        }

        // UI 상태 결정
        int currentActiveRoute = -1;
        if (TrainingDB.Instance != null)
        {
            currentActiveRoute = TrainingDB.Instance.GetRoute(unit, skill);
        }

        // 해금 UI는 끄고, 적용 UI를 켠다
        if (unlockPanelRoot) unlockPanelRoot.SetActive(false);
        if (applyPanelRoot) applyPanelRoot.SetActive(true);

        // 버튼 분기 처리
        if (currentActiveRoute == index)
        {
            // 이미 적용 중인 훈련을 클릭함 -> "취소(X)" 버튼 활성화
            if (btnApply) btnApply.gameObject.SetActive(false);
            if (btnCancel) btnCancel.gameObject.SetActive(true);
        }
        else
        {
            // 다른 훈련을 클릭함 -> "적용(V)" 버튼 활성화
            if (btnApply) btnApply.gameObject.SetActive(true);
            if (btnCancel) btnCancel.gameObject.SetActive(false);
        }
    }

    public void UpdateTrainingDescription(string title, string desc)
    {
        if (trainingPanelRoot) trainingPanelRoot.SetActive(true);
        if (trainingNameText) trainingNameText.text = title;
        if (trainingDescText) trainingDescText.text = desc;
    }

    private void UpdateSkillDescription(SkillAsset skill)
    {
        if (skill == null) return;
        if (skillNameText) skillNameText.text = skill.displayName; // 혹은 skill.name
        if (skillDescText) skillDescText.text = skill.description;
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

        // 돈 확인 및 소모
        if (CurrencyManager.Instance.Consume(targetCost))
        {
            // DB 해금
            if (TrainingDB.Instance != null)
            {
                TrainingDB.Instance.UnlockRoute(targetUnit, targetSkill, targetRouteIndex);
            }

            // UI 갱신 (전체 다시 그리기 - 가장 확실함)
            RefreshUI();

            if (unlockPanelRoot) unlockPanelRoot.SetActive(false);
        }
        else
        {
            Debug.Log("재화 부족!");
            // 여기에 "재화가 부족합니다" 팝업 띄우기
        }
    }
    // [V] 버튼 클릭: 훈련 적용
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
        RefreshUI();

        // 적용 후 UI 상태 복구
        OnUnlockedTrainingClicked(tempUnit, tempSkill, tempIndex);
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
        RefreshUI();

        // 해제 후에는 V버튼 상태로 갱신해서 남겨둠 (다시 선택 가능하게)
        OnUnlockedTrainingClicked(tempUnit, tempSkill, tempIndex);
    }

    public void ClearDescription()
    {
        if (skillNameText) skillNameText.text = "";
        if (skillDescText) skillDescText.text = "스킬을 선택하세요.";

        ClearTrainingDescription();
    }
    private void ClearTrainingDescription()
    {
        // 훈련 설명창을 아예 끄거나 텍스트만 비움
        // if (trainingPanelRoot) trainingPanelRoot.SetActive(false); 
        if (trainingNameText) trainingNameText.text = "";
        if (trainingDescText) trainingDescText.text = "선택된 훈련이 없습니다.";
    }

    private void DeselectAllSlots()
    {
        foreach (var s in allSlots)
        {
            if (s is CampSkillSlot main) main.SetSelected(false);
            if (s is CampSubSkillSlot sub) sub.SetSelected(false);
        }
    }

    private void ClearList()
    {
        foreach (var slot in allSlots)
        {
            if (slot != null) Destroy(slot.gameObject);
        }
        allSlots.Clear();
        currentSelectedSlot = null;
    }
}