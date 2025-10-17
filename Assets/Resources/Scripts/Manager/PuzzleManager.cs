using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class PuzzleManager : MonoBehaviour
{
    public Tilemap FloorTilemap { get; private set; }
    public Tilemap WallTilemap { get; private set; }
    public GameObject portalPrefab;
    public Button resetBtn;

    private List<PuzzleBox> puzzleBoxes = new();
    private List<BoxGoal> goals = new();
    private Vector3 initialPlayerWorld;

    public bool IsPuzzleComplete { get; private set; } = false;
    public bool IsPuzzleActive { get; set; } = false;

    void Awake()
    {
        Shared.PuzzleManager = this;
        resetBtn?.gameObject.SetActive(false);
    }

    public void SetMaps(Tilemap floor, Tilemap wall)
    {
        FloorTilemap = floor;
        WallTilemap = wall;

        InitBoxes();
        InitGoals();
        IsPuzzleComplete = false;

        var timer = FindObjectOfType<ExplorationTimerUi>(true);
        if (timer != null)
        {
            timer.OnUiHidden();
        }

        if (IsPuzzleActive)
            resetBtn?.gameObject.SetActive(true);


    }

    void InitBoxes()
    {
        puzzleBoxes.Clear();
        foreach (var box in FindObjectsOfType<PuzzleBox>())
        {
            var cell = FloorTilemap.WorldToCell(box.transform.position);
            puzzleBoxes.Add(box);
            box.CacheInitialCell(cell, FloorTilemap);
        }
    }

    void InitGoals()
    {
        if (!IsPuzzleActive) return;

        goals.Clear();
        foreach (var goal in FindObjectsOfType<BoxGoal>())
        {
            goal.Init(FloorTilemap);
            goals.Add(goal);
        }
        CheckGoals();
    }

    public void ExecutePush(PushObject box, Vector3Int fromCell, Vector3Int toCell)
    {
        if (!IsPuzzleActive || IsPuzzleComplete) return;

        // 밀린 대상이 PuzzleBox일 경우에만 처리
        if (box.TryGetComponent<PuzzleBox>(out var puzzleBox))
        {
            puzzleBox.SetCell(toCell, FloorTilemap);
            CheckGoals();
        }
    }

    public void CheckGoals()
    {
        foreach (var goal in goals)
        {
            bool hasBoxOnGoal = puzzleBoxes.Any(box =>
            {
                Vector3Int boxCell = FloorTilemap.WorldToCell(box.transform.position);
                return boxCell == goal.Cell;
            });
            goal.SetActive(hasBoxOnGoal);
        }

        if (goals.All(g => g.IsActive) && goals.Count > 0)
            OnPuzzleComplete();
    }

    void OnPuzzleComplete()
    {
        if (IsPuzzleComplete) return;

        IsPuzzleComplete = true;
        Instantiate(portalPrefab, initialPlayerWorld, Quaternion.identity);

        foreach (var box in puzzleBoxes)
        {
            var push = box.GetComponent<PushObject>();
            if (push != null) push.isPushable = false; // 밀기 차단
        }

        resetBtn?.gameObject.SetActive(false);

        Debug.Log("[PuzzleManager] 모든 박스 목표 달성 → 포탈 생성 완료");
    }

    public void ResetPuzzle()
    {
        if (IsPuzzleComplete) return;

        foreach (var box in puzzleBoxes)
            box.ResetToInitial(FloorTilemap);

        Shared.PlayerMovement.TeleportTo(initialPlayerWorld);
        InitGoals();
    }

    public void ClearMaps()
    {
        FloorTilemap = null;
        WallTilemap = null;
        puzzleBoxes.Clear();
        goals.Clear();
        IsPuzzleActive = false;
    }

    public void CacheInitialPlayerPosition(Vector3 pos)
    {
        initialPlayerWorld = pos;
    }
}