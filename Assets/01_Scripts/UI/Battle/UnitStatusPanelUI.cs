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
        if (!battle) battle = BattleManager.Instance;

        if (battle != null)
        {
            battle.RegisterStatusPanel(this);
            battle.OnWaveChanged += HandleWaveChanged_RebuildEnemies;
            battle.OnWaveStarted += BuildOnce;
        }
    }

    void Start()
    {
        // 빌드 환경 초기화 타이밍이 꼬이는 것을 방지하기 위해 0.1초 딜레이 후 강제 수행
        Invoke(nameof(BuildOnce), 0.1f);
        if (battle != null) battle.OnUnitActionLabel += HandleActionLabel; // 기술명 라벨 업데이트
    }

    private float buildTimer = 0f;

    void Update()
    {
        // 1초 단위로 지속적으로 아직 만들어지지 않은 패널이 있는지 체크 (빌드 타이밍 예방용 확실한 폴백)
        buildTimer += Time.deltaTime;
        if (buildTimer > 1f)
        {
            buildTimer = 0f;
            BuildOnce();
        }
    }

    void OnDestroy()
    {
        if (battle != null)
        {
            battle.OnUnitActionLabel -= HandleActionLabel;
            battle.OnWaveChanged -= HandleWaveChanged_RebuildEnemies;
            battle.OnWaveStarted -= BuildOnce;
        }
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
        if (battle == null)
        {
            battle = BattleManager.Instance;
            if (battle != null)
            {
                battle.RegisterStatusPanel(this);
                battle.OnWaveChanged -= HandleWaveChanged_RebuildEnemies;
                battle.OnWaveChanged += HandleWaveChanged_RebuildEnemies;
                // OnUnitActionLabel도 필요하다면 연결
                battle.OnUnitActionLabel -= HandleActionLabel;
                battle.OnUnitActionLabel += HandleActionLabel;
            }
        }

        // 현재 씬의 생존 유닛 조회 (매니저 캐싱 사용)
        var units = (battle != null) ? battle.ActiveUnits.Where(u => u != null && !u.IsDead).ToList() : new List<BattleUnit>();

        // 빌드 환경 등에서 초기화 타이밍 문제로 매니저가 유닛을 못주면
        // 하이어라키에서 직접 한 번 더 긁어옵니다. (Fallback)
        if (units.Count == 0)
        {
            units = FindObjectsOfType<BattleUnit>().Where(u => u != null && u.gameObject.activeInHierarchy && !u.IsDead).ToList();
        }

        // data가 아직 할당되지 않은 (초기화 중인) 유닛은 그리지 않음
        var validUnits = units.Where(u => u.data != null).ToList();

        // 디버깅: 빌드에서 유닛이 왜 안나오는지 카운트 체크
        if (units.Count > 0 && validUnits.Count != lastReportedCount)
        {
            Debug.Log($"[UnitStatusPanelUI] BuildOnce: Found {units.Count} units. Valid Data: {validUnits.Count}. Parent(E/P): {enemyParent!=null}/{playerParent!=null}");
            lastReportedCount = validUnits.Count;
        }

        var enemies = Sort(validUnits.Where(u => u.data.team == Team.Enemy), enemySort);
        var allies = Sort(validUnits.Where(u => u.data.team == Team.Player), playerSort);

        // 이미 만들어진 카드가 있어도 없는 것만 채워 넣음
        foreach (var u in enemies) if (!views.ContainsKey(u)) SpawnCard(enemyParent, enemyItemPrefab, u);
        foreach (var u in allies) if (!views.ContainsKey(u)) SpawnCard(playerParent, playerItemPrefab, u);
    }

    private int lastReportedCount = -1;
    void SpawnCard(RectTransform parent, UnitStatusItemUI prefab, BattleUnit u)
    {
        if (!parent || !prefab) return;
        var item = Instantiate(prefab, parent);
        item.Bind(u);
        item.SetHighlighted(false);
        item.SetSkillLabel(""); // 초기 라벨 비우기

        if (sharedUnitStateDB) item.SetVisualDB(sharedUnitStateDB);
        if (sharedStackVisualDB) item.SetStackVisualDB(sharedStackVisualDB);

        // 초기 상태가 이미 죽어있다면(예외 케이스), Player만 회색 유지
        if (u.data.team == Team.Player && u.IsDead) item.SetDeadStyle(true);

        views[u] = item;
        u.OnDied += OnUnitDied; //사망 이벤트 구독
        u.OnRetreated += OnUnitRetreated; //도주 이벤트 구독
    }

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
            (kv.Key != null && kv.Key.data != null && kv.Key.data.team == Team.Enemy) ||
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
        var units = (battle != null ? battle.ActiveUnits : System.Linq.Enumerable.Empty<BattleUnit>())
                        .Where(u => u != null && !u.IsDead).ToList();

        if (units.Count == 0 || !units.Any(u => u.data != null && u.data.team == Team.Enemy))
        {
            // 빌드 환경 지연 대비 Fallback
            var fb = FindObjectsOfType<BattleUnit>().Where(u => u != null && u.gameObject.activeInHierarchy && !u.IsDead).ToList();
            if (fb.Count > 0) units = fb;
        }

        var validEnemies = units.Where(u => u.data != null && u.data.team == Team.Enemy).ToList();
        var enemies = Sort(validEnemies, enemySort);

        foreach (var u in enemies)
        {
            if (views.ContainsKey(u)) continue;
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
