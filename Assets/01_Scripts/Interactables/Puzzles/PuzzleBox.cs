using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(PushObject))]
public class PuzzleBox : MonoBehaviour
{
    public Vector3Int CurrentCell { get; private set; }
    private Vector3Int initialCell;

    public void CacheInitialCell(Vector3Int cell, Tilemap floorMap)
    {
        initialCell = cell;
        CurrentCell = cell;
        transform.position = floorMap.GetCellCenterWorld(cell);
    }

    public void ResetToInitial(Tilemap floorMap)
    {
        CurrentCell = initialCell;
        transform.position = floorMap.GetCellCenterWorld(initialCell);
    }

    public void SetCell(Vector3Int cell, Tilemap floorMap)
    {
        CurrentCell = cell;
        Vector3 target = floorMap.GetCellCenterWorld(cell);

        StopAllCoroutines();
        StartCoroutine(AnimateMove(transform.position, target, 0.15f));
    }

    private System.Collections.IEnumerator AnimateMove(Vector3 from, Vector3 to, float duration)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.position = Vector3.Lerp(from, to, t);
            yield return null;
        }
        transform.position = to;
    }
}