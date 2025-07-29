using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using static UnityEngine.GraphicsBuffer;

public class PuzzleBox : MonoBehaviour
{
    // 현재 셀
    public Vector3Int CurrentCell { get; private set; }
    // 초기 셀
    private Vector3Int initialCell;

    /// 초기 위치(셀)와 월드 좌표를 기록
    /// InitializeBoxes 호출 시 반드시 한 번만 세팅
    public void CacheInitialCell(Vector3Int cell, Tilemap floorMap)
    {
        initialCell = cell;
        CurrentCell = cell;
        // 월드 위치도 맞춰줌
        transform.position = floorMap.GetCellCenterWorld(cell);
    }

    // 새로운 셀로 이동
    public void SetCell(Vector3Int cell, Tilemap floorMap)
    {
        CurrentCell = cell;
        //transform.position = floorMap.GetCellCenterWorld(cell);
        Vector3 target = floorMap.GetCellCenterWorld(cell);

        // 기존 이동 코루틴 중단
        StopAllCoroutines();
        // 새로운 이동 코루틴 실행 (0.15초 동안 Lerp)
        StartCoroutine(AnimateMove(transform.position, target, 0.15f));

        //Debug.Log("현재 이동된 Box 오브젝트: " + this.gameObject.name);
    }

    // 초기 셀로 되돌림
    public void ResetToInitial(Tilemap floorMap)
    {
        CurrentCell = initialCell;
        transform.position = floorMap.GetCellCenterWorld(initialCell);
    }

    // 박스 이동 시 부드럽게 위치를 변경하는 코루틴
    private IEnumerator AnimateMove(Vector3 from, Vector3 to, float duration)
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
