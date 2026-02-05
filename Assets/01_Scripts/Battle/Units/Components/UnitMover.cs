using UnityEngine;
using UnityEngine.Tilemaps;
using Cysharp.Threading.Tasks;
using System;

// SRP: Handles Grid Movement and Position
public class UnitMover : MonoBehaviour
{
    public Tilemap Map { get; private set; }
    public Vector3Int Cell { get; private set; }
    
    public event Action<Vector3Int> OnMoved;

    public void Initialize(Tilemap map, Vector3Int startCell)
    {
        Map = map;
        Cell = startCell;
        transform.position = map.GetCellCenterWorld(startCell);
    }

    public async UniTask MoveToAsync(Vector3Int targetCell, float duration = 0.2f)
    {
        if (Map == null) return;

        Vector3 startPos = transform.position;
        Vector3 targetPos = Map.GetCellCenterWorld(targetCell);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, targetPos, elapsed / duration);
            await UniTask.Yield();
        }

        transform.position = targetPos;
        Cell = targetCell;
        OnMoved?.Invoke(Cell);
    }
}
