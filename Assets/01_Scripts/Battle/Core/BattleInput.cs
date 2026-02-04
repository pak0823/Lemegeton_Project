using Project.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using System;

public class BattleInput : MonoBehaviour
{
    public BattleManager battleManager;
    private IGridProvider grid; // 필드 추가
    private IBattleMapProvider mapProvider; // 추가

    // Inspector
    public Camera cam;
    public LayerMask unitMask;

    public GameSpeedController speedCtrl; // GameSpeedController 할당
    public HudController hudCtrl;   //HUD 컨트롤러 할당
    public OptionsMenuUI optionsMenuUI; //옵션창 할당

    // Events (매니저가 구독)

    public event Action<Tilemap, Vector3Int> OnTileClick;
    public event Action<Tilemap, Vector3Int> OnTileHover;

    public event Action<BattleUnit> OnUnitClick;
    public event Action<BattleUnit> OnUnitHover;
    public event Action OnCancelKeyPress;
    public event Action OnConfirmKeyPress;
    public event Action OnEscapeKeyPress;

    // 내부 변수
    private Vector3Int _lastHoverCell = new Vector3Int(int.MaxValue, int.MaxValue, 0);
    private BattleUnit _lastHoverUnit = null;

    // 키 바인딩
    readonly (KeyCode key, int idx)[] speedBinds = new (KeyCode, int)[] {
        (KeyCode.Alpha1, 0), (KeyCode.Alpha2, 1), (KeyCode.Alpha3, 2), (KeyCode.BackQuote, 3)
    };
    [SerializeField] private KeyCode battle_CancelKey = KeyCode.Q;
    [SerializeField] private KeyCode battle_CurrentKey = KeyCode.E;
    [SerializeField] private KeyCode battle_HudKey = KeyCode.Tab;
    [SerializeField] private KeyCode battle_Escape = KeyCode.F1;    //도주 키

    private int _rebindTries = 0; //재바인딩 시도 카운터(디버그용)

    #region Internal References
    IBattleMapProvider provider;
    #endregion

    #region Unity Callbacks
    void Awake()
    {
        if (cam == null) cam = Camera.main;

    }

    public void Initialize(BattleManager _battleManager, IGridProvider _grid, IBattleMapProvider _mapProvider)
    {
        this.battleManager = _battleManager;
        this.grid = _grid;
        this.mapProvider = _mapProvider;
    }

    void Update()
    {
        // 키보드 입력 감지
        HandleHotkeys();

        // 마우스 위치 계산
        Vector3 worldPos = cam.ScreenToWorldPoint(Input.mousePosition);
        worldPos.z = 0;

        // 호버링(Hover) 감지 및 이벤트 발송
        HandleHover(worldPos);

        // 클릭(Click) 감지
        HandleClick(worldPos);
    }
    #endregion

    // 좌표 변환 헬퍼 함수
    // BattleGridManager -> Tilemap -> WorldToCell 순서로 접근
    private bool TryGetMapAndCell(Vector3 worldPos, out Tilemap outMap, out Vector3Int outCell)
    {
        outMap = null;
        outCell = default;

        if (grid == null) return false;

        // 1. 플레이어 맵 먼저 체크
        Tilemap pMap = grid.GetMap(Team.Player);
        if (pMap != null)
        {
            Vector3Int c = pMap.WorldToCell(worldPos);
            if (pMap.HasTile(c))
            {
                outMap = pMap;
                outCell = c;
                return true;
            }
        }

        // 2. 적 맵 체크 (플레이어 맵에 없으면)
        Tilemap eMap = grid.GetMap(Team.Enemy);
        if (eMap != null)
        {
            Vector3Int c = eMap.WorldToCell(worldPos);
            if (eMap.HasTile(c))
            {
                outMap = eMap;
                outCell = c;
                return true;
            }
        }

        return false;
    }

    void HandleHover(Vector3 worldPos)
    {
        // 유닛 호버 체크
        var hit = Physics2D.OverlapCircle(worldPos, 0.1f, unitMask);
        BattleUnit unit = hit ? hit.GetComponentInParent<BattleUnit>() : null;

        if (unit != _lastHoverUnit)
        {
            _lastHoverUnit = unit;
            OnUnitHover?.Invoke(unit); // 유닛 호버 이벤트 발사
        }

        // 맵 정보까지 함께 호버링 체크
        if (TryGetMapAndCell(worldPos, out Tilemap map, out Vector3Int cell))
        {
            if (cell != _lastHoverCell) // 맵이 바뀌는 경우는 드무니 셀 변경만 체크해도 충분
            {
                _lastHoverCell = cell;
                OnTileHover?.Invoke(map, cell); // ★ Map 전달
            }
        }
    }

    void HandleClick(Vector3 worldPos)
    {
        // 좌클릭 (0번 버튼)
        if (Input.GetMouseButtonDown(0))
        {
            // 1. 유닛 클릭 우선 판정
            if (_lastHoverUnit != null)
            {
                OnUnitClick?.Invoke(_lastHoverUnit);
                return; // 유닛 클릭했으면 타일 클릭은 무시
            }

            // 2. 타일 클릭 판정
            if (grid != null)
            {
                if (TryGetMapAndCell(worldPos, out Tilemap map, out Vector3Int cell))
                {
                    OnTileClick?.Invoke(map, cell); // Map 전달
                }
            }
        }

        // 우클릭 (취소)
        if (Input.GetMouseButtonDown(1))
        {
            OnCancelKeyPress?.Invoke();
        }
    }

    void HandleHotkeys()
    {
        // 취소 키
        if (Input.GetKeyDown(battle_CancelKey) || Input.GetKeyDown(KeyCode.Escape))
        {
            OnCancelKeyPress?.Invoke();
        }

        // 확정 키
        if (Input.GetKeyDown(battle_CurrentKey) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            OnConfirmKeyPress?.Invoke();
        }
        // 도주 키
        if (Input.GetKeyDown(battle_Escape))
        {
            OnEscapeKeyPress?.Invoke();
        }

        // 탭 키 (HUD 토글) - 이건 여기서 처리해도 무방
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            hudCtrl?.Toggle();
        }

        // 숫자 키 (배속)
        for (int i = 0; i < speedBinds.Length; i++)
        {
            if (Input.GetKeyDown(speedBinds[i].key))
            {
                speedCtrl.SetSpeedIndex(speedBinds[i].idx);
            }
        }
    }


    bool EnsureProviders()
    {
        // provider + 그가 제공하는 Floor 타일맵 + 카메라가 준비됐는지 확인
        bool ok = (provider != null
                           && provider.PlayerFloor != null
                           && provider.EnemyFloor != null
                           && cam != null);
        if (ok) return true;

        // provider 재획득 (현재 구조와 동일한 경로로)
        if (provider == null)
            provider = BattleMapManager.Instance as IBattleMapProvider
                                   ?? FindObjectOfType<BattleMapManager>(true);

        // 카메라 재획득
        if (cam == null) cam = Camera.main;

        _rebindTries++;
        if (_rebindTries == 1 || _rebindTries % 60 == 0)
        {
            var pf = provider != null ? provider.PlayerFloor != null : false;
            var ef = provider != null ? provider.EnemyFloor != null : false;
            Debug.Log($"[BattleInput] Rebind #{_rebindTries} -> provider:{provider != null}, PF:{pf}, EF:{ef}, cam:{cam != null}");
        }

        return (provider != null
                && provider.PlayerFloor != null
                && provider.EnemyFloor != null
                && cam != null);
    }

    public void RebindProviders()
    {
        _rebindTries = 0;     // 로그 스팸 방지
        EnsureProviders();    // 즉시 1회 재바인딩 시도
    }

    #region Input Handlers
    void HandleGameSpeedToggle()
    {
        // 모달 중이면 속도 토글도 비활성
        if (PopupManager.IsModalOpen) return;
        for (int i = 0; i < speedBinds.Length; i++)
        {
            var (key, idx) = speedBinds[i];
            if (Input.GetKeyDown(key))
            {
                speedCtrl?.SetSpeedIndex(idx);
                break;
            }
        }
    }

    //void HandleMouseInput()
    //{
    //    if (PopupManager.IsModalOpen) return;
        
    //    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
    //        return;

    //    if (!Input.GetMouseButtonDown(0)) return;

    //    hudCtrl?.Show();    //HUD가 꺼진 상태에서 마우스 클릭이 확인될 시 켜짐

    //    // 스킬 타겟팅(플레이어 턴 + Targeting + 스킬 선택됨) 중이면 레거시 우회
    //    bool canTargetSkill = (battle.IsPlayerTurn && battle.IsTargeting && battle.currentSkillSO != null);
    //    if (canTargetSkill) return;

    //    Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
    //    world.z = 0f;

    //    // 1) 유닛부터 체크 (단일 대상 클릭 우선)
    //    var hit = Physics2D.OverlapCircle(world, 0.1f, unitMask);
    //    if (hit?.TryGetComponent(out BattleUnit unit) ?? false)
    //    {
    //        battle?.OnUnitClicked(unit);
    //        return;
    //    }

    //    // 2) 타일 클릭 → 우선순위: 적맵(타게팅) → 아군맵(이동)
    //    if (TryCell(provider.EnemyFloor, world, out var enemyCell))
    //    {
    //        battle?.OnTileClicked(provider.EnemyFloor, enemyCell);
    //        return;
    //    }

    //    if (TryCell(provider.PlayerFloor, world, out var playerCell))
    //    {
    //        battle?.OnTileClicked(provider.PlayerFloor, playerCell);
    //        return;
    //    }
    //}
    #endregion

    bool HandleHudToggleEarly()
    {
        if (PopupManager.IsModalOpen) return true;

        // Tab 키로 토글
        if (Input.GetKeyDown(battle_HudKey))
        {
            hudCtrl?.Toggle();
            return true; // 같은 프레임에 다른 입력 소비 방지
        }

        // 2) HUD가 꺼져있을 때 아무 키/마우스 버튼 입력으로 즉시 복귀
        if (hudCtrl != null && !hudCtrl.IsVisible)
        {
            if (Input.GetKeyDown(battle_HudKey))
            {
                return true;
            }
            // anyKeyDown은 키 또는 마우스 버튼 눌림에 반응(스크롤/이동 제외)
            if (Input.anyKeyDown)
            {
                hudCtrl.Show();
                return true; // 같은 프레임 다른 입력 막음
            }

        }

        return false;
    }

    #region Helper
    bool TryCell(Tilemap map, Vector3 world, out Vector3Int cell)
    {
        cell = map.WorldToCell(world);
        return map.cellBounds.Contains(cell) && map.HasTile(cell);
    }
    #endregion
}
