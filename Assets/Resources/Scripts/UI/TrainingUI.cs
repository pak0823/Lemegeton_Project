using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshPro 사용 안하면 Text로 바꿔도 됨

public class TrainingUI : MonoBehaviour
{
    bool isShow = true;

    [Header("Refs")]
    public TrainingDB trainingDB;                 // 인스펙터에 TrainingDB 할당
    public List<UnitData> ownedUnits;             // 플레이어 소유 유닛 목록 (임시: 인스펙터로 지정)
    public Transform unitListRoot;                // 유닛 버튼 부모
    public Transform skillListRoot;               // 스킬 버튼 부모
    public Transform routesRoot;                  // 3개 루트 버튼 부모

    public Button saveButton;                     // 좌하단 저장
    public Button resetButton;                    // 우하단 초기화

    [Header("Prefabs")]
    public Button unitButtonPrefab;
    public Button skillButtonPrefab;
    public Button routeButtonPrefab;

    // 선택 상태
    UnitData _selectedUnit;
    SkillAsset _selectedSkill;
    int _selectedRoute = -1; // -1 = 미선택

    // 임시 캐시(스킬 버튼들/루트 버튼들 텍스트 갱신용)
    readonly List<Button> _unitBtns = new();
    readonly List<Button> _skillBtns = new();
    readonly List<Button> _routeBtns = new();

    // 스킬별로 선택된 루트를 임시 저장해 두는 캐시
    readonly Dictionary<SkillAsset, int> _pendingRoutes = new();

    void Start()
    {
        BuildUnitList();
        saveButton.onClick.AddListener(OnClickSave);
        resetButton.onClick.AddListener(OnClickReset);
        OnHide();
    }

    void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject);
    }

    public void OnToggle()
    {
        isShow = !isShow;
        if (isShow)
            OnShow();
        else
            OnHide();
    }
    void OnShow()
    {
        isShow = true;
        this.gameObject.SetActive(isShow);
    }
    void OnHide()
    {
        isShow = false;
        this.gameObject.SetActive(isShow);
    }


    void BuildUnitList()
    {
        ClearChildren(unitListRoot);
        _unitBtns.Clear();

        foreach (var ud in ownedUnits)
        {
            if (!ud) continue;
            var b = Instantiate(unitButtonPrefab, unitListRoot);
            var txt = b.GetComponentInChildren<TMP_Text>();
            if (txt) txt.text = ud.DisplayName;
            else b.GetComponentInChildren<Text>().text = ud.DisplayName;

            b.onClick.AddListener(() => { SelectUnit(ud); });
            _unitBtns.Add(b);
        }
    }

    void SelectUnit(UnitData ud)
    {
        _selectedUnit = ud;
        _selectedSkill = null;
        _selectedRoute = -1;

        _pendingRoutes.Clear(); // 유닛 바꿀 때 캐시 초기화

        BuildSkillList();
        BuildRoutes(null); // 클리어
    }

    void BuildSkillList()
    {
        ClearChildren(skillListRoot);
        _skillBtns.Clear();

        if (_selectedUnit == null || _selectedUnit.skills == null) return;

        for (int i = 0; i < _selectedUnit.skills.Length; i++)
        {
            var s = _selectedUnit.skills[i];
            if (!s) continue;

            // 패시브가 아니고(우리는 SkillAsset만 보유), 액티브만 보여주기:
            // 기준: targetMode == Unit/Tile 둘 중 하나
            if (s.targetMode != SkillTargetMode.Unit && s.targetMode != SkillTargetMode.Tile)
                continue;

            var b = Instantiate(skillButtonPrefab, skillListRoot);
            var txt = b.GetComponentInChildren<TMP_Text>();
            if (txt) txt.text = string.IsNullOrEmpty(s.displayName) ? s.name : s.displayName;
            else b.GetComponentInChildren<Text>().text = string.IsNullOrEmpty(s.displayName) ? s.name : s.displayName;

            b.onClick.AddListener(() => { SelectSkill(s); });
            _skillBtns.Add(b);
        }
    }

    void SelectSkill(SkillAsset s)
    {
        _selectedSkill = s;

        if (s == null)
        {
            _selectedRoute = -1;
            BuildRoutes(null);
            return;
        }

        // 1순위: 이번 UI 세션에서 선택해둔 값(_pendingRoutes)
        if (_pendingRoutes.TryGetValue(s, out var cachedRoute))
        {
            _selectedRoute = cachedRoute;
        }
        else
        {
            // 2순위: TrainingDB에 이미 저장된 값
            _selectedRoute = (trainingDB != null && _selectedUnit != null)
                ? trainingDB.GetRoute(_selectedUnit, _selectedSkill)
                : -1;
        }

        BuildRoutes(s);
    }

    void BuildRoutes(SkillAsset s)
    {
        ClearChildren(routesRoot);
        _routeBtns.Clear();

        if (s == null)
            return;

        // SkillAsset.trainingRoutes 우선 사용
        var routeInfos = s.trainingRoutes;

        // (선택) ITrainableSkill도 지원하고 싶다면, title/desc가 비어있을 때만 보완용으로 사용
        ITrainableSkill.TrainingOption[] fallback = null;
        if ((routeInfos == null || routeInfos.Length == 0) && s is ITrainableSkill trainable)
            fallback = trainable.GetTrainingOptions();


        // 항상 3칸 생성
        for (int i = 0; i < 3; i++)
        {
            var b = Instantiate(routeButtonPrefab, routesRoot);
            var txt = b.GetComponentInChildren<TMPro.TMP_Text>();
            var uText = (txt == null) ? b.GetComponentInChildren<UnityEngine.UI.Text>() : null;

            string title = $"루트 {i + 1}";
            string desc = "훈련 정보가 설정되지 않았습니다.";

            if (routeInfos != null && i < routeInfos.Length)
            {
                if (!string.IsNullOrEmpty(routeInfos[i].title)) title = routeInfos[i].title;
                if (!string.IsNullOrEmpty(routeInfos[i].description)) desc = routeInfos[i].description;
            }
            else if (fallback != null && i < fallback.Length)
            {
                // (옵션) ITrainableSkill 보완
                if (!string.IsNullOrEmpty(fallback[i].title)) title = fallback[i].title;
                if (!string.IsNullOrEmpty(fallback[i].description)) desc = fallback[i].description;
            }

            string composed = $"{title}\n<size=70%>{desc}</size>";
            if (txt) txt.text = composed;
            else if (uText) uText.text = title + "\n" + desc;

            int idx = i;
            b.onClick.AddListener(() =>
            {
                _selectedRoute = idx;
                if (_selectedSkill != null)
                {
                    // 현재 스킬에 대해 선택한 루트를 캐시에 저장
                    _pendingRoutes[_selectedSkill] = idx;
                }
                HighlightRoutes();
            });
            _routeBtns.Add(b);
        }


        HighlightRoutes();
    }

    void HighlightRoutes()
    {
        for (int i = 0; i < _routeBtns.Count; i++)
        {
            // 간단히 interactable 로 강조(선택된 건 비활성처럼 보이게)
            _routeBtns[i].interactable = (i != _selectedRoute);
        }
    }

    void OnClickSave()
    {
        if (trainingDB == null || _selectedUnit == null)
            return;

        // 캐시에 쌓인 모든 (스킬, 루트)를 저장
        if (_pendingRoutes.Count > 0)
        {
            foreach (var kv in _pendingRoutes)
            {
                var skill = kv.Key;
                var route = kv.Value;

                if (skill == null) continue;
                // route는 -1(미선택) ~ 2 범위, TrainingDB.SetRoute 안에서 clamp됨
                trainingDB.SetRoute(_selectedUnit, skill, route);
            }
        }
        else
        {
            // 혹시 예전 방식처럼 "현재 스킬 하나만" 저장하던 패턴을 유지하고 싶다면
            if (_selectedSkill != null && _selectedRoute >= 0)
            {
                trainingDB.SetRoute(_selectedUnit, _selectedSkill, _selectedRoute);
            }
        }

        Debug.Log("[TrainingUI] 저장 완료");
    }

    void OnClickReset()
    {
        if (trainingDB != null && _selectedUnit != null)
            trainingDB.ClearSelectionsFor(_selectedUnit);

        _selectedUnit = null;
        _selectedSkill = null;
        _selectedRoute = -1;
        _pendingRoutes.Clear();

        ClearChildren(skillListRoot);
        ClearChildren(routesRoot);
        BuildSkillList();

        Debug.Log("[TrainingUI] 초기화 완료 (해당 유닛)");
    }
}
