using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static StatusController;

public class UnitStatusItemUI : MonoBehaviour
{
    [Header("Widgets (있는 것만 연결하면 됨)")]
    public Text nameText;
    public Slider hpBar;
    public Text hpText;         // 현재 HP 숫자 표시
    public Slider mpBar;        // 아군용: 없으면 비워두기
    public Text mpText;         // 현재 MP 숫자 표시
    public Slider rageBar;      // 아군용: 분노 게이지(없으면 비워두기)
    public Text rageText;         // 현재 Rage 숫자 표시
    public Text skillNameText;  // 적 전용(적 카드에만 배치)


    [SerializeField] private Transform chipRoot;                 // 공용 칩 루트 (오른쪽 정렬)
    [SerializeField] private HorizontalLayoutGroup chipLayout;   // ChipRoot에 붙은 LayoutGroup

    [Header("Visual DB (상태 버프)")]
    [SerializeField] private UnitStateVisualDB visualDB;
    [SerializeField] private GameObject stateChipPrefab;
    [SerializeField] private Sprite defaultIcon;           // 매핑 없을 때 대체

    [Header("Stackable Status (중첩 버프)")]
    [SerializeField] private StackableStatusVisualDB stackVisualDB; 
    [SerializeField] private GameObject stackChipPrefab;// 아이콘 칩 프리팹(둔화에서 쓰던 것)
    [SerializeField] private Sprite defaultDebuffIcon;

    [Header("Highlight (둘 중 하나/둘 다 가능)")]
    public Image highlightOverlay;                 // 예: 전체를 덮는 Image
    public Sprite defaultHighlightSprite;          // 기본 하이라이트 스프라이트
    public List<Graphic> tintTargets = new List<Graphic>(); //오버레이가 없을 때 색상으로 강조하고 싶은 Graphic들
    public Color highlightTint = new Color(255f / 255f, 173f / 255f, 122f / 255f, 1f); // 밝은 노란 톤

    [Header("Death Style")]
    //public Color deadTint = new Color(0.6f, 0.6f, 0.6f, 1f);   // 회색 강조
    [Range(0f, 1f)] public float deadNameAlpha = 0.4f; // 죽었을 때 이름 투명도
    public Sprite deadHighlightSprite;                         // (선택) 사망 오버레이
    Color _nameOrigColor;
    bool _nameColorCached;

    Color[] _originalColors;
    bool _highlighted;
    bool _isDead;
    Sprite _pendingOverlaySprite;         // SetHighlighted에서 전달받아 보관

    BattleUnit Battleunit;
    StatusController StatusController;

    // 패널에서 주입 가능하도록 열어둠
    public void SetVisualDB(UnitStateVisualDB db) => visualDB = db;
    public void SetStackVisualDB(StackableStatusVisualDB db) => stackVisualDB = db;

    private void Awake()
    {
        CacheOriginalColors();
        if (highlightOverlay) highlightOverlay.enabled = false;
    }

    public void Bind(BattleUnit u)
    {
        Battleunit = u;

        if (nameText)
        {
            nameText.text = u ? u.name : "-";

            if (!_nameColorCached)
            {
                _nameOrigColor = nameText.color;
                _nameColorCached = true;
            }
            ApplyNameAlpha(); // 현재 dead 상태에 맞춰 이름 알파 적용
        }

        if (hpBar)
        {
            hpBar.minValue = 0;
            hpBar.maxValue = Mathf.Max(1, u.MaxHP);
            hpBar.value = u.HP;
        }

        if (mpBar)
        {
            mpBar.minValue = 0;
            mpBar.maxValue = Mathf.Max(1, u.MaxMP);
            mpBar.value = u.MP;
        }

        if (rageBar)
        {
            rageBar.minValue = 0;
            rageBar.maxValue = Mathf.Max(1, u.MaxRage);
            rageBar.value = u.Rage;
        }

        if (hpText) hpText.text = (u != null ? u.HP : 0).ToString();  // 현재 HP 텍스트 세팅
        if (mpText) mpText.text = (u != null ? u.MP : 0).ToString();  // 현재 MP 텍스트 세팅
        if (rageText) rageText.text = (u != null ? u.Rage : 0).ToString();  // 현재 Rage 텍스트 세팅

        StatusController = u ? u.GetComponent<StatusController>() : null;
        if (Battleunit != null) Battleunit.OnDied += HandleUnitDied;
    }

    void OnDestroy()
    {
        if (Battleunit != null) Battleunit.OnDied -= HandleUnitDied;
    }
    void HandleUnitDied(BattleUnit dead)
    {
        ClearChildren(chipRoot);
        if (chipRoot) chipRoot.gameObject.SetActive(false);
    }
    static void ClearChildren(Transform root)
    {
        if (!root) return;
        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
    }

    void Update()
    {
        if (Battleunit == null) return;

        if (hpBar) hpBar.value = Battleunit.HP;

        if (mpBar)
        {
            mpBar.maxValue = Mathf.Max(1, Battleunit.MaxMP);
            mpBar.value = Battleunit.MP;
        }
        if (rageBar)
        {
            rageBar.maxValue = Mathf.Max(1, Battleunit.MaxRage);
            rageBar.value = Battleunit.Rage;
        }

        if (hpText) hpText.text = Battleunit.HP.ToString();  // 매 프레임 현재 HP 갱신
        if (mpText) mpText.text = Battleunit.MP.ToString();  // 매 프레임 현재 HP 갱신
        if (rageText) rageText.text = Battleunit.Rage.ToString();  // 매 프레임 현재 HP 갱신
    }

    void CacheOriginalColors()
    {
        if (tintTargets == null) return;
        _originalColors = new Color[tintTargets.Count];
        for (int i = 0; i < tintTargets.Count; i++)
            _originalColors[i] = tintTargets[i] ? tintTargets[i].color : Color.white;

        // tintTargets가 비어있고 카드 루트에 Image가 있다면 자동 등록(편의)
        if (tintTargets.Count == 0)
        {
            var auto = GetComponent<Image>();
            if (auto) { tintTargets.Add(auto); _originalColors = new[] { auto.color }; }
        }
    }

    static Color MulRGB(Color a, Color b) // 알파는 원본 유지
        => new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a);

    // 사망 스타일 토글 API
    public void SetDeadStyle(bool dead)
    {
        _isDead = dead;
        ApplyNameAlpha();
        ApplyVisualState();
    }

    public void SetHighlighted(bool on, Sprite overlaySprite = null)
    {
        _highlighted = on;
        _pendingOverlaySprite = overlaySprite;
        ApplyVisualState();
    }

    void ApplyNameAlpha()
    {
        if (!nameText || !_nameColorCached) return;
        var c = _nameOrigColor;
        c.a = _isDead ? deadNameAlpha : _nameOrigColor.a;
        nameText.color = c;
    }

    void ApplyVisualState()
    {
        // 1) Overlay
        if (highlightOverlay)
        {
            if (_isDead)
            {
                highlightOverlay.enabled = false; // 사망 시 오버레이 비활성
            }
            else
            {
                if (_highlighted)
                {
                    if (_pendingOverlaySprite != null) highlightOverlay.sprite = _pendingOverlaySprite;
                    else if (highlightOverlay.sprite == null && defaultHighlightSprite != null)
                        highlightOverlay.sprite = defaultHighlightSprite;
                    highlightOverlay.enabled = true;
                    highlightOverlay.color = Color.white;
                }
                else
                {
                    highlightOverlay.enabled = false;
                }
            }
        }

        // 2) Tint (사망 > 하이라이트 > 원본)
        if (tintTargets != null && tintTargets.Count > 0)
        {
            if (_originalColors == null || _originalColors.Length != tintTargets.Count)
                CacheOriginalColors();

            Color tint = _highlighted ? highlightTint : Color.white; // 죽었을 때 deadTint 대신, 하이라이트 시에만 tint 적용
            for (int i = 0; i < tintTargets.Count; i++)
            {
                var g = tintTargets[i];
                if (!g) continue;
                g.color = MulRGB(_originalColors[i], tint);
            }
        }
    }

    public void RefreshFromControllers(UnitStateController usc, StatusController sc)
    {
        var states = usc != null ? usc.GetAll() : null;            // SelfState 집합
        var stacks = sc != null ? sc.GetStatusViews() : null;     // 중첩 디버프 뷰들
        RefreshChips(states, stacks);
    }

    public void RefreshChips(IReadOnlyCollection<UnitStateId> states, IEnumerable<StatusView> stacks)
    {
        if (!chipRoot) return;

        // 0) 초기화
        for (int i = chipRoot.childCount - 1; i >= 0; i--)
            Destroy(chipRoot.GetChild(i).gameObject);

        // 1) 상태칩을 먼저 만들어 '맨 오른쪽'에 고정
        GameObject rightmostState = null;
        if (states != null && states.Count > 0)
        {
            foreach (var s in states)
            {
                var go = Instantiate(stateChipPrefab, chipRoot);
                // ─ 아이콘 세팅(기존 코드 그대로) ─
                var icon = go.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.name == "Icon");
                var sprite = visualDB ? (visualDB.GetIcon(s) ?? defaultIcon) : defaultIcon;
                var color = visualDB ? visualDB.GetColor(s) : Color.white;
                if (icon) { icon.sprite = sprite; icon.color = color; icon.enabled = (sprite != null); }
                // 상태칩은 텍스트 숨김
                var texts = go.GetComponentsInChildren<Text>(true);
                var tTurn = texts.FirstOrDefault(t => t.name == "Text_Turn");
                var tStack = texts.FirstOrDefault(t => t.name == "Text_Stack");
                if (tTurn) tTurn.gameObject.SetActive(false);
                if (tStack) tStack.gameObject.SetActive(false);

                go.transform.SetAsLastSibling();  // ★ 맨 오른쪽으로
                rightmostState = go;              // 여러 개면 마지막 것이 맨 오른쪽
            }
        }

        // 2) 중첩칩을 ‘상태칩 바로 왼쪽’부터 생성
        if (stacks != null)
        {
            // 생성 순서대로 오른쪽→왼쪽이 되려면: 오른쪽에 가까울수록 '더 먼저 생성된 것'
            // => 최신순으로 먼저 붙이고, 오래된 것이 나중에 들어가 상태칩에 더 가까워지도록 역순 삽입
            var list = stacks.ToList();
            for (int i = list.Count - 1; i >= 0; --i)   // newest → oldest 순으로 루프
            {
                var v = list[i];
                var go = Instantiate(stackChipPrefab, chipRoot);
                // ─ 아이콘/텍스트 세팅(기존 코드 그대로) ─
                var icon = go.GetComponentsInChildren<Image>(true).FirstOrDefault(ii => ii.name == "Icon");
                var tStk = go.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.name == "Text_Stack");
                var tTurn = go.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.name == "Text_Turn");

                var entry = stackVisualDB ? stackVisualDB.Get(v.id) : null;
                var sprite = entry?.icon ?? defaultDebuffIcon;
                var color = entry?.tint ?? Color.white;

                if (icon) { icon.sprite = sprite; icon.color = color; icon.enabled = (sprite != null); }
                if (tStk) { tStk.gameObject.SetActive(entry?.showStacks ?? true); if (tStk.gameObject.activeSelf) tStk.text = v.stacks.ToString(); }
                if (tTurn) { tTurn.gameObject.SetActive(entry?.showTurns ?? true); if (tTurn.gameObject.activeSelf) tTurn.text = v.remainingTurns > 0 ? v.remainingTurns + "" : "∞"; }

                if (rightmostState)
                {
                    // 상태칩 바로 왼쪽 위치로 삽입(형제 인덱스 고정)
                    int idx = rightmostState.transform.GetSiblingIndex();
                    go.transform.SetSiblingIndex(idx);
                }
                else
                {
                    // 상태칩이 없을 때는 그냥 오른쪽 정렬에서 오른쪽으로 붙음
                    go.transform.SetAsLastSibling();
                }
            }
        }

        // 3) 레이아웃 즉시 갱신
        var rt = chipRoot as RectTransform;
        if (rt)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }
    }

    // === 외부 이벤트 훅 ===
    public void SetSkillLabel(string label)
    {
        if (skillNameText) skillNameText.text = label ?? "";
    }
}
