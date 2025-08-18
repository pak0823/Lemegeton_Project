using UnityEngine;
using UnityEngine.Tilemaps;

public class BattleInput : MonoBehaviour
{
    #region Inspector References
    public Camera cam;
    public BattleManager battle;
    public LayerMask unitMask;
    public GameSpeedController speedCtrl; // 인스펙터에 GameSpeedController 할당
    #endregion

    #region Internal References
    IBattleMapProvider provider;
    #endregion

    #region Unity Callbacks
    void Awake()
    {
        if (cam == null) cam = Camera.main;
        provider = Shared.battleMapManager as IBattleMapProvider ?? FindObjectOfType<BattleMapManager>();
    }

    void Start()
    {
        if (provider == null)
            provider = Shared.battleMapManager as IBattleMapProvider ?? FindObjectOfType<BattleMapManager>();
    }

    void Update()
    {
        // 맵 준비 전이면 입력 무시
        if (provider == null || provider.PlayerFloor == null || provider.EnemyFloor == null) return;

        HandleGameSpeedToggle();
        if (GamePause.IsPaused) return;

        HandleMouseInput();
        HandleTargetCycleInput();
        HandleActionShortcuts();
    }
    #endregion

    #region Input Handlers
    void HandleGameSpeedToggle()
    {
        // === 배속 토글 ===
        if (Input.GetKeyDown(KeyCode.F2))
            speedCtrl?.ToggleSpeed();
    }

    void HandleMouseInput()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
        world.z = 0f;

        // 1) 유닛부터 체크 (단일 대상 클릭 우선)
        var hit = Physics2D.OverlapCircle(world, 0.15f, unitMask);
        if (hit?.TryGetComponent(out BattleUnit unit) ?? false)
        {
            battle?.OnUnitClicked(unit);
            return;
        }

        // 2) 타일 클릭 → 우선순위: 적맵(타게팅) → 아군맵(이동)
        if (TryCell(provider.EnemyFloor, world, out var enemyCell))
        {
            battle?.OnTileClicked(provider.EnemyFloor, enemyCell);
            return;
        }

        if (TryCell(provider.PlayerFloor, world, out var playerCell))
        {
            battle?.OnTileClicked(provider.PlayerFloor, playerCell);
            return;
        }
    }

    void HandleTargetCycleInput()
    {
        // === 타겟 사이클링/확정 ===
        // 공격 타게팅 상태일 때만 반응 (BattleManager 내부에서도 가드)
        if (Input.GetKeyDown(KeyCode.RightArrow)) battle?.CycleTarget(+1); // 빠른→느린
        if (Input.GetKeyDown(KeyCode.LeftArrow)) battle?.CycleTarget(-1); // 느린→빠른
        if (Input.GetKeyDown(KeyCode.C)) battle?.ConfirmTarget();   // 결정(확정)
    }

    void HandleActionShortcuts()
    {
        // 우클릭/ESC로 취소(선택사항)
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.X))
        {
            battle?.CancelCurrentAction();
        }

        // === 액션 단축키 (플레이어 턴에만 동작; 내부에서도 가드) ===
        if (Input.GetKeyDown(KeyCode.W))
        {
            battle?.CancelCurrentAction();
            battle?.OnClickAttack(); // 공격
        }

        if (Input.GetKeyDown(KeyCode.Z)) // 이동
        {
            battle?.CancelCurrentAction();
            battle?.OnClickMove();
        }

        if (Input.GetKeyDown(KeyCode.E)) // 턴 종료
        {
            battle?.CancelCurrentAction();
            battle?.OnClickEndTurn(); // 수동 종료(회복 판단용)
        }
    }
    #endregion

    #region Helper
    bool TryCell(Tilemap map, Vector3 world, out Vector3Int cell)
    {
        cell = map.WorldToCell(world);
        return map.cellBounds.Contains(cell) && map.HasTile(cell);
    }
    #endregion
}
