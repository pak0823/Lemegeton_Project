using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UIArrowNavigator : MonoBehaviour
{
    public enum NavAxis { Horizontal, Vertical }

    [Header("Buttons in order")]
    [SerializeField] private List<Button> buttons = new List<Button>();

    [Header("Navigation Axis")]
    [SerializeField] private NavAxis navAxis = NavAxis.Horizontal; // 패널별로 설정

    [Header("Keys")]
    private KeyCode UpKey = KeyCode.W;
    private KeyCode DownKey = KeyCode.S;
    private KeyCode LeftKey = KeyCode.A;
    private KeyCode RightKey = KeyCode.D;
    private KeyCode confirmKey = KeyCode.E;  // 확정키

    [Header("Behavior")]
    [SerializeField] private bool autoFocusOnEnable = true;
    [Tooltip("마우스/다른 스크립트로 선택된 버튼도 하이라이트가 따라가도록 갱신")]
    [SerializeField] private bool followExternalSelection = true;

    [Header("Unity 기본 네비게이션(Selectable Navigation) 끄기")]
    [SerializeField] private bool disableUnitySelectableNavigation = true;

    [Header("Lock Settings")]
    [SerializeField] private bool lockAfterConfirm = true;   // 확정(버튼 onClick) 직후 잠금
    [SerializeField] private bool lockWhileTargeting = true; // 전투가 타겟팅/프리뷰 상태면 잠금
    [SerializeField] private BattleManager battle;           // (SkillPanel 쪽만) 인스펙터에 할당
    private bool navLocked = false;

    // 텍스트 색상 하이라이트
    [Header("Text Color Highlight")]
    [SerializeField] private Color32 focusOrHoverColor = new Color32(255, 155, 0, 255);

    private int index = 0;
    private readonly List<GameObject> highlightCache = new List<GameObject>();
    private GameObject lastSelectedGO; // 외부 선택 추적용

    // 버튼별 라벨 및 원본 색상 캐시
    private readonly List<Text> labelCache = new List<Text>();
    private readonly List<Color> originalColors = new List<Color>();
    private bool originalCaptured = false;   // 최초 1회만 원본색 저장

    void Awake()
    {
        BuildLabelCache(captureOriginal: true);
        if (disableUnitySelectableNavigation) ApplyDisableSelectableNavigation();
        if (Shared.UIArrowNavigator == null) Shared.UIArrowNavigator = this; // 기존 동작 유지  :contentReference[oaicite:0]{index=0}
    }

    void OnEnable()
    {
        BuildLabelCache(captureOriginal: false);
        if (disableUnitySelectableNavigation) ApplyDisableSelectableNavigation();
        navLocked = false;

        index = FirstActiveIndex();
        if (autoFocusOnEnable) Focus();
        UpdateHighlight(); // 이제는 "텍스트 색"을 갱신하도록 동작  :contentReference[oaicite:1]{index=1}

        lastSelectedGO = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        battle = Shared.BattleManager;
    }

    void Update()
    {
        // 패널이 꺼져 있거나, HUD CanvasGroup이 꺼져 있으면 동작 안함
        if (!gameObject.activeInHierarchy) return;
        if (!IsInteractableByCanvasGroup(this.gameObject)) return;
        if (PopupManager.IsModalOpen) return;

        // 스킬 타겟팅 중 잠금(스킬 패널일 때 유효)
        if (lockWhileTargeting && battle != null)
        {
            if(battle.IsTargeting) navLocked = true;
            else if (navLocked) { navLocked = false; UpdateHighlight(); }// 타깃팅이 끝났다면(= 취소/확정 이후 상태 복귀) 잠금 해제
        }

        // 잠금 상태에서는 '마우스 외부 선택 추적'과 '좌/우/확정' 모두 무시
        if (navLocked) return;

        bool handledKey = false;

        // 이동 키 분기: Panel_Action(Horizontal) / Panel_Skill(Vertical)
        if (navAxis == NavAxis.Horizontal)
        {
            if (Input.GetKeyDown(RightKey)) { index = NextIndex(+1); Focus(); UpdateHighlight(); handledKey = true; }
            else if (Input.GetKeyDown(LeftKey)) { index = NextIndex(-1); Focus(); UpdateHighlight(); handledKey = true; }
        }
        else // Vertical
        {
            if (Input.GetKeyDown(DownKey)) { index = NextIndex(+1); Focus(); UpdateHighlight(); handledKey = true; }
            else if (Input.GetKeyDown(UpKey)) { index = NextIndex(-1); Focus(); UpdateHighlight(); handledKey = true; }
        }

        // 확정(E)  현재 버튼 onClick (취소는 BattleInput에서 처리 계속)
        if (Input.GetKeyDown(confirmKey))
        {
            var b = GetButton(index);
            if (lockAfterConfirm) navLocked = true;
            b?.onClick?.Invoke();
            handledKey = true;
        }

        // 외부 선택 추적(마우스/다른 스크립트), 이 프레임에 키를 처리했다면 건너뜀
        if (!handledKey && followExternalSelection && EventSystem.current)
        {
            var now = EventSystem.current.currentSelectedGameObject;
            if (now != lastSelectedGO && now != null)
            {
                // 클릭된 오브젝트가 버튼의 자식이어도 부모 쪽으로 올라가며 Button을 찾는다
                int extIdx = IndexOfButtonDeep(now);
                if (extIdx >= 0)
                {
                    index = extIdx;
                    Focus();            // (선택) EventSystem에도 반영하고 싶으면 유지
                    UpdateHighlight();
                }
            }
            lastSelectedGO = now;
        }
        else
        {
            // 키로 선택을 바꿨다면 마지막 선택 캐시도 갱신
            lastSelectedGO = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        }
    }

    // === Public helpers ===

    // 버튼 배열이 런타임에 바뀐 뒤(예: 스킬 목록 재구성) 반드시 호출.
    public void RebuildAndRefocus(bool keepCurrentIfPossible = true)
    {
        int prevIdx = index;
        BuildLabelCache(captureOriginal: true);
        if (disableUnitySelectableNavigation) ApplyDisableSelectableNavigation();
        if (!keepCurrentIfPossible || !IsUsable(GetButton(prevIdx)))
            index = FirstActiveIndex();
        Focus();
        UpdateHighlight();
    }

    // 외부에서 버튼 리스트를 교체하려면 이 메서드 사용.
    public void SetButtons(List<Button> newButtons, bool refocus = true)
    {
        buttons = newButtons ?? new List<Button>();
        BuildLabelCache(captureOriginal: true);
        if (disableUnitySelectableNavigation) ApplyDisableSelectableNavigation();
        index = FirstActiveIndex();
        if (refocus) { Focus(); UpdateHighlight(); }
    }
    public void SelectIndexImmediate(int i, bool focus = true, bool updateHighlight = true)
    {
        if (i < 0 || i >= buttons.Count) return;
        index = i;
        if (focus) Focus();
        if (updateHighlight) UpdateHighlight();
    }
    // 마우스/외부 포커스 진입 (Relay에서 호출)  :contentReference[oaicite:3]{index=3}
    public void SetExternalFocus(Button b, bool alsoSetEventSystem = false)
    {
        if (!b) return;
        int i = IndexOfButtonDeep(b.gameObject);
        if (i < 0) return;

        index = i;      // 포커스 이동
        if (alsoSetEventSystem)
        {
            Focus();
        }
        UpdateHighlight();
    }

    // === Core ===

    int FirstActiveIndex()
    {
        for (int i = 0; i < buttons.Count; i++)
            if (IsUsable(buttons[i])) return i;
        return 0;
    }

    int NextIndex(int dir)
    {
        int n = buttons.Count;
        if (n == 0) return 0;

        // 감싸지 않고 경계에서 멈춤
        int i = index + dir;
        while (i >= 0 && i < n)
        {
            if (IsUsable(buttons[i])) return i;
            i += dir; // 다음 유효 버튼까지 직선 탐색
        }
        return index; // 더 이상 갈 곳 없으면 제자리
    }

    void Focus()
    {
        var b = GetButton(index);
        if (b == null) return;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(b.gameObject);
        else
            b.Select();
    }
    void ApplyDisableSelectableNavigation()
    {
        foreach (var btn in buttons)
        {
            if (!btn) continue;
            var nav = btn.navigation;
            nav.mode = Navigation.Mode.None; // Unity 기본 네비 비활성화
            btn.navigation = nav;
        }
    }


    void BuildLabelCache(bool captureOriginal)
    {
        labelCache.Clear();
        foreach (var btn in buttons)
        {
            Text t = null;
            if (btn != null) t = btn.GetComponentInChildren<Text>(true);
            labelCache.Add(t);
        }

        // 원본색은 "최초 1회"만 캡처
        if (captureOriginal && !originalCaptured)
        {
            originalColors.Clear();
            foreach (var t in labelCache)
                originalColors.Add(t ? t.color : Color.white);
            originalCaptured = true;
        }

        // 패널 재활성화 시 한 번 전체를 원본으로 복구
        for (int i = 0; i < labelCache.Count && i < originalColors.Count; i++)
            if (labelCache[i]) labelCache[i].color = originalColors[i];
    }

    void UpdateHighlight()
    {
        for (int i = 0; i < labelCache.Count && i < originalColors.Count; i++)
            if (labelCache[i]) labelCache[i].color = originalColors[i];

        if (index >= 0 && index < labelCache.Count && labelCache[index])
            labelCache[index].color = focusOrHoverColor;
    }

    // === Utils ===

    Button GetButton(int i)
    {
        if (i < 0 || i >= buttons.Count) return null;
        return buttons[i];
    }

    bool IsUsable(Button b)
    {
        return b != null && b.gameObject.activeInHierarchy && b.interactable;
    }

    static bool IsInteractableByCanvasGroup(GameObject go)
    {
        var groups = go.GetComponentsInParent<CanvasGroup>(true);
        foreach (var cg in groups)
        {
            if (!cg.enabled) continue;
            if (!cg.interactable || !cg.blocksRaycasts || cg.alpha <= 0f)
                return false;
        }
        return true;
    }
    int IndexOfButtonDeep(GameObject go)
    {
        if (!go) return -1;
        Transform t = go.transform;
        while (t != null)
        {
            var btn = t.GetComponent<Button>();
            if (btn != null)
            {
                for (int i = 0; i < buttons.Count; i++)
                    if (buttons[i] == btn) return i;
                return -1; // 버튼이지만 내 리스트가 아닐 때
            }
            t = t.parent;
        }
        return -1;
    }
}
