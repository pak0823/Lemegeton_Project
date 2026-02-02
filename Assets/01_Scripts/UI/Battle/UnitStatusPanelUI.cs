using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UnitStatusPanelUI : MonoBehaviour
{
    [Header("Refs")]
    public BattleManager battle;              // 비워두면 자동 찾음
    public RectTransform enemyParent;         // 왼쪽 중앙 정렬 컨테이너
    public RectTransform playerParent;          // 오른쪽 중앙 정렬 컨테이너
    public UnitStatusItemUI enemyItemPrefab;  // 적용 카드 프리팹
    public UnitStatusItemUI playerItemPrefab;   // 아군용 카드 프리팹

    readonly Dictionary<BattleUnit, UnitStatusItemUI> views = new();

    [Header("Highlight")]
    public Sprite defaultHighlightSprite;

    [SerializeField] private UnitStateVisualDB sharedUnitStateDB;
    [SerializeField] private StackableStatusVisualDB sharedStackVisualDB;



    public enum UnitSort { AsFound, NameAsc, YPosDesc, AgiDesc }    //정렬 방식
    [SerializeField] UnitSort enemySort = UnitSort.AsFound;  // 적 정렬 기준
    [SerializeField] UnitSort playerSort = UnitSort.AsFound;  // 아군 정렬 기준

    void Awake()
    {
        if (!battle) battle = FindObjectOfType<BattleManager>();
        if (battle != null) battle.OnWaveChanged += HandleWaveChanged_RebuildEnemies;
    }

    void Start()
    {
        BuildOnce();
        if (battle != null) battle.OnUnitActionLabel += HandleActionLabel; // 기술명 라벨 업데이트

        //아군 카드가 하나도 없으면 즉시 보강
        if (!views.Keys.Any(k => k && k.data.team == Team.Player))
            BuildOnce();
    }

    void OnDestroy()
    {
        if (battle != null) battle.OnUnitActionLabel -= HandleActionLabel;
        if (battle != null) battle.OnWaveChanged -= HandleWaveChanged_RebuildEnemies;
        foreach (var kv in views)
            if (kv.Key)
            {
                kv.Key.OnDied -= OnUnitDied; //사망 이벤트 구독 해제
                kv.Key.OnRetreated -= OnUnitRetreated;  //도주 이벤트 구독 해제
            }
    }

    // === 정렬 함수 추가 ===
    IEnumerable<BattleUnit> Sort(IEnumerable<BattleUnit> src, UnitSort mode)
    {
        switch (mode)
        {
            case UnitSort.NameAsc: return src.OrderBy(u => u.name);
            case UnitSort.YPosDesc: return src.OrderByDescending(u => u.transform.position.y); // 화면 위→아래
            case UnitSort.AgiDesc: return src.OrderByDescending(u => u.EffectiveAGI);
            default: return src; // 검색된 그대로
        }
    }

    void BuildOnce()
    {
        // 현재 씬의 생존 유닛 조회
        var units = FindObjectsOfType<BattleUnit>().Where(u => !u.IsDead).ToList();

        var enemies = Sort(units.Where(u => u.data.team == Team.Enemy), enemySort);
        var allies = Sort(units.Where(u => u.data.team == Team.Player), playerSort);

        // 이미 만들어진 카드가 있어도 없는 것만 채워 넣음
        foreach (var u in enemies) if (!views.ContainsKey(u)) SpawnCard(enemyParent, enemyItemPrefab, u);
        foreach (var u in allies) if (!views.ContainsKey(u)) SpawnCard(playerParent, playerItemPrefab, u);
    }
    void SpawnCard(RectTransform parent, UnitStatusItemUI prefab, BattleUnit u)
    {
        if (!parent || !prefab) return;
        var item = Instantiate(prefab, parent);
        item.Bind(u);
        item.SetHighlighted(false);
        item.SetSkillLabel(""); // 초기 라벨 비우기

        if (sharedUnitStateDB) item.SetVisualDB(sharedUnitStateDB);
        if (sharedStackVisualDB) item.SetStackVisualDB(sharedStackVisualDB);

        var usc = u.GetComponent<UnitStateController>();
        var sc = u.GetComponent<StatusController>();

        // 초기 렌더
        item.RefreshFromControllers(usc, sc);

        // 이벤트 구독(둘 다 같은 콜백으로)
        if (usc != null)
        {
            usc.OnStatesChanged += () =>
            {
                if (u) item.RefreshFromControllers(usc, sc);
            };
        }
        if (sc != null)
        {
            sc.OnStatusChanged += () =>
            {
                if (u) item.RefreshFromControllers(usc, sc);
            };
        }

        // 초기 상태가 이미 죽어있다면(예외 케이스), Player만 회색 유지
        if (u.data.team == Team.Player && u.IsDead) item.SetDeadStyle(true);

        views[u] = item;
        u.OnDied += OnUnitDied; //사망 이벤트 구독
        u.OnRetreated += OnUnitRetreated; //도주 이벤트 구독
    }
    //public void Resort()  // 현재 views에 있는 유닛만 대상으로 정렬해 재배치
    //{
    //    var enemies = Sort(views.Keys.Where(u => u && u.team == Team.Enemy), enemySort).ToList();
    //    for (int i = 0; i < enemies.Count; i++)
    //        views[enemies[i]].transform.SetSiblingIndex(i);

    //    var allies = Sort(views.Keys.Where(u => u && u.team == Team.Player), playerSort).ToList();
    //    for (int i = 0; i < allies.Count; i++)
    //        views[allies[i]].transform.SetSiblingIndex(i);
    //}

    void OnUnitDied(BattleUnit u)
    {
        if (u == null) return;
        if (!views.TryGetValue(u, out var item)) return;

        if (u.data.team == Team.Enemy)
        {
            // 적은 즉시 제거
            RemoveView(u);
        }
        else
        {
            // 플레이어는 유지 + 회색 하이라이트
            item.SetDeadStyle(true);
            // 필요 시 상태칩/라벨 업데이트 등 추가 작업 가능
        }
    }
    void OnUnitRetreated(BattleUnit u)
    {
        if (u == null) return;
        if (u.data.team == Team.Enemy) return;
        RemoveView(u);
    }

    void RemoveView(BattleUnit u)
    {
        if (!views.ContainsKey(u)) return;
        Destroy(views[u].gameObject);
        views.Remove(u);
    }


    // 웨이브 변경 시 적 카드 재구성
    void HandleWaveChanged_RebuildEnemies(int cur, int total, string _)
    {
        var toRemove = views
        .Where(kv =>
            (kv.Key != null && kv.Key.data.team == Team.Enemy) ||
            (kv.Key == null && kv.Value != null && kv.Value.transform != null &&
              enemyParent != null && kv.Value.transform.IsChildOf(enemyParent)))
        .Select(kv => kv.Key)
        .ToList();

        foreach (var key in toRemove)
        {
            var view = views[key];
            if (view) Destroy(view.gameObject);
            views.Remove(key);
        }

        // 2) 현재 씬의 '생존 적'만 다시 카드 생성
        var enemies = Sort(
        FindObjectsOfType<BattleUnit>().Where(u => u && !u.IsDead && u.data.team == Team.Enemy),
        enemySort
            );
        foreach (var u in enemies)
        {
            if (views.ContainsKey(u)) continue; // 이미 있으면 스킵(이론상 없음)
            SpawnCard(enemyParent, enemyItemPrefab, u);
        }
    }

    // 범위 안의 유닛만 하이라이트
    public void HighlightUnits(IEnumerable<BattleUnit> units, Sprite overlaySprite = null)
    {
        var set = (units != null) ? new HashSet<BattleUnit>(units) : new HashSet<BattleUnit>();
        foreach (var kv in views)
        {
            bool on = set.Contains(kv.Key);
            kv.Value.SetHighlighted(on, overlaySprite ?? defaultHighlightSprite);
        }
    }

    // 전부 끄기
    public void ClearHighlights() => HighlightUnits(null);

    // === 이벤트 핸들러 ===
    void HandleActionLabel(BattleUnit u, string label)
    {
        if (u == null) return;
        if (!views.TryGetValue(u, out var v)) return;
        v.SetSkillLabel(label);
    }
}
