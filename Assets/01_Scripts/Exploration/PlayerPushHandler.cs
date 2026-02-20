using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerPushHandler : MonoBehaviour
{
    private PlayerMovement player;

    [Header("Push Settings")]
    [SerializeField] private GameObject pushMarkerPrefab; // Push 후보 타일 표시용

    // --- Push State Variables ---
    public bool IsPushMode { get; private set; }
    private Direction pendingDirectionKey = Direction.None;
    public bool IsPerformingPush { get; private set; }

    public bool IsPushSelectMode { get; private set; }
    public PushObject PendingPushBox { get; private set; }
    private HashSet<Vector3Int> pushValidTargetCells = new HashSet<Vector3Int>();

    private readonly List<GameObject> activePushMarkers = new List<GameObject>();

    public void Initialize(PlayerMovement playerMovement)
    {
        player = playerMovement;
    }

    public void ProcessPushObjectClick(PushObject push)
    {
        if (player.HandleGlobalClickBlocking()) return;
        if (player.IsMoving) return;

        player.CancelSelectionAndHint(); // 기존 선택/경로 정리

        PendingPushBox = push;
        PendingPushBox.SetHighlight(true);

        InteractionHintUI.Instance?.ShowPushCancelAt(PendingPushBox.transform);
    }

    public void ProcessPushTargetClick(Vector3Int clickedCell)
    {
        if (player.HandleGlobalClickBlocking()) return;

        if (!IsPushSelectMode) return;
        if (!pushValidTargetCells.Contains(clickedCell)) return;

        StartPushToCell(PendingPushBox, clickedCell);

        ExitPushSelectMode(keepBoxHighlight: false);
    }

    public void EnterPushSelectMode(PushObject box)
    {
        if (box == null || player.floorTilemap == null) return;

        player.ClearPathPreview();
        player.currentPathCells.Clear();

        IsPushSelectMode = true;
        IsPushMode = true;

        if (player.animator != null)
            player.animator.SetBool("IsPushIdle", true);

        var playerCell = player.floorTilemap.WorldToCell(player.rb.position);
        var boxCell = player.floorTilemap.WorldToCell(box.transform.position);

        var delta = boxCell - playerCell;
        bool odd = (playerCell.y & 1) != 0;
        pendingDirectionKey = GetDirectionFromDelta(delta, odd);

        pushValidTargetCells.Clear();

        var startBoxCell = player.floorTilemap.WorldToCell(box.transform.position);
        var line = BuildPushLineTargets(box, startBoxCell, pendingDirectionKey);

        for (int i = 0; i < line.Count; i++)
            pushValidTargetCells.Add(line[i]);

        if (pushValidTargetCells.Count == 0)
        {
            ExplorationLogUI.Instance?.Push("이 위치에서는 밀 수 없습니다.");
            ExitPushSelectMode(keepBoxHighlight: false);
            return;
        }

        ShowPushTargets(pushValidTargetCells);
    }

    public void ExitPushSelectMode(bool keepBoxHighlight = false)
    {
        IsPushSelectMode = false;
        IsPushMode = false;

        if (player.animator != null)
            player.animator.SetBool("IsPushIdle", false);

        ClearPushTargets();
        pushValidTargetCells.Clear();

        if (PendingPushBox != null)
        {
            if (!keepBoxHighlight)
            {
                PendingPushBox.SetHighlight(false);
                PendingPushBox = null;
            }
        }

        InteractionHintUI.Instance?.HideAll();
    }

    public Direction GetDirectionFromDelta(Vector3Int delta, bool odd)
    {
        foreach (Direction dir in System.Enum.GetValues(typeof(Direction)))
        {
            if (dir == Direction.None) continue;
            if (PathfindingSystem.Instance.GetOffsetForDirection(dir, odd) == delta)
                return dir;
        }
        return Direction.None;
    }

    public List<Vector3Int> FindPathToPushReadyCell(Vector3Int playerCell, Vector3Int boxCell, PushObject box)
    {
        Direction[] dirs =
        {
            Direction.West, Direction.East,
            Direction.NW, Direction.NE,
            Direction.SW, Direction.SE
        };

        List<Vector3Int> best = null;
        bool oddBox = (boxCell.y & 1) != 0;

        foreach (var d in dirs)
        {
            var adj = boxCell + PathfindingSystem.Instance.GetOffsetForDirection(d, oddBox);
            if (!PathfindingSystem.Instance.IsWalkableCell(adj)) continue;

            var delta = boxCell - adj;
            bool oddAdj = (adj.y & 1) != 0;
            var dirKey = GetDirectionFromDelta(delta, oddAdj);
            if (dirKey == Direction.None) continue;

            var line = BuildPushLineTargets(box, boxCell, dirKey);
            if (line.Count == 0) continue;

            var path = PathfindingSystem.Instance.FindPath(playerCell, adj);
            if (path == null || path.Count < 2) continue;

            if (best == null || path.Count < best.Count) best = path;
        }

        return best;
    }

    private List<Vector3Int> BuildPushLineTargets(PushObject box, Vector3Int startBoxCell, Direction dirKey)
    {
        var results = new List<Vector3Int>();

        if (box == null || box.MainFloorMap == null) return results;
        if (dirKey == Direction.None) return results;

        var occupied = new HashSet<Vector3Int>();
        foreach (var po in FindObjectsOfType<PushObject>())
        {
            if (po == null || po == box) continue;
            if (box.MainFloorMap != null)
            {
                occupied.Add(box.MainFloorMap.WorldToCell(po.transform.position));
            }
        }

        var cur = startBoxCell;

        while (true)
        {
            bool odd = (cur.y & 1) != 0;
            var offset = PathfindingSystem.Instance.GetOffsetForDirection(dirKey, odd);
            var next = cur + offset;

            bool hasFloor = box.HasFloorAt(next);
            bool hasWall = false;

            if (PathfindingSystem.Instance.wallMaps != null)
            {
                foreach (var wall in PathfindingSystem.Instance.wallMaps)
                {
                    if (wall.HasTile(next))
                    {
                        hasWall = true;
                        break;
                    }
                }
            }

            if (!hasFloor || hasWall) break;

            var world = box.MainFloorMap.GetCellCenterWorld(next);
            var obstacle = Physics2D.OverlapCircle(world, 0.1f, box.obstacleLayer);
            if (obstacle != null) break;

            if (occupied.Contains(next)) break;

            results.Add(next);
            cur = next;
        }
        return results;
    }

    private void StartPushToCell(PushObject box, Vector3Int targetCell)
    {
        Vector3Int currentCell = PathfindingSystem.Instance.GetCellFromWorldPos(player.rb.position);
        currentCell.z = 0;

        var boxCell = player.floorTilemap.WorldToCell(box.transform.position);

        var pathToReady = FindPathToPushReadyCell(currentCell, boxCell, box);

        if (pathToReady != null)
        {
            if (player.pathMoveRoutine != null) StopCoroutine(player.pathMoveRoutine);
            player.pathMoveRoutine = StartCoroutine(Co_MoveToPushReadyAndPush(pathToReady, box, targetCell));
        }
    }

    private IEnumerator Co_MoveToPushReadyAndPush(List<Vector3Int> pathToReady, PushObject box, Vector3Int targetCell)
    {
        player.isMovingByPath = true;
        IsPushMode = true;

        if (pathToReady.Count > 1)
        {
            yield return StartCoroutine(player.Co_MoveAlongPath(pathToReady));
        }

        Vector3Int readyCell = pathToReady[pathToReady.Count - 1];
        Vector3Int boxPos = player.floorTilemap.WorldToCell(box.transform.position);

        Vector3Int dirVec = boxPos - readyCell;
        bool odd = (readyCell.y & 1) != 0;
        Direction pushDir = GetDirectionFromDelta(dirVec, odd);

        if (pushDir != Direction.None)
        {
            yield return StartCoroutine(PerformPushToTarget(box, pushDir, targetCell));
        }

        player.isMovingByPath = false;
        IsPushMode = false;
    }

    private IEnumerator PerformPushToTarget(PushObject box, Direction dirKey, Vector3Int targetCell)
    {
        int pushedTiles = 0;
        bool reachedTarget = false;

        IsPerformingPush = true;

        if (player.animator != null)
            player.animator.SetBool("IsPushing", true);

        try
        {
            while (true)
            {
                if (box == null) yield break;

                var curCell = player.floorTilemap.WorldToCell(box.transform.position);

                if (curCell == targetCell)
                {
                    reachedTarget = true;
                    yield break;
                }

                var line = BuildPushLineTargets(box, curCell, dirKey);
                if (line.Count == 0)
                    yield break;

                var nextCell = line[0];

                if (targetCell != nextCell && !line.Contains(targetCell))
                    yield break;

                var stepDir = nextCell - curCell;

                yield return StartCoroutine(PerformPush(box, curCell, stepDir));

                pushedTiles++;
            }
        }
        finally
        {
            if (player.animator != null)
                player.animator.SetBool("IsPushing", false);

            IsPerformingPush = false;

            if (reachedTarget && pushedTiles > 0 && VigorManager.Instance != null)
            {
                int cost = pushedTiles * VigorManager.Instance.costPushBoxPerTile;
                if (cost > 0)
                    VigorManager.Instance.TrySpend(cost, VigorSpendReason.PushBox);
            }
        }
    }

    private IEnumerator PerformPush(PushObject box, Vector3Int fromCell, Vector3Int dir)
    {
        float duration = 0.2f;

        var from = fromCell;
        var to = fromCell + dir;
        var boxOdd = (from.y & 1) != 0;
        var key = GetDirectionFromDelta(to - from, boxOdd);

        var (blend, flipX) = GetPushBlend(key);

        if (player.animator != null)
        {
            player.animator.SetFloat("PushX", blend.x);
            player.animator.SetFloat("PushY", blend.y);
            player.spriterenderer.flipX = flipX;
        }

        Vector3 fromBox = box.transform.position;
        Vector3 toBox = player.floorTilemap.GetCellCenterWorld(fromCell + dir);
        Vector3 fromPlayer = player.rb.position;

        Vector3 moveDir = (toBox - fromBox).normalized;
        float offset = 0.15f;
        Vector3 pushVisualTarget = fromBox - moveDir * offset;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            box.transform.position = Vector3.Lerp(fromBox, toBox, t);
            player.rb.MovePosition(Vector3.Lerp(fromPlayer, pushVisualTarget, t));
            yield return null;
        }

        box.transform.position = toBox;
        var logicalPlayerCellCenter = player.floorTilemap.GetCellCenterWorld(fromCell);
        logicalPlayerCellCenter.z = player.transform.position.z;
        player.rb.MovePosition(logicalPlayerCellCenter);

        // 상자 이동 완료 후 PathfindingSystem 점유 좌표 갱신
        box.UpdateObstaclePosition();

        var boxArrivedCell = fromCell + dir;

        player.InteractionHandler.TryConsumeTrapByBoxAtCell(boxArrivedCell);
    }

    private (Vector2 blend, bool flipX) GetPushBlend(Direction dir)
    {
        return dir switch
        {
            Direction.NW => (new Vector2(1f, 1f), false),
            Direction.NE => (new Vector2(1f, 1f), true),
            Direction.SW => (new Vector2(1f, -1f), false),
            Direction.SE => (new Vector2(1f, -1f), true),
            Direction.West => (new Vector2(1f, 0f), false),
            Direction.East => (new Vector2(1f, 0f), true),
            _ => (Vector2.zero, false)
        };
    }

    private void ShowPushTargets(IEnumerable<Vector3Int> cells)
    {
        ClearPushTargets();

        if (pushMarkerPrefab == null) return;

        foreach (var c in cells)
        {
            var world = player.floorTilemap.GetCellCenterWorld(c);
            world.z = player.transform.position.z;

            var marker = Instantiate(pushMarkerPrefab, world, Quaternion.identity);
            activePushMarkers.Add(marker);
        }
    }

    public void ClearPushTargets()
    {
        for (int i = 0; i < activePushMarkers.Count; i++)
            if (activePushMarkers[i]) Destroy(activePushMarkers[i]);

        activePushMarkers.Clear();
    }

    public void HaltPushImmediately()
    {
        if (IsPushSelectMode)
            ExitPushSelectMode(keepBoxHighlight: false);
        else if (PendingPushBox != null)
        {
            PendingPushBox.SetHighlight(false);
            PendingPushBox = null;
            ClearPushTargets();
            pushValidTargetCells.Clear();
            IsPushMode = false;
        }
    }

    public bool IsAdjacentOrSame(Vector3Int a, Vector3Int b)
    {
        if (a == b) return true;

        Direction[] dirs =
        {
            Direction.West, Direction.East,
            Direction.NW, Direction.NE,
            Direction.SW, Direction.SE
        };

        bool odd = (b.y & 1) != 0;
        foreach (var dir in dirs)
        {
            var off = PathfindingSystem.Instance.GetOffsetForDirection(dir, odd);
            if (b + off == a) return true;
        }
        return false;
    }
}
