using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UIArrowNavigator : MonoBehaviour
{
    [Header("Buttons in order (좌->우)")]
    [SerializeField] private List<Button> buttons = new List<Button>();

    [Header("Keys")]
    [SerializeField] private KeyCode leftKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode rightKey = KeyCode.RightArrow;
    [SerializeField] private KeyCode confirmKey = KeyCode.C;  // 요구사항: C 확정

    [Header("Behavior")]
    [SerializeField] private bool autoFocusOnEnable = true;
    [SerializeField] private bool loop = true;
    [Tooltip("마우스/다른 스크립트로 선택된 버튼도 하이라이트가 따라가도록 갱신")]
    [SerializeField] private bool followExternalSelection = true;

    [Header("Highlight")]
    [Tooltip("각 버튼의 자식 중 이 이름의 오브젝트만 활성화합니다")]
    [SerializeField] private string highlightChildName = "Highlight";
    [Tooltip("버튼 라벨 재바인딩 등으로 구조가 바뀌면 true로 호출하여 캐시를 갱신하세요")]
    [SerializeField] private bool autoRebuildOnEnable = true;

    [Header("Unity 기본 네비게이션(Selectable Navigation) 끄기")]
    [SerializeField] private bool disableUnitySelectableNavigation = true;

    private int index = 0;
    private readonly List<GameObject> highlightCache = new List<GameObject>();
    private GameObject lastSelectedGO; // 외부 선택 추적용

    void Awake()
    {
        if (autoRebuildOnEnable) BuildHighlightCache();
    }

    void OnEnable()
    {
        if (autoRebuildOnEnable) BuildHighlightCache();
        // 패널이 켜질 때 첫 유효 버튼으로 포커스
        index = FirstActiveIndex();
        if (autoFocusOnEnable) Focus();    // EventSystem에 선택 반영
        UpdateHighlight();                 // 하이라이트 갱신
        lastSelectedGO = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
    }

    void Update()
    {
        // 패널이 꺼져 있거나, HUD CanvasGroup이 꺼져 있으면 동작 안함
        if (!gameObject.activeInHierarchy) return;
        if (!IsInteractableByCanvasGroup(this.gameObject)) return;

        bool handledKey = false;

        // 키보드 네비게이션
        if (Input.GetKeyDown(rightKey))
        {
            index = NextIndex(+1);
            Focus();
            UpdateHighlight();
            handledKey = true;
        }
        else if (Input.GetKeyDown(leftKey))
        {
            index = NextIndex(-1);
            Focus();
            UpdateHighlight();
            handledKey = true;
        }
        else if (Input.GetKeyDown(confirmKey))
        {
            var b = GetButton(index);
            b?.onClick?.Invoke();
            handledKey = true;
        }

        // 외부 선택 추적(마우스/다른 스크립트), 이 프레임에 키를 처리했다면 건너뜀
        if (!handledKey && followExternalSelection && EventSystem.current)
        {
            var now = EventSystem.current.currentSelectedGameObject;
            if (now != lastSelectedGO && now != null)
            {
                int extIdx = IndexOfButton(now);
                if (extIdx >= 0)
                {
                    index = extIdx;
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
        BuildHighlightCache();
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
        BuildHighlightCache();
        if (disableUnitySelectableNavigation) ApplyDisableSelectableNavigation();

        index = FirstActiveIndex();
        if (refocus) { Focus(); UpdateHighlight(); }
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

        for (int step = 1; step <= n; step++)
        {
            int i = ((index + dir * step) % n + n) % n;
            if (IsUsable(buttons[i])) return i;
            if (!loop && (i < 0 || i >= n)) break;
        }
        return index;
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

    // === Highlight ===

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


    void BuildHighlightCache()
    {
        highlightCache.Clear();
        for (int i = 0; i < buttons.Count; i++)
        {
            GameObject h = null;
            var btn = buttons[i];
            if (btn != null)
            {
                var t = btn.transform.Find(highlightChildName);
                if (t) h = t.gameObject;
            }
            highlightCache.Add(h);
        }
        // 최초엔 모두 끔
        for (int i = 0; i < highlightCache.Count; i++)
        {
            var h = highlightCache[i];
            if (h && h.activeSelf) h.SetActive(false);
        }
    }

    void UpdateHighlight()
    {
        for (int i = 0; i < highlightCache.Count; i++)
        {
            var h = highlightCache[i];
            if (!h) continue;
            bool on = (i == index);
            if (h.activeSelf != on) h.SetActive(on);
        }
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

    int IndexOfButton(GameObject go)
    {
        for (int i = 0; i < buttons.Count; i++)
            if (buttons[i] && buttons[i].gameObject == go) return i;
        return -1;
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
}
