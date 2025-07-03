using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class test : MonoBehaviour
{
    public Tilemap tilemap;

    // Start is called before the first frame update
    private void Update()
    {
        Debug.Log(tilemap.GetCellCenterWorld(Vector3Int.zero));
    }
}
