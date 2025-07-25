using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PuzzleBox : MonoBehaviour
{
    public Vector3Int cell;
    public void SetCell(Vector3Int c, Tilemap floor)
    {
        cell = c;
        transform.position = floor.GetCellCenterWorld(c);
    }
}
