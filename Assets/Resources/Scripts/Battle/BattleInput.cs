using Unity.Burst.CompilerServices;
using UnityEngine;
using UnityEngine.Tilemaps;

public class BattleInput : MonoBehaviour
{
    #region Inspector References
    public Camera cam;
    public BattleManager battle;
    public LayerMask unitMask;
    public GameSpeedController speedCtrl; // GameSpeedController 할당
    public HudController hudCtrl;   //HUD 컨트롤러 할당

    bool wasTargetingPrev = false;
    bool suppressLMBOnce = false;
    #endregion

    #region Internal References
    IBattleMapProvider provider;
    #endregion

    // 배속 키 바인딩을 테이블로 정의
    readonly (KeyCode key, int idx)[] speedBinds = new (KeyCode, int)[] {
    (KeyCode.Alpha1, 0),   // x1
    (KeyCode.Alpha2, 1),   // x2
    (KeyCode.Alpha3, 2),   // x3
    (KeyCode.BackQuote, 3) // 정지(물결/백틱 키)
};

    #region Unity Callbacks
    void Awake()
    {
        if (cam == null) cam = Camera.main;
        provider = Shared.battleMapManager as IBattleMapProvider ?? FindObjectOfType<BattleMapManager>();

        if (Shared.battleInput == null) Shared.battleInput = this;
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
        if (HandleHudToggleEarly()) return; //HUD 상태 관리
        if (GamePause.IsPaused) return;

        HandleMouseInput();
        HandleActionShortcuts();
    }
    #endregion

    #region Input Handlers
    void HandleGameSpeedToggle()
    {
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

    void HandleMouseInput()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        hudCtrl?.Show();    //HUD가 꺼진 상태에서 마우스 클릭이 확인될 시 켜짐

        // 스킬 타겟팅(플레이어 턴 + Targeting + 스킬 선택됨) 중이면 레거시 우회
        bool canTargetSkill = (battle.IsPlayerTurn && battle.IsTargeting && battle.currentSkillSO != null);
        if (canTargetSkill) return;

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

    void HandleActionShortcuts()
    {
        // === 스킬 관련 입력 공통 게이트 ===
        bool canSelectSkillNow = (battle != null
            && battle.IsPlayerTurn
            && battle.isSelectingSkill);      // Attack으로 패널 열린 상태
        bool canTargetSkill = (battle != null
            && battle.IsPlayerTurn
            && battle.IsTargeting
            && battle.currentSkillSO != null);

        bool isTargetingNow = canTargetSkill;

        if (!wasTargetingPrev && isTargetingNow)
        {
            // 스킬 선택으로 Targeting 들어온 '첫 프레임' → 그 프레임의 좌클릭은 무시
            suppressLMBOnce = true;
        }
        if (!Input.GetMouseButton(0))
        {
            // 마우스 버튼이 올라간 뒤에야 다음 좌클릭을 허용
            if (suppressLMBOnce) suppressLMBOnce = false;
        }
        wasTargetingPrev = isTargetingNow;

        // 우클릭/Q로 취소(선택사항)
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Q))
        {
            // 스킬 타겟팅 중(선택됨) → '스킬만 해제', 패널 유지 (1단계 취소)
            if (canTargetSkill)
            {
                battle.CancelCurrentAction(); // 내부에서 스킬만 초기화 & 패널 유지
                return;
            }
            // 타겟팅 아님 + 스킬 패널 열림 → 패널 닫기 (2단계 취소)
            if (battle != null && battle.isSelectingSkill)
            {
                battle.ClearSkillPreview();
                battle.CloseSkillPanel();
                return;
            }
            // 그 외(이동 중 등) 일반 취소
            battle?.CancelCurrentAction();
        }

        if (Input.GetKeyDown(KeyCode.F1))   // 도망가기(F1)
        {
            battle?.CancelCurrentAction(); // 진행 중이던 선택 취소
            battle?.OnClickEscape();       // 전투 즉시 종료 & 복귀
        }

        // === E 키로 확정 (Unit 스킬일 때만) ===
        if (canTargetSkill && battle.currentSkillSO.targetMode == SkillTargetMode.Unit && Input.GetKeyDown(KeyCode.E))
        {
            battle.ConfirmTarget();
        }
        else if (canTargetSkill && battle.currentSkillSO.targetMode == SkillTargetMode.Tile && Input.GetKeyDown(KeyCode.E))
        {
            var map = (Shared.battleMapManager as IBattleMapProvider)?.EnemyFloor;
            if (map != null) battle.ConfirmSkillOnTile(map, battle.selectedCell);
        }

        // === 스킬 타겟팅 중 호버 미리보기 ===
        if (canTargetSkill)
        {
            Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
            world.z = 0f;

            if (battle.currentSkillSO != null && battle.currentSkillSO.targetMode == SkillTargetMode.Unit)
            {
                // 마우스가 가리키는 적 유닛이 있다면 '선택' 자체를 그 유닛으로 갱신
                var hit = Physics2D.Raycast(world, Vector2.zero, 0.01f, unitMask);
                var unit = hit.collider ? hit.collider.GetComponentInParent<BattleUnit>() : null;
                if (unit != null && unit.team == Team.Enemy)
                {
                    // targetCycle에 포함된 적일 때만 선택 변경(내부에서 index 찾아 SelectTarget 호출)
                    if (!battle.SelectTargetByUnit(unit))
                    {
                        // 사이클에 없다면(필요 시) 미리보기만 보여주고 선택은 유지하고 싶다면 아래 한 줄 유지
                        battle.PreviewSkillAreaOnUnit(unit);
                    }
                    // SelectTargetByUnit()이 성공하면 마커+하이라이트는 내부에서 이미 갱신됨
                }
                else
                {
                    // 마우스가 비었거나 아군이면, 현재 선택 유지(원하면 미리보기도 유지)
                    if (battle.SelectedTarget != null)
                        battle.PreviewSkillAreaOnUnit(battle.SelectedTarget);
                    else
                        battle.ClearSkillPreview();
                }
            }
            else
            {
                var map = (Shared.battleMapManager as IBattleMapProvider)?.EnemyFloor;
                if (map != null)
                {
                    if (TryCell(map, world, out var cell))
                    {
                        // 내부 커서만 동기화 + 프리뷰만
                        battle.selectedCell = cell;
                        battle.PreviewSkillAreaOnTile(map, cell); // ← 프리뷰
                    }
                }
            }
        }
        if (canTargetSkill && !suppressLMBOnce && Input.GetMouseButtonDown(0)) // 좌클릭 확정(첫 클릭 무시)
        {
            Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
            world.z = 0f;

            if (battle.currentSkillSO != null && battle.currentSkillSO.targetMode == SkillTargetMode.Unit)
            {
                // 유닛 레이캐스트로 확정
                var hit = Physics2D.Raycast(world, Vector2.zero, 0.01f, unitMask);
                if (hit.collider != null)
                {
                    var unit = hit.collider.GetComponentInParent<BattleUnit>();
                    if (unit != null)
                    {
                        battle.ConfirmSkillOnUnit(unit);
                        return;
                    }
                }
            }
            else // SkillTargetMode.Tile
            {
                var map = (Shared.battleMapManager as IBattleMapProvider)?.EnemyFloor;
                if (map != null)
                {
                    if (TryCell(map, world, out var cell))
                    {
                        battle.selectedCell = cell; // 내부 커서 동기화
                        battle.ConfirmSkillOnTile(map, cell);     // 확정
                        return;
                    }
                    // 타일맵 밖 클릭이면 아무 것도 하지 않음(실행 X)
                }
            }
        }
    }
    #endregion

    bool HandleHudToggleEarly()
    {
        // 1) LeftControl로 토글
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            hudCtrl?.Toggle();
            return true; // 같은 프레임에 다른 입력 소비 방지
        }

        // 2) HUD가 꺼져있을 때 아무 키/마우스 버튼 입력으로 즉시 복귀
        if (hudCtrl != null && !hudCtrl.IsVisible)
        {
            if (Input.GetKeyDown(KeyCode.Tab) /*|| Input.GetKeyDown(KeyCode.Escape)*/)
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
