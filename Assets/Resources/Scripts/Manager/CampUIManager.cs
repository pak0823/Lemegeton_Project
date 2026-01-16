using Project.UI;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


// 헤더 종류 정의
public enum CampHeaderType
{
    None,       // 아무것도 안 띄움 (Option 등)
    Character,  // 캐릭터 선택창 (Status, Skill 등)
    Item        // 아이템 선택창 (Inventory 등)
}

[System.Serializable]
public struct CampTab
{
    public string tabName;       // 에디터 식별용 이름
    public Toggle tabToggle;     // 상단 탭 토글 버튼
    public GameObject contentPage; // 연결된 페이지 오브젝트 (Page_Status 등)
    public CampHeaderType headerType;   // 이 탭을 눌렀을 때 상단에 뭘 띄울지 결정
}

public class CampUIManager : ModalWindowBase
{
    public static CampUIManager Instance;

    [Header("UI Control")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
    [SerializeField] private bool pauseTimerWhileOpen = true;

    [Header("Tabs Configuration")]
    public List<CampTab> tabs = new List<CampTab>(); // 여기에 탭과 페이지를 등록

    [Header("Selectors Roots")]
    // 캐릭터 선택창 오브젝트
    [SerializeField] private GameObject charSelectorRoot;
    // 아이템 선택창 오브젝트 (추후 제작)
    [SerializeField] private GameObject itemSelectorRoot;

    [Header("Character Selection")]
    public UnitData selectedUnit;

    [Header("Character Selection UI")]
    // 캐릭터 토글들을 순서대로 넣어둘 리스트 (왼쪽 -> 오른쪽 순서)
    public List<Toggle> charToggles = new List<Toggle>();
    // 화살표 버튼 연결
    public Button arrowLeftButton;  // Btn_Next (왼쪽 이동)
    public Button arrowRightButton; // Btn_Prev (오른쪽 이동)

    [Header("Drag & Drop Visuals")]
    [SerializeField] private Image dragGhostImage; // 마우스 따라다닐 투명 이미지

    [Header("UI Pages References")]
    public CampStatusPage statusPage;

    // 현재 선택된 캐릭터 인덱스 (0 ~ N)
    private int currentCharIndex = 0;

    private bool isOpen = false;

    protected override void Awake()
    {
        base.Awake();

        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 탭 토글 이벤트 연결
        InitializeTabs();

        // 캐릭터 선택 UI 초기화
        InitializeCharacterSelector();

        // 고스트 이미지는 평소에 꺼둠 + 레이캐스트 차단
        if (dragGhostImage)
        {
            dragGhostImage.gameObject.SetActive(false);
            dragGhostImage.raycastTarget = false; // 이게 꺼져 있어야 Drop 이벤트가 아래 슬롯에 전달됨
        }
    }
    private void Start()
    {
        // Awake만으로는 다른 컴포넌트 초기화 순서에 밀릴 수 있으므로 Start에서 확실하게 처리
        if (root == null) root = GetComponent<CanvasGroup>();
        Hide();
    }

    private void Update()
    {
        // 키 입력으로 열고 닫기
        if (Input.GetKeyDown(toggleKey))
        {
            Toggle();
        }
    }

    // --- 드래그 지원 함수들 (FormationSlotUI에서 호출) ---

    public void StartDrag(Sprite sprite)
    {
        if (dragGhostImage == null) return;

        dragGhostImage.sprite = sprite;
        dragGhostImage.gameObject.SetActive(true);
        // 맨 위로 올려서 다른 UI에 안 가려지게 함
        dragGhostImage.transform.SetAsLastSibling();
    }

    public void UpdateDragPosition(Vector2 position)
    {
        if (dragGhostImage == null) return;
        dragGhostImage.transform.position = position;
    }

    public void EndDrag()
    {
        if (dragGhostImage == null) return;
        dragGhostImage.gameObject.SetActive(false);
    }

    // 캐릭터 선택 로직
    private void InitializeCharacterSelector()
    {
        // 화살표 버튼 이벤트 연결
        if (arrowLeftButton) arrowLeftButton.onClick.AddListener(SelectLeftCharacter);
        if (arrowRightButton) arrowRightButton.onClick.AddListener(SelectRightCharacter);

        // 각 캐릭터 토글에 인덱스 갱신 로직 심기
        for (int i = 0; i < charToggles.Count; i++)
        {
            int index = i; // 클로저 문제 방지용 임시 변수
            charToggles[i].onValueChanged.AddListener((isOn) =>
            {
                if (isOn)
                {
                    currentCharIndex = index;
                    Debug.Log($"[CampUI] 캐릭터 변경됨: 인덱스 {index}");
                    // 여기에 OnSelectCharacter(해당 유닛 데이터) 호출 로직 넣으면 됨
                }
            });
        }
    }

    // 왼쪽 화살표 기능
    public void SelectLeftCharacter()
    {
        // 맨 왼쪽(0)이면 더 이상 안 감
        if (currentCharIndex > 0)
        {
            // 인덱스 줄이고 해당 토글을 켠다 (그러면 위 리스너가 반응해서 로직 돌아감)
            charToggles[currentCharIndex - 1].isOn = true;
        }
    }

    // 오른쪽 화살표 기능
    public void SelectRightCharacter()
    {
        // 맨 오른쪽(Count - 1)이면 더 이상 안 감
        if (currentCharIndex < charToggles.Count - 1)
        {
            charToggles[currentCharIndex + 1].isOn = true;
        }
    }

    // --- 탭 초기화 및 이벤트 등록 ---
    private void InitializeTabs()
    {
        foreach (var tab in tabs)
        {
            if (tab.tabToggle != null)
            {
                // 토글 값이 바뀔 때(클릭될 때) 실행될 함수 연결
                tab.tabToggle.onValueChanged.AddListener((isOn) =>
                {
                    if (isOn) OnTabSelected(tab);
                });
            }
        }
    }

    // 탭 선택 시 실행되는 로직
    private void OnTabSelected(CampTab activeTab)
    {
        // 페이지 교체
        foreach (var tab in tabs)
        {
            if (tab.contentPage != null)
            {
                bool isActive = (tab.contentPage == activeTab.contentPage);
                if (tab.contentPage.activeSelf != isActive)
                    tab.contentPage.SetActive(isActive);
            }
        }

        // 헤더 교체
        UpdateHeader(activeTab.headerType);
    }

    public override void Show()
    {
        base.Show();

        if (!isOpen)
        {
            // 플레이어 이동 잠금
            if (pauseTimerWhileOpen)
                Shared.PlayerMovement?.LockMovementIndefinite();

            // 열릴 때 무조건 첫 번째 탭으로 초기화
            ResetToFirstTab();

            // 창 열릴 때 첫 번째 캐릭터(또는 기억된 캐릭터) 자동 선택
            if (charToggles.Count > 0)
            {
                // 강제로 0번을 찍음, 기존 선택 유지하려면 로직 추가
                charToggles[0].isOn = true;
            }
        }

        isOpen = true;
    }

    public override void Hide()
    {
        base.Hide();

        if (isOpen)
        {
            // 이동 잠금 해제
            if (pauseTimerWhileOpen)
                Shared.PlayerMovement?.UnlockMovementIndefinite();
        }

        isOpen = false;
    }

    // 탭 강제 초기화 함수
    private void ResetToFirstTab()
    {
        if (tabs.Count == 0) return;

        // 첫 번째 페이지를 보여줌
        // 토글 이벤트에 의존하지 않고 직접 페이지를 켬
        OnTabSelected(tabs[0]);

        // 토글 버튼 비주얼 동기화
        // SetIsOnWithoutNotify를 써서 이벤트 루프(무한 호출이나 씹힘 현상)를 방지
        for (int i = 0; i < tabs.Count; i++)
        {
            if (tabs[i].tabToggle != null)
            {
                bool shouldBeOn = (i == 0);
                tabs[i].tabToggle.SetIsOnWithoutNotify(shouldBeOn);
            }
        }
    }

    // 헤더 교체 함수
    private void UpdateHeader(CampHeaderType type)
    {
        // 일단 다 끈다
        if (charSelectorRoot) charSelectorRoot.SetActive(false);
        if (itemSelectorRoot) itemSelectorRoot.SetActive(false);

        // 필요한 놈만 켠다
        switch (type)
        {
            case CampHeaderType.Character:
                if (charSelectorRoot) charSelectorRoot.SetActive(true);
                break;
            case CampHeaderType.Item:
                if (itemSelectorRoot) itemSelectorRoot.SetActive(true);
                break;
            case CampHeaderType.None:
                // 아무것도 안 켜면 됨
                break;
        }
    }

    // 캐릭터 선택 로직 
    public void OnSelectCharacter(UnitData unit)
    {
        selectedUnit = unit;
        Debug.Log($"배치 모드: {unit.DisplayName} 선택됨");

        // 상태창 정보 갱신
        if (statusPage != null && statusPage.gameObject.activeInHierarchy)
        {
            statusPage.RefreshUI();
        }
    }
}