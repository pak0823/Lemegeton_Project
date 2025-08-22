using Unity.Burst.CompilerServices;
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

        // 스킬 타겟팅(플레이어 턴 + Targeting + 스킬 선택됨) 중이면 레거시 우회
        bool canTargetSkill = (battle != null
                              && battle.IsPlayerTurn
                              && battle.IsTargeting
                              && battle.currentSkill.GetAreaCells != null);
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
                   && battle.IsTargeting                // Targeting 상태
                   && battle.currentSkill.GetAreaCells != null); // 스킬 선택됨

        // 우클릭/X로 취소(선택사항)
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.X))
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
                battle.ClearPreview();
                battle.CloseSkillPanel();
                return;
            }
            // 그 외(이동 중 등) 일반 취소
            battle?.CancelCurrentAction();
        }

        // === 액션 단축키 (플레이어 턴에만 동작; 내부에서도 가드) ===
        if (Input.GetKeyDown(KeyCode.W))// 공격(W)
        {
            battle?.CancelCurrentAction();
            battle?.OnClickAttack(); 
        }

        if (Input.GetKeyDown(KeyCode.Z)) // 이동(Z)
        {
            battle?.CancelCurrentAction();
            battle?.CloseSkillPanel();   // 입력단에서도 한 번 더 닫기
            battle?.OnClickMove();
        }

        if (Input.GetKeyDown(KeyCode.E)) // 턴 종료(E)
        {
            battle?.CancelCurrentAction();
            battle?.OnClickEndTurn(); // 수동 종료(회복 판단용)
        }

        if (Input.GetKeyDown(KeyCode.F1))   // 도망가기(F1)
        {
            battle?.CancelCurrentAction(); // 진행 중이던 선택 취소
            battle?.OnClickEscape();       // 전투 즉시 종료 & 복귀
        }

        if (canSelectSkillNow && Input.GetKeyDown(KeyCode.Alpha1)) { battle?.SelectSkill(0); }
        if (canSelectSkillNow && Input.GetKeyDown(KeyCode.Alpha2)) { battle?.SelectSkill(1); }
        if (canSelectSkillNow && Input.GetKeyDown(KeyCode.Alpha3)) { battle?.SelectSkill(2); }
        if (canSelectSkillNow && Input.GetKeyDown(KeyCode.Alpha4)) { battle?.SelectSkill(3); }
        if (canSelectSkillNow && Input.GetKeyDown(KeyCode.Alpha5)) { battle?.SelectSkill(4); }

        // === C 키로 확정 (Unit 스킬일 때만) ===
        if (canTargetSkill && battle.currentSkill.targetMode == SkillTargetMode.Unit && Input.GetKeyDown(KeyCode.C))
        {
            battle.ConfirmTarget(); // BattleManager 쪽에서 스킬 확정으로 연결되도록 이미 수정한 그 함수
        }

        // === 방향키로 타겟 순환 (Unit 스킬일 때만) ===
        if (canTargetSkill && battle.currentSkill.targetMode == SkillTargetMode.Unit)
        {
            if (Input.GetKeyDown(KeyCode.RightArrow))
                battle.CycleTarget(+1);    // AGI 내림차순 리스트에서 다음 대상
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
                battle.CycleTarget(-1);    // 역방향
        }

        // === 스킬 타겟팅 중 호버 미리보기 ===
        if (canTargetSkill)
        {
            Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
            world.z = 0f;

            if (battle.currentSkill.targetMode == SkillTargetMode.Unit)
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
                        battle.ClearPreview();
                }
            }
            else
            {
                var map = (Shared.battleMapManager as IBattleMapProvider)?.EnemyFloor;

                if (map != null && TryCell(map, world, out var cell))
                    battle.PreviewSkillAreaOnTile(map, cell);
                else
                    battle.ClearPreview();
            }
        }

            if (canTargetSkill && Input.GetMouseButtonDown(0)) // 좌클릭 확정
        {
            Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
            world.z = 0f;

            if (battle.currentSkill.targetMode == SkillTargetMode.Unit)
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
                // 현재 전장 타일맵을 기준으로 셀 확정
                var map = (Shared.battleMapManager as IBattleMapProvider)?.EnemyFloor;

                if (map != null && TryCell(map, world, out var cell))
                {
                    battle.ConfirmSkillOnTile(map, cell);
                    return;
                }
            }
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
