using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

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

    [Header("Buff/Debuff Tags")]
    public Transform statusTagRoot;     // 상태칩 부모
    public GameObject statusTagPrefab;  // 상태칩 프리팹(Text or Image+Text)

    [Header("Highlight (둘 중 하나/둘 다 가능)")]
    [Tooltip("카드 전체 위에 얹을 오버레이 이미지(비활성으로 두고 시작). 없으면 비워두기")]
    public Image highlightOverlay;                 // 예: 전체를 덮는 Image
    public Sprite defaultHighlightSprite;          // (선택) 기본 하이라이트 스프라이트
    public List<Graphic> tintTargets = new List<Graphic>(); //오버레이가 없을 때 색상으로 강조하고 싶은 Graphic들
    public Color highlightTint = new Color(1f, 1f, 0.5f, 1f); // 밝은 노란 톤

    [Header("Death Style")]
    //public Color deadTint = new Color(0.6f, 0.6f, 0.6f, 1f);   // 회색 강조
    [Range(0f, 1f)] public float deadNameAlpha = 0.4f; // 죽었을 때 이름 투명도
    public Sprite deadHighlightSprite;                         // (선택) 사망 오버레이
    Color _nameOrigColor;
    bool _nameColorCached;

    [Header("Status Icons")]
    public Sprite slowIcon; // 필요시 다른 아이콘도 추가


    Color[] _originalColors;
    bool _highlighted;
    bool _isDead;
    Sprite _pendingOverlaySprite;         // SetHighlighted에서 전달받아 보관

    BattleUnit Battleunit;
    StatusController StatusController;

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
        if (StatusController != null) StatusController.OnStatusChanged += HandleStatusChanged;
        if (Battleunit != null) Battleunit.OnDied += HandleUnitDied;
        HandleStatusChanged(); // 초기 1회
    }

    void OnDestroy()
    {
        if (StatusController != null) StatusController.OnStatusChanged -= HandleStatusChanged;
    }
    void HandleUnitDied(BattleUnit dead)
    {
        ClearStatusChips();                 // 자식 프리팹 전부 Destroy
        if (statusTagRoot != null)
            statusTagRoot.gameObject.SetActive(false); // (선택) 루트 자체 숨김
    }
    void HandleStatusChanged()
    {
        if (StatusController == null) return;
        SetStatusViews(StatusController.GetStatusViews());
    }
    void ClearStatusChips()
    {
        if (!statusTagRoot) return;
        for (int i = statusTagRoot.childCount - 1; i >= 0; i--)
            Destroy(statusTagRoot.GetChild(i).gameObject);
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

        var sc = Battleunit != null ? Battleunit.GetComponent<StatusController>() : null;
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
    public void SetStatusViews(IEnumerable<StatusController.StatusView> views)
    {
        if (!statusTagRoot || !statusTagPrefab) return;

        // 기존 칩 제거
        for (int i = statusTagRoot.childCount - 1; i >= 0; i--)
            Destroy(statusTagRoot.GetChild(i).gameObject);

        if (views == null) return;

        foreach (var v in views)
        {
            var go = Instantiate(statusTagPrefab, statusTagRoot);

            // 아이콘 지정 (필요하면 이름 "Icon"을 가진 Image를 찾음)
            var icon = go.GetComponentsInChildren<Image>(true).FirstOrDefault(img => img.name == "Icon");
            if (icon != null)
            {
                icon.sprite = GetIcon(v.id);
                icon.enabled = icon.sprite != null;
            }

            // 텍스트 2개 지정
            var texts = go.GetComponentsInChildren<Text>(true);
            var turnText = texts.FirstOrDefault(t => t.name == "Text_Turn");
            var stackText = texts.FirstOrDefault(t => t.name == "Text_Stack");

            if (turnText) turnText.text = Mathf.Max(0, v.remainingTurns).ToString(); // 남은 턴
            if (stackText) stackText.text = Mathf.Max(0, v.stacks).ToString();        // 스택 수

            // 필요 시 1스택/0턴일 때 숨김 처리하고 싶다면:
            // if (stackText) stackText.gameObject.SetActive(v.stacks > 1);
            // if (turnText)  turnText.gameObject.SetActive(v.remainingTurns > 0);
        }
    }
    Sprite GetIcon(StatusId id)
    {
        switch (id)
        {
            case StatusId.Slow: return slowIcon;
            default: return null;
        }
    }

    // === 외부 이벤트 훅 ===
    public void SetSkillLabel(string label)
    {
        if (skillNameText) skillNameText.text = label ?? "";
    }

    public void SetStatusTags(IEnumerable<string> tags)
    {
        if (!statusTagRoot || !statusTagPrefab) return;

        for (int i = statusTagRoot.childCount - 1; i >= 0; i--)
            Destroy(statusTagRoot.GetChild(i).gameObject);

        if (tags == null) return;
        foreach (var t in tags)
        {
            var go = Instantiate(statusTagPrefab, statusTagRoot);
            var txt = go.GetComponentInChildren<Text>();
            if (txt) txt.text = t;
        }
    }
}
