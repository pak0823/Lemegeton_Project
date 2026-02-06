using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

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

    [Header("UnitState Buff (버프)")]
    [SerializeField] private UnitStateBuffVisualDB buffVisualDB;
    [SerializeField] private GameObject buffChipPrefab;     // 없으면 stateChipPrefab 재사용해도 됨
    [SerializeField] private Sprite defaultBuffIcon;

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

    // Refactored to use interface
    private IUnitStatus _targetUnit;

    // 패널에서 주입 가능하도록 열어둠
    public void SetVisualDB(UnitStateVisualDB db) => visualDB = db;
    public void SetStackVisualDB(StackableStatusVisualDB db) => stackVisualDB = db;

    void Unsubscribe()
    {
        if (_targetUnit != null)
        {
            _targetUnit.OnStatusChanged -= HandleStatusChanged; // 통합 이벤트
            _targetUnit.OnDead -= HandleUnitDied; // 통합 이벤트
        }
    }

    private void Awake()
    {
        CacheOriginalColors();
        if (highlightOverlay) highlightOverlay.enabled = false;
    }

    void OnDisable()
    {
        Unsubscribe();
    }
    void OnDestroy()
    {
        Unsubscribe();
    }

    // 하위 호환성 유지 (전투용)
    public void Bind(BattleUnit u)
    {
        if (u == null)
        {
            Bind((IUnitStatus)null);
            return;
        }
        Bind(new BattleUnitAdapter(u));
    }

    // 신규 (인터페이스 주입)
    public void Bind(IUnitStatus unit)
    {
        // 이전 유닛과의 구독 해제
        Unsubscribe();

        _targetUnit = unit;

        if (_targetUnit == null) return;

        if (nameText)
        {
            nameText.text = _targetUnit.Name;

            if (!_nameColorCached)
            {
                _nameOrigColor = nameText.color;
                _nameColorCached = true;
            }
            ApplyNameAlpha(); 
        }

        UpdateBars(); // 초기 값 설정

        // 상태 및 사망 상태 동기화
        _isDead = _targetUnit.IsDead;
        SetDeadStyle(_isDead); // 초기 사망 스타일

        // 이벤트 구독
        _targetUnit.OnStatusChanged += HandleStatusChanged;
        _targetUnit.OnDead += HandleUnitDied;

        // 현재 상태/버프/스택으로 아이콘 한 번 갱신
        RefreshChips();
    }

    void HandleUnitDied(bool isDead)
    {
        _isDead = isDead;
        SetDeadStyle(_isDead);

        if (isDead)
        {
            ClearChildren(chipRoot);
            if (chipRoot) chipRoot.gameObject.SetActive(false);
        }
    }

    void HandleStatusChanged()
    {
        RefreshChips();
    }

    static void ClearChildren(Transform root)
    {
        if (!root) return;
        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
    }

    void Update()
    {
        if (_targetUnit == null) return;
        UpdateBars();
        UpdateTexts();
    }

    void UpdateBars()
    {
        if (hpBar)
        {
            hpBar.minValue = 0;
            hpBar.maxValue = Mathf.Max(1, _targetUnit.MaxHP);
            hpBar.value = _targetUnit.HP;
        }

        if (mpBar)
        {
            mpBar.minValue = 0;
            mpBar.maxValue = Mathf.Max(1, _targetUnit.MaxMP);
            mpBar.value = _targetUnit.MP;
        }

        if (rageBar)
        {
            rageBar.minValue = 0;
            rageBar.maxValue = Mathf.Max(1, _targetUnit.MaxRage);
            rageBar.value = _targetUnit.Rage;
        }
    }

    void UpdateTexts()
    {
        if (hpText) hpText.text = _targetUnit.HP.ToString();
        if (mpText) mpText.text = _targetUnit.MP.ToString();
        if (rageText) rageText.text = _targetUnit.Rage.ToString();
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

    public void RefreshChips()
    {
        if (_targetUnit == null) return;
        if (!chipRoot) return;

        var states = _targetUnit.GetStates();
        var buffs = _targetUnit.GetBuffs();
        var stacks = _targetUnit.GetStacks();

        // 0) 초기화
        for (int i = chipRoot.childCount - 1; i >= 0; i--)
            Destroy(chipRoot.GetChild(i).gameObject);

        // 1) UnitStateId 상태칩 (오른쪽)
        GameObject rightmostState = null;
        if (states != null && states.Count > 0)
        {
            foreach (var s in states)
            {
                var go = Instantiate(stateChipPrefab, chipRoot);
                var icon = go.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.name == "Icon");
                var sprite = visualDB ? (visualDB.GetIcon(s) ?? defaultIcon) : defaultIcon;
                var color = visualDB ? visualDB.GetColor(s) : Color.white;
                if (icon)
                {
                    icon.sprite = sprite;
                    icon.color = color;
                    icon.enabled = (sprite != null);
                }

                // 상태칩은 텍스트 숨김
                var texts = go.GetComponentsInChildren<Text>(true);
                var tTurn = texts.FirstOrDefault(t => t.name == "Text_Turn");
                var tStack = texts.FirstOrDefault(t => t.name == "Text_Stack");
                if (tTurn) tTurn.gameObject.SetActive(false);
                if (tStack) tStack.gameObject.SetActive(false);

                go.transform.SetAsLastSibling();
                rightmostState = go;
            }
        }

        // 2) UnitStateBuffId 버프칩 (상태 바로 왼쪽)
        if (buffs != null)
        {
            foreach (var v in buffs)
            {
                var go = Instantiate(buffChipPrefab ? buffChipPrefab : stateChipPrefab, chipRoot);
                var icon = go.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.name == "Icon");

                Sprite sprite = defaultBuffIcon;
                Color color = Color.white;
                bool showTurns = true;

                if (buffVisualDB != null)
                {
                    sprite = buffVisualDB.GetIcon(v.id) ?? defaultBuffIcon;
                    color = buffVisualDB.GetColor(v.id);
                    showTurns = buffVisualDB.GetShowTurns(v.id);
                }

                if (icon)
                {
                    icon.sprite = sprite;
                    icon.color = color;
                    icon.enabled = (sprite != null);
                }

                var texts = go.GetComponentsInChildren<Text>(true);
                var tTurn = texts.FirstOrDefault(t => t.name == "Text_Turn");
                var tStack = texts.FirstOrDefault(t => t.name == "Text_Stack");

                if (tStack) tStack.gameObject.SetActive(false);           // 버프는 스택 없음
                if (tTurn)
                {
                    tTurn.gameObject.SetActive(showTurns);
                    if (showTurns)
                        tTurn.text = (v.remainingTurns > 0) ? v.remainingTurns.ToString() : "∞";
                }

                if (rightmostState)
                {
                    int idx = rightmostState.transform.GetSiblingIndex();
                    go.transform.SetSiblingIndex(idx);  // 상태칩 바로 왼쪽
                }
                else
                {
                    go.transform.SetAsLastSibling();
                    rightmostState = go;
                }
            }
        }

        // 3) 기존 Stackable Status 칩 (디버프)
        if (stacks != null)
        {
            var list = stacks.ToList();
            for (int i = list.Count - 1; i >= 0; --i)
            {
                var v = list[i];
                var go = Instantiate(stackChipPrefab, chipRoot);
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
                    int idx = rightmostState.transform.GetSiblingIndex();
                    go.transform.SetSiblingIndex(idx);
                }
                else
                {
                    go.transform.SetAsLastSibling();
                }
            }
        }

        // 4) 레이아웃 강제 갱신
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
