using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;

public class PuzzleManager : MonoBehaviour
{
    public Tilemap FloorTilemap { get; private set; }
    public Tilemap WallTilemap { get; private set; }
    private Dictionary<Vector3Int, PuzzleBox> boxes = new();

    public bool IsPuzzleActive { get; private set; }

    private void Awake()
    {
        Shared.PuzzleManager = this;
    }

    void Start()
    {
        // 박스 초기화: 씬에 있는 모든 Box 컴포넌트 돌면서
        foreach (var box in FindObjectsOfType<PuzzleBox>())
        {
            var c = FloorTilemap.WorldToCell(box.transform.position);
            boxes[c] = box;
            box.SetCell(c, FloorTilemap);
        }
    }

    public void SetMaps(Tilemap floor, Tilemap wall)
    {
        FloorTilemap = floor;
        WallTilemap = wall;
        InitializeBoxes();    // 상자 셀 초기화
        IsPuzzleActive = true;
    }

    // FloorTilemap 위 모든 PuzzleBox를 찾아서 셀 단위로 배치하고,
    // Dictionary를 초기화
    private void InitializeBoxes()
    {
        boxes.Clear();
        foreach (var box in FindObjectsOfType<PuzzleBox>())
        {
            Vector3Int c = FloorTilemap.WorldToCell(box.transform.position);
            boxes[c] = box;
            box.SetCell(c, FloorTilemap);
        }
    }

    public void ClearMaps()
    {
        FloorTilemap = null;
        WallTilemap = null;
        boxes.Clear();
        IsPuzzleActive = false;
    }

    // 플레이어가 playerCell 위치에서 dir 방향으로 상자를 밀어내고,
    // 성공 시 true 반환
    public bool TryPush(Vector3Int playerCell, Vector3Int dir)
    {
        var boxCell = playerCell + dir;
        if (!boxes.TryGetValue(boxCell, out var box))
            return false; // 옆에 상자가 없다면 패스

        var target = boxCell + dir;
        // 1) 땅이 아니거나, 2) 벽이거나, 3) 다른 상자가 있으면 밀 수 없음
        if (!FloorTilemap.HasTile(target)
         || WallTilemap.HasTile(target)
         || boxes.ContainsKey(target))
            return false;

        // 4) 실제 밀기
        boxes.Remove(boxCell);
        boxes[target] = box;
        box.SetCell(target, FloorTilemap);
        return true;
    }
}