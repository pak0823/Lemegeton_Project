using System.Collections.Generic;

using System.Linq;

using UnityEngine;

using UnityEngine.UI;



public class TurnBarUI : MonoBehaviour

{

    [Header("UI Settings")]

    public RectTransform barImage;          // 바(이미지)의 RectTransform

    public GameObject unitIconPrefab;  // 아이콘 프리팹(Anchor=Left, Pivot=Center 권장)

    [SerializeField] private Text wavelabel;    //wave 텍스트

    [SerializeField] private CanvasGroup waveTransitionPanel; // 전환 안내 패널(투명도/입력제어용)

    [SerializeField] private Text waveTransitionText;         // 전환 안내 문구 텍스트

    [SerializeField] private float transitionDuration = 1.5f; // 표시 시간(초, 실시간)



    [Header("Active Turn Display")]

    [SerializeField] RectTransform activeSlot;             // 턴바 오른쪽 바깥에 놓을 빈 컨테이너

    [SerializeField] Vector2 activeIconSize = new Vector2(96, 96);

    [SerializeField] float activePopDuration = 0.12f;      // 살짝 팝업 애니

    [SerializeField] bool hideInBarWhileActive = true;     // 활성화된 유닛은 바에서 숨김



    [Tooltip("플레이어/적 아이콘 Y 오프셋")]

    public float playerRowY = 20f;

    public float enemyRowY = -20f;



    [Tooltip("아이콘 간 추가 간격(아이콘 너비에 더해짐)")]

    public float extraSpacing = 0f;



    [Tooltip("체크 시 아이콘 간격 보정 없이 '겹치도록' 배치합니다.")]

    public bool allowOverlap = false;



    public float barWidth;  // 현재 바 너비

    Dictionary<BattleUnit, Image> unitIcons = new();

    bool uiPaused = false;  // 전환 중 UI 정지



    BattleManager battle;

    BattleUnit activeUnit;        // 현재 턴 주인공

    Image activeBigIcon;          // activeSlot에 띄운 큰 아이콘

    Coroutine activePopCo;



    void Start()

    {

        battle = FindObjectOfType<BattleManager>();

        barWidth = barImage.rect.width;



        if (battle != null)

        {

            battle.OnATBChanged += OnATBChanged_RelayoutAll;

            InitializeIcons();

            RelayoutAll();// 최초 1회 전체 그리기



            battle.OnWaveChanged += WaveHandle;

            WaveHandle(battle.CurrentWave, battle.TotalWaves, null); // 초기 웨이브 표시 누락 방지



            // 웨이브 변경 시 아이콘 전부 재생성

            battle.OnWaveChanged += (_, __, ___) =>

            {

                RebuildIconsFromScene();

                RelayoutAll();

            };



            battle.OnATBReset += HandleATBResetToZero;

            battle.OnWaveTransition += ShowWaveTransition;  // 다음 웨이브 전환 안내 구독

        }



        BattleManager.OnAnyUnitTurnStarted += HandleTurnStarted;

    }

    void OnDestroy()

    {

        if (battle)

        {

            battle.OnATBChanged -= OnATBChanged_RelayoutAll;

            battle.OnWaveChanged -= WaveHandle;

            battle.OnATBReset -= HandleATBResetToZero;

            battle.OnWaveTransition -= ShowWaveTransition;

        }



        BattleManager.OnAnyUnitTurnStarted -= HandleTurnStarted;

    }



    void InitializeIcons()

    {

        // [Optimization] Use BattleManager
        var bm = BattleManager.Instance;
        var units = (bm != null) ? bm.GetAllUnits() : FindObjectsOfType<BattleUnit>().ToList();

        foreach (var u in units)

        {

            var iconGO = Instantiate(unitIconPrefab, barImage);

            var img = iconGO.GetComponent<Image>();

            if (u.data != null) img.sprite = u.data.UnitIcon;



            unitIcons[u] = img;



            var rt = img.rectTransform;

            // 초기 Y를 팀별로 분리

            rt.anchoredPosition = new Vector2(0f, u.data.team == Team.Player ? playerRowY : enemyRowY);



            // 사망,도주 이벤트 구독 → 제거

            u.OnDied += RemoveUnitIcon;

            u.OnRetreated += RemoveUnitIcon;

        }

    }



    // 현재 씬의 유닛을 기준으로 아이콘 사전 동기화

    void RebuildIconsFromScene()

    {

        // 1) 사라진 유닛 정리

        foreach (var kv in unitIcons.ToArray())

        {

            var unit = kv.Key;

            if (unit == null)

            {

                if (kv.Value) Destroy(kv.Value.gameObject);

                unitIcons.Remove(unit);

            }

        }

        // 2) 존재하지만 아이콘이 없는 유닛 추가

        foreach (var u in FindObjectsOfType<BattleUnit>())

        {

            if (unitIcons.ContainsKey(u)) continue;

            var iconGO = Instantiate(unitIconPrefab, barImage);

            var img = iconGO.GetComponent<Image>();

            if (u.data != null) img.sprite = u.data.UnitIcon;

            unitIcons[u] = img;

            var rt = img.rectTransform;

            rt.anchoredPosition = new Vector2(0f, u.data.team == Team.Player ? playerRowY : enemyRowY);

            u.OnDied += RemoveUnitIcon;

        }

    }



    void OnATBChanged_RelayoutAll(BattleUnit _battleunit, float _atb, float _maxatb)

    {

        //if (_battleunit.name == "LuckySix") Debug.Log($"[TurnBarUI] Received ATB for {_battleunit?.name ?? "null"} : atb={_atb:F3}, max={_maxatb:F3}");  //ActiveIcon의 출력이 이상할 시 테스트용으로 남겨둠



        // 보조 판정: atb가 아주 작아졌으면(턴이 막 끝나서 리셋된 상태) active 아이콘 정리

        // (원래보다 느슨한 임계값을 사용해서 실수/소수 오류 방지)

        const float kEps = 0.25f; // 필요시 0.05 ~ 0.2 사이로 조절



        // 턴 종료(= ATB가 0으로 리셋) 프레임에 액티브 표시 정리

        if (hideInBarWhileActive && activeUnit != null && _battleunit == activeUnit && _atb <= kEps)

        {

            ClearActiveIcon(); // 아래 2번 패치로 "좌측으로 스냅"까지 함께 처리

        }



        RelayoutAll(); // 정리 후 배치

    }



    void RelayoutAll()

    {

        if (barImage == null || unitIcons.Count == 0) return;

        if (uiPaused) return; // 전환 중엔 배치 정지(현재 위치 고정)

        if (barImage == null || unitIcons.Count == 0) return;



        // 두 줄로 분리

        var players = new List<Item>();

        var enemies = new List<Item>();



        foreach (var kv in unitIcons.ToArray()) // 혹시 파괴된 유닛/아이콘이 사전에 남아있을 수 있음

        {

            var unit = kv.Key;

            var img = kv.Value;

            if (unit == null || img == null) { unitIcons.Remove(unit); continue; }



            // 활성 유닛은 바에서 숨기고, 레이아웃 계산 대상에서 제외

            if (hideInBarWhileActive && unit == activeUnit)

            {

                img.gameObject.SetActive(false);

                continue;

            }

            else if (!img.gameObject.activeSelf)

            {

                // 활성 유닛이 아니면 반드시 보이게

                img.gameObject.SetActive(true);

            }



            var rt = img.rectTransform;

            float width = rt.rect.width;

            float half = Mathf.Max(1f, width * 0.5f);

            float pivot = rt.pivot.x;

            float minX = width * pivot;

            float maxX = Mathf.Max(minX, barWidth - width * (1f - pivot));



            // 0~1 정규화 ATB

            float normalized = unit.MaxATB > 0 ? Mathf.Clamp01(unit.ATB / unit.MaxATB) : 0f;

            float desired = Mathf.Lerp(minX, maxX, normalized);



            var item = new Item

            {

                unit = unit,

                rt = rt,

                half = half,

                width = width,

                pivot = pivot,

                desired = desired,

                y = (unit.data.team == Team.Player) ? playerRowY : enemyRowY

            };



            if (unit.data.team == Team.Player) players.Add(item); else enemies.Add(item);

        }



        ArrangeRow(players);

        ArrangeRow(enemies);

    }

    void ArrangeRow(List<Item> row)

    {

        if (row.Count == 0) return;



        // 원하는 위치 기준으로 정렬

        row.Sort((a, b) => a.desired.CompareTo(b.desired));



        //  겹침 허용

        if (allowOverlap)

        {

            for (int i = 0; i < row.Count; i++)

            {

                var it = row[i];



                float minX = it.width * it.pivot;

                float maxX = barWidth - it.width * (1f - it.pivot);

                float x = Mathf.Clamp(it.desired, minX, maxX);



                it.rt.anchoredPosition = new Vector2(x, it.y);



                // 오른쪽(더 뒤 순번)일수록 위로 올리려면

                it.rt.SetSiblingIndex(Mathf.Min(barImage.childCount - 1, i));



                // 반대로 왼쪽이 위로 오게 하려면:

                //it.rt.SetSiblingIndex(Mathf.Min(barImage.childCount - 1, row.Count - 1 - i));

            }

            return;

        }



        // 좌→우로 진행하며 겹치지 않게 '오른쪽으로' 민다

        for (int i = 0; i < row.Count; i++)

        {

            var cur = row[i]; // 복사본

            float minX = cur.width * cur.pivot;                   // 왼쪽 경계 내 피벗 최소

            float maxX = barWidth - cur.width * (1f - cur.pivot);        // 오른쪽 경계 내 피벗 최대



            float x = Mathf.Clamp(cur.desired, minX, maxX);



            if (i > 0)

            {

                var prev = row[i - 1]; // 이미 확정된 값 사용

                float minSep = prev.x + prev.half + cur.half + extraSpacing;

                if (x < minSep) x = minSep; //옆 아이콘과 겹치지 않기

            }



            cur.x = Mathf.Clamp(x, minX, maxX);

            row[i] = cur;   // 수정된 값 되돌려놓기

        }



        // 3) 우→좌로 진행하며 오른쪽 경계 초과/겹침을 좌측으로 보정

        for (int i = row.Count - 1; i >= 0; i--)

        {

            var cur = row[i]; // 복사본

            float minX = cur.width * cur.pivot;

            float maxX = barWidth - cur.width * (1f - cur.pivot);



            if (cur.x > maxX) cur.x = maxX;



            if (i < row.Count - 1)

            {

                var next = row[i + 1]; // 오른쪽 아이콘

                float maxLeft = next.x - (next.half + cur.half + extraSpacing);

                if (cur.x > maxLeft) cur.x = Mathf.Max(minX, maxLeft);

            }



            row[i] = cur; // 수정된 값 되돌려놓기

        }



        // 적용

        foreach (var it in row)

        {

            it.rt.anchoredPosition = new Vector2(it.x, it.y);

        }

    }

    void HandleTurnStarted(BattleUnit u)

    {

        if (u == null) return;

        ClearActiveIcon();  // 이전 active 아이콘을 완전히 정리

        ShowActiveIcon(u);  // 새 유닛의 큰 아이콘을 띄움

    }



    void ShowActiveIcon(BattleUnit u)

    {

        // 이전 표시 정리

        ClearActiveIcon();



        activeUnit = u;



        // 턴바의 작은 아이콘 숨김(레이아웃에서 제외)

        if (unitIcons.TryGetValue(u, out var small))

            small.gameObject.SetActive(!hideInBarWhileActive);



        if (!activeSlot) return;



        // 큰 아이콘 생성

        var go = Instantiate(unitIconPrefab, activeSlot);

        activeBigIcon = go.GetComponent<Image>();

        if (activeBigIcon && u.data) activeBigIcon.sprite = u.data.UnitIcon;



        var rt = activeBigIcon.rectTransform;

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);

        rt.anchoredPosition = Vector2.zero;

        rt.sizeDelta = activeIconSize;



        // 팝업 애니

        if (activePopCo != null) StopCoroutine(activePopCo);

        activePopCo = StartCoroutine(Co_PopIn(rt));

    }

    System.Collections.IEnumerator Co_PopIn(RectTransform rt)

    {

        if (!rt) yield break;

        float t = 0f, dur = Mathf.Max(0.01f, activePopDuration);

        Vector3 from = Vector3.one * 0.85f, to = Vector3.one;

        rt.localScale = from;

        while (t < dur)

        {

            t += Time.unscaledDeltaTime; // UI 애니는 실시간 기준

            float k = Mathf.Clamp01(t / dur);

            rt.localScale = Vector3.Lerp(from, to, k);

            yield return null;

        }

        rt.localScale = to;

        activePopCo = null;

    }



    void HandleATBResetToZero()

    {

        // 현재 아이콘 구성(웨이브 교체로 갱신된 상태)을 0 지점으로 이동

        foreach (var kv in unitIcons.ToArray())

        {

            var unit = kv.Key;

            var img = kv.Value;

            if (!img) continue;

            // 행(Y)은 유지, X만 0으로

            var rt = img.rectTransform;

            rt.anchoredPosition = new Vector2(0f, rt.anchoredPosition.y);

        }

        // 내부 레이아웃 계산 로직이 있다면 한 번 더 강제

        RelayoutAll();

        ClearActiveIcon();

    }





    // 유닛 사망 시 아이콘 제거

    void RemoveUnitIcon(BattleUnit unit)

    {

        if (!unitIcons.ContainsKey(unit)) return;

        if (activeUnit == unit) ClearActiveIcon();



        Destroy(unitIcons[unit].gameObject);

        unitIcons.Remove(unit);

    }

    void ClearActiveIcon()

    {

        if (activeBigIcon) Destroy(activeBigIcon.gameObject);

        activeBigIcon = null;



        // 턴바의 작은 아이콘 다시 보이게 + 좌측으로 스냅

        var u = activeUnit;

        if (u && unitIcons.TryGetValue(u, out var img) && img)

        {

            img.gameObject.SetActive(true);

            var rt = img.rectTransform;

            float xMin = rt.rect.width * rt.pivot.x; // 바의 왼쪽 경계 내 피벗 최소

            float y = (u.data.team == Team.Player) ? playerRowY : enemyRowY;

            rt.anchoredPosition = new Vector2(xMin, y); // 즉시 왼쪽으로 스냅

        }



        activeUnit = null;

    }



    // Wave 텍스트 표시

    private void WaveHandle(int cur, int total, string waveLabel)

    {

        if (!wavelabel) return;

        wavelabel.text = $"{cur}";

    }



    // 전환 안내 표시

    void ShowWaveTransition(int next, int total)

    {

        if (waveTransitionPanel == null) { uiPaused = true; StartCoroutine(Co_AutoUnpause()); return; }

        StopCoroutineSafe("Co_ShowWaveTransition");

        StartCoroutine(Co_ShowWaveTransition(next, total));

    }



    System.Collections.IEnumerator Co_ShowWaveTransition(int next, int total)

    {

        uiPaused = true;

        if (waveTransitionText)

            waveTransitionText.text = $"다음 웨이브가 진행됩니다.";

        waveTransitionPanel.gameObject.SetActive(true);

        waveTransitionPanel.alpha = 1f;

        waveTransitionPanel.interactable = false;

        waveTransitionPanel.blocksRaycasts = false;

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, transitionDuration));

        waveTransitionPanel.alpha = 0f;

        waveTransitionPanel.gameObject.SetActive(false);

        uiPaused = false;

    }



    // 패널이 없을 때도 일정 시간 후 자동 해제

    System.Collections.IEnumerator Co_AutoUnpause()

    {

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, transitionDuration));

        uiPaused = false;

    }



    void StopCoroutineSafe(string name)

    {

        var r = GetType().GetMethod(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // 이름 기반 Stop은 비권장이라, 여기선 호출 전 StopAllCoroutines로 간단히 처리하거나 필요 없으면 생략해도 됩니다.

    }



    struct Item

    {

        public BattleUnit unit;

        public RectTransform rt;

        public float half;

        public float width;

        public float pivot; // rt.pivot.x

        public float desired;

        public float x;

        public float y;

    }

}

