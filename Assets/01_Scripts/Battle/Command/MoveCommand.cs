using UnityEngine;
using Cysharp.Threading.Tasks;

public class MoveCommand : ICommand
{
    private readonly BattleUnit _unit;
    private readonly Vector3Int _targetCell;

    public MoveCommand(BattleUnit unit, Vector3Int targetCell)
    {
        _unit = unit;
        _targetCell = targetCell;
    }

    public async UniTask ExecuteAsync()
    {
        if (_unit == null || _unit.Mover == null) return;
        
        // Use the decomposed Mover component
        // Note: BattleUnit.Mover is exposed from Phase 2
        await _unit.Mover.MoveToAsync(_targetCell);
    }
}
