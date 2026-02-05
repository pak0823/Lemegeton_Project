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



    [Header("Visual Settings")]

    public Image tabImage;           // 탭 버튼의 배경 이미지가 있는 컴포넌트

    public Sprite normalSprite;      // 선택 안 됐을 때 (테두리 4면 다 있음)

    public Sprite selectedSprite;    // 선택 됐을 때 (하단 테두리 없음, 배경색과 일치)

}



public class CampUIManager : ModalWindowBase

{

    public static CampUIManager Instance {  get; private set; }



    // 관리 컴포넌트들

    [SerializeField] private CampTabController tabController;

    [SerializeField] private CampHeaderController headerController;

    [SerializeField] private CharacterHeaderController charHeader;

    //[SerializeField] private CraftHeaderController itemHeader;



    private CampHeaderType currentHeaderType;



    [Header("UI Control")]

    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    [SerializeField] private bool pauseTimerWhileOpen = true;



    [Header("Tabs Configuration")]

    public List<CampTab> tabs = new List<CampTab>(); // 여기에 탭과 페이지를 등록



    [Header("Character Selection")]

    public UnitData selectedUnit;



    [Header("Drag & Drop Visuals")]

    [SerializeField] private Image dragGhostImage; // 마우스 따라다닐 투명 이미지



    [Header("UI Pages References")]

    public CampStatusPage statusPage;

    public CampSkillPage skillPage;



    [Header("Databases")]

    public TrainingDB trainingDB;



    [Header("Button")]

    public Button closeCampBtn; 



    private bool isOpen = false;



    protected override void Awake()

    {

        base.Awake();



        if (Instance == null) Instance = this;

        else Destroy(gameObject);



        InitializeTabs(); // 탭 리스너 등록은 미리 해둠



        // 로딩 완료 이벤트 구독

        if (PlayerDataManager.Instance != null)

        {

            PlayerDataManager.Instance.OnUnitsLoaded += OnDataLoaded;

        }





        // 고스트 이미지는 평소에 꺼둠 + 레이캐스트 차단

        if (dragGhostImage)

        {

            dragGhostImage.gameObject.SetActive(false);

            dragGhostImage.raycastTarget = false; // 이게 꺼져 있어야 Drop 이벤트가 아래 슬롯에 전달됨

        }



        if (closeCampBtn != null) closeCampBtn.onClick.AddListener(OnClickClose);

    }

    private void Start()

    {

        // Awake만으로는 다른 컴포넌트 초기화 순서에 밀릴 수 있으므로 Start에서 확실하게 처리

        if (root == null) root = GetComponent<CanvasGroup>();

        Hide();



        if (PlayerDataManager.Instance != null && !PlayerDataManager.Instance.IsLoading)

        {

            OnDataLoaded();

        }

    }

    private void OnDataLoaded()

    {

        // 데이터가 로드된 후에야 캐릭터 헤더를 초기화함

        charHeader.Initialize((index) => {

            UnitData data = PlayerDataManager.Instance.GetOwnedUnit(index);

            if (data != null) OnSelectCharacter(data);

        });



        // 첫 번째 탭 강제 실행 (데이터가 있으니 이제 안전함)

        ResetToFirstTab();

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

        currentHeaderType = activeTab.headerType; // 현재 헤더 타입 저장



        // 페이지 스위칭

        foreach (var tab in tabs)

        {

            if (tab.contentPage != null)

                tab.contentPage.SetActive(tab.contentPage == activeTab.contentPage);

        }



        // 비주얼 위임

        tabController.UpdateTabVisuals(tabs, activeTab);



        // 헤더 위임

        headerController.UpdateHeader(activeTab.headerType);

    }



    private void OnClickClose()

    {

        Hide();

    }



    public override void Show()

    {

        base.Show();

        ResetToFirstTab();

    }



    public override void Hide()

    {

        base.Hide();



        if (isOpen)

        {

            // 이동 잠금 해제

            if (pauseTimerWhileOpen)

                PlayerMovement.Instance?.UnlockMovementIndefinite();

        }



        isOpen = false;

    }



    // 탭 강제 초기화 함수

    private void ResetToFirstTab()

    {

        if (tabs.Count > 0)

        {

            tabs[0].tabToggle.isOn = true; // 리스너가 OnTabSelected 호출함

            OnTabSelected(tabs[0]);

        }

    }



    // 캐릭터 선택 로직 

    public void OnSelectCharacter(UnitData unit)

    {

        selectedUnit = unit;



        // 상태창 정보 갱신

        if (statusPage != null && statusPage.gameObject.activeInHierarchy)

        {

            // 페이지가 직접 selectedUnit을 참조하거나, 아래처럼 명시적으로 갱신 명령

            statusPage.RefreshUI();

        }



        // 스킬창 정보 갱신

        if (skillPage != null && skillPage.gameObject.activeInHierarchy)

        {

            skillPage.RefreshUI();

        }

    }

}