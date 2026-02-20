using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Tilemaps;

public class BattleInputHandler : MonoBehaviour
{
    [Header("Manager References")]
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private BattleInput battleInput;

    [Header("Visual Feedback")]
    public TargetMarker targetMarker;
    public Highlighter moveHighlighter;
    public Highlighter skillHighlighter;

    // 타겟 순환 관리
    private List<BattleUnit> _targetCycle = new();
    private int _targetIndex = -1;
    private BattleUnit _selectedTarget;

    // 내부 상태
    private int _skillPreviewHold = 0;
    private Tilemap _customPreviewMap;
    private HashSet<Vector3Int> _customPreviewCells;

    // 넉백/이동 선택 콜백용
    private System.Action<Vector3Int?> _onCellSelectedCallback;
    private Tilemap _selectionMap;
    private List<Vector3Int> _selectionCandidates;

    public void Initialize(BattleManager bm)
    {
        this.battleManager = bm;
        if (battleInput == null) battleInput = FindObjectOfType<BattleInput>();

        if (battleInput != null)
        {
            battleInput.OnUnitClick += OnUnitClick;
            battleInput.OnTileClick += OnTileClick;
            battleInput.OnUnitHover += OnUnitHover;
            battleInput.OnTileHover += OnTileHover;
            battleInput.OnCancelKeyPress += OnCancelAction;
            battleInput.OnEscapeKeyPress += OnEscapeAction;
        }
    }

    private void OnDestroy()
    {
        if (battleInput != null)
        {
            battleInput.OnUnitClick -= OnUnitClick;
            battleInput.OnTileClick -= OnTileClick;
            battleInput.OnUnitHover -= OnUnitHover;
            battleInput.OnTileHover -= OnTileHover;
            battleInput.OnCancelKeyPress -= OnCancelAction;
            battleInput.OnEscapeKeyPress -= OnEscapeAction;
        }
    }

    #region Input Event Listeners
    private void OnUnitClick(BattleUnit unit)
    {
        if (battleManager.state == BattleState.Resolving) return;

        if (battleManager.state == BattleState.Targeting && battleManager.CurrentSkillSO != null)
        {
            if (battleManager.CurrentSkillSO.targetMode == SkillTargetMode.Unit)
            {
                if (battleManager.IsValidSkillTarget(unit))
                    battleManager.ConfirmSkillOnUnit(unit);
                else
                    Debug.Log("유효하지 않은 대상입니다.");
                return;
            }
            else if (battleManager.CurrentSkillSO.targetMode == SkillTargetMode.Tile)
            {
                battleManager.ConfirmSkillOnTile(unit.CurrentMap, unit.Cell);
                return;
            }
        }
        battleManager.ShowUnitInfo(unit);
    }

    private void OnTileClick(Tilemap map, Vector3Int cell)
    {
        if (battleManager.state == BattleState.Moving)
        {
            battleManager.ProcessMoveCommand(map, cell);
            return;
        }

        if (battleManager.state == BattleState.Targeting)
        {
            if (battleManager.CurrentSkillSO != null
                && battleManager.CurrentSkillSO.targetMode == SkillTargetMode.Tile)
            {
                // [수정] 맵 유효성 검사 추가 (ITargetMapProvider 지원)
                if (!IsValidMapForSkill(map, battleManager.CurrentSkillSO))
                {
                    Debug.Log($"[Input] Invalid map for skill: {map.name}");
                    return;
                }

                battleManager.ConfirmSkillOnTile(map, cell);
                return;
            }
        }

        if (battleManager.state == BattleState.TargetingKnockback)
        {
            HandleCellSelectionClick(map, cell);
        }
    }

    private void OnUnitHover(BattleUnit unit)
    {
        if (unit == null || battleManager.state != BattleState.Targeting || battleManager.CurrentSkillSO == null)
        {
            targetMarker?.Hide();
            if (battleManager.CurrentSkillSO == null || battleManager.CurrentSkillSO.targetMode == SkillTargetMode.Unit)
                ClearSkillPreview();
            return;
        }

        if (battleManager.CurrentSkillSO.targetAlignment == SkillTargetAlignment.Self)
        {
            targetMarker?.Hide();
            return;
        }

        if (battleManager.CurrentSkillSO.targetMode == SkillTargetMode.Unit)
        {
            if (battleManager.IsValidSkillTarget(unit))
            {
                targetMarker?.Attach(unit);
                PreviewSkillAreaOnUnit(unit);
            }
            else
            {
                targetMarker?.Hide();
                ClearSkillPreview();
            }
        }
    }

    private void OnTileHover(Tilemap map, Vector3Int cell)
    {
        if (battleManager.state != BattleState.Targeting || battleManager.CurrentSkillSO == null || map == null) return;

        if (battleManager.CurrentSkillSO.targetMode == SkillTargetMode.Tile)
        {
            if (battleManager.CurrentSkillSO is ParametricDirectionSkill) return;

            // [수정] ITargetMapProvider 우선 검사
            if (!IsValidMapForSkill(map, battleManager.CurrentSkillSO))
            {
                skillHighlighter?.ClearTransient();
                return;
            }

            bool isMapValid = false;
            var skill = battleManager.CurrentSkillSO;

            if (skill.targetAlignment == SkillTargetAlignment.Enemy && map.name.Contains("Enemy")) isMapValid = true;
            else if (skill.targetAlignment == SkillTargetAlignment.Ally && map.name.Contains("Player")) isMapValid = true;
            else if (skill.targetAlignment == SkillTargetAlignment.Any) isMapValid = true;

            if (isMapValid) PreviewSkillAreaOnTile(map, cell);
            else skillHighlighter?.ClearTransient(); // ClearTransient -> Clear로 안전하게 변경
        }
    }
    private void OnEscapeAction()
    {
        // BattleManager에 이미 구현된 도주 로직 호출
        battleManager.OnClickEscape().Forget();
    }

    public void OnCancelAction()
    {
        battleManager.CancelCurrentAction();
    }
    #endregion

    #region Visualization & Highlighting
    public void ShowMoveOptions(Tilemap map, IEnumerable<Vector3Int> cells)
    {
        moveHighlighter?.ShowCells(map, cells);
    }

    public void ClearMovePreview() => moveHighlighter?.ClearTransient();

    public void ShowSkillPreview(Tilemap map, IEnumerable<Vector3Int> cells)
    {
        if (_customPreviewCells != null && _customPreviewCells.Count > 0)
        {
            if (!object.ReferenceEquals(cells, _customPreviewCells)) return;
        }
        skillHighlighter?.ShowCells(map, cells);
    }

    // BM이 호출하는 프리뷰 홀드 메서드
    public void HoldSkillPreview() => _skillPreviewHold++;
    public void ReleaseSkillPreview() => _skillPreviewHold = Mathf.Max(0, _skillPreviewHold - 1);

    public void ClearSkillPreview()
    {
        if (_skillPreviewHold == 0)
        {
            skillHighlighter?.ClearTransient(); // 맵 하이라이트 끄기 (Highlighter API에 따라 Clear() 또는 ClearTransient())
            battleManager.ClearStatusPanelHighlights(); // [추가] UI 하이라이트 끄기
        }
    }

    public void ClearAllPreviews()
    {
        ClearMovePreview();
        ClearSkillPreview();
        targetMarker?.Hide();
        _selectedTarget = null;
    }

    public void PreviewSkillAreaOnUnit(BattleUnit unit)
    {
        if (battleManager.CurrentSkillSO == null) return;

        var origin = unit.Cell;
        bool isOdd = (origin.y % 2) != 0;
        var cells = battleManager.CurrentSkillSO.GetAreaCells(origin, isOdd);
        var map = unit.CurrentMap;

        var validCells = cells.Where(c => map.HasTile(c)).ToList();
        if (validCells.Count > 0)
        {
            skillHighlighter?.ShowCells(map, validCells);
            battleManager.HighlightUnitsInArea(map, validCells);
        }
    }

    public void PreviewSkillAreaOnTile(Tilemap map, Vector3Int cell)
    {
        if (_customPreviewCells != null) return;
        if (battleManager.CurrentSkillSO == null) return;

        bool isOdd = (cell.y % 2) != 0;
        var cells = battleManager.CurrentSkillSO.GetAreaCells(cell, isOdd);
        var validCells = cells.Where(c => map.HasTile(c)).ToList();

        if (validCells.Count > 0)
        {
            skillHighlighter?.ShowCells(map, validCells);
            battleManager.HighlightUnitsInArea(map, validCells);
        }
    }
    #endregion

    #region Knockback / Cell Selection Mode
    public void StartCellSelectionMode(Tilemap map, List<Vector3Int> candidates, System.Action<Vector3Int?> callback)
    {
        _selectionMap = map;
        _selectionCandidates = candidates;
        _onCellSelectedCallback = callback;
        ShowSkillPreview(map, candidates);
    }

    private void HandleCellSelectionClick(Tilemap map, Vector3Int cell)
    {
        if (_selectionMap == null || _selectionCandidates == null) return;
        if (map != _selectionMap) return;
        if (!_selectionCandidates.Contains(cell)) return;

        _onCellSelectedCallback?.Invoke(cell);
        EndCellSelectionMode();
    }

    public void EndCellSelectionMode()
    {
        _selectionMap = null;
        _selectionCandidates = null;
        _onCellSelectedCallback = null;
        ClearSkillPreview();
    }

    public void CancelCellSelection()
    {
        _onCellSelectedCallback?.Invoke(null);
        EndCellSelectionMode();
    }
    #endregion

    #region Target Cycle (Tab/Auto Target)
    public void BuildTargetCycle(List<BattleUnit> units)
    {
        _targetCycle = units;
        _targetIndex = -1;
        _selectedTarget = null;
    }

    public void CycleTarget(int direction)
    {
        if (_targetCycle.Count == 0) return;
        _targetIndex = (_targetIndex + direction + _targetCycle.Count) % _targetCycle.Count;
        _selectedTarget = _targetCycle[_targetIndex];

        targetMarker?.Attach(_selectedTarget);
        if (battleManager.CurrentSkillSO != null && battleManager.CurrentSkillSO.targetMode == SkillTargetMode.Unit)
        {
            PreviewSkillAreaOnUnit(_selectedTarget);
        }
    }

    public BattleUnit GetSelectedTarget() => _selectedTarget;
    #endregion

    #region Targeting Commands
    public void PrepareSkillTargeting(SkillAsset skill, BattleUnit caster)
    {
        ClearAllPreviews();

        if (skill is ISkillCustomPreview customPreview)
        {
            var cells = customPreview.GetPreviewCells(battleManager, caster);
            ShowSkillPreview(caster.CurrentMap, cells);
        }

        if (skill.targetMode == SkillTargetMode.Unit)
        {
            var targets = battleManager.GetValidTargetsForCycle(skill, caster);
            BuildTargetCycle(targets);
            if (targets.Count > 0) CycleTarget(1);
        }
        else if (skill.targetMode == SkillTargetMode.Tile)
        {
            _targetIndex = -1;
            _selectedTarget = null;
        }
    }

    public bool TrySelectTarget(BattleUnit unit)
    {
        if (_targetCycle == null || !_targetCycle.Contains(unit)) return false;

        _selectedTarget = unit;
        _targetIndex = _targetCycle.IndexOf(unit);

        targetMarker?.Attach(unit);
        PreviewSkillAreaOnUnit(unit);
        return true;
    }

    [Header("Special Highlighters")]
    public Highlighter beastDomainHighlighter;

    // [New] ITargetMapProvider 검사 헬퍼
    private bool IsValidMapForSkill(Tilemap map, SkillAsset skill)
    {
        if (skill is ITargetMapProvider provider)
        {
            var targetMap = provider.GetTargetMap(battleManager, battleManager.ActingUnit);
            // provider가 특정 맵을 요구하면 그것만 허용
            if (targetMap != null && map != targetMap) return false;
        }
        return true;
    }
    #endregion
}
