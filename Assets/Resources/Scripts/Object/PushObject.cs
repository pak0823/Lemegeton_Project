using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;

public class PushObject : MonoBehaviour, IExplorationPersistable
{
    private ExplorationPersistId pid;
    public Tilemap floorTilemap;
    public Tilemap wallTilemap;
    public LayerMask obstacleLayer;

    [Header("초기화 위치 정보")]
    private Vector3 initialPosition;

    [Header("하이라이트 처리")]
    public SpriteRenderer highlightRenderer;
    private Color originalColor;
    private bool isHighlighted = false;
    //public GameObject targetMarker;   //인식 표시

    public bool isPushable = true;

    private void Awake()
    {
        initialPosition = transform.position;

        pid = GetComponent<ExplorationPersistId>();
        if (!pid) pid = gameObject.AddComponent<ExplorationPersistId>();

        highlightRenderer = GetComponent<SpriteRenderer>();
        if (highlightRenderer != null)
            originalColor = highlightRenderer.color;

        // 시작 시 안내 UI OFF
        //if (targetMarker != null) targetMarker.SetActive(false);
    }

    void OnEnable()
    {
        SetHighlight(false);
    }

    public void SetHighlight(bool on)
    {
        if (highlightRenderer != null && isHighlighted != on)
        {
            highlightRenderer.color = on ?  Color.yellow : originalColor;
            isHighlighted = on;
        }

        //if (targetMarker != null)
        //    targetMarker.SetActive(on);
    }

    public bool CanBePushed(Direction dir)
    {
        if (!isPushable) return false;

        Vector3Int currentCell = floorTilemap.WorldToCell(transform.position);
        bool odd = Mathf.Abs(currentCell.y) % 2 == 1;
        Vector3Int offset = GetOffsetForDirection(dir, odd);
        Vector3Int targetCell = currentCell + offset;

        bool hasFloor = floorTilemap.HasTile(targetCell);
        bool hasWall = wallTilemap != null && wallTilemap.HasTile(targetCell);

        Vector3 worldPos = floorTilemap.GetCellCenterWorld(targetCell);
        Collider2D obstacle = Physics2D.OverlapCircle(worldPos, 0.1f, obstacleLayer);

        var hits = Physics2D.OverlapCircleAll(worldPos, 0.1f);
        bool hasOtherPushObject = hits.Any(h => h.GetComponent<PushObject>() != null && h.gameObject != this);

        Debug.Log($"[PushCheck] {name} → {offset} | Floor: {hasFloor}, Wall: {hasWall}, Blocked: {obstacle != null}, OtherPush: {hasOtherPushObject}");

        return hasFloor && !hasWall && obstacle == null && !hasOtherPushObject;
    }

    public bool TryPush(Direction dir, out Vector3Int fromCell, out Vector3Int toCell)
    {
        fromCell = floorTilemap.WorldToCell(transform.position);
        bool odd = Mathf.Abs(fromCell.y) % 2 == 1;
        Vector3Int offset = GetOffsetForDirection(dir, odd);
        toCell = fromCell + offset;

        if (!CanBePushed(dir))
            return false;
            

        return true;
    }

    private Vector3Int GetOffsetForDirection(Direction dir, bool odd)
    {
        return dir switch
        {
            Direction.West => new Vector3Int(-1, 0, 0),
            Direction.East => new Vector3Int(1, 0, 0),
            Direction.NW => odd ? new Vector3Int(0, 1, 0) : new Vector3Int(-1, 1, 0),
            Direction.NE => odd ? new Vector3Int(1, 1, 0) : new Vector3Int(0, 1, 0),
            Direction.SW => odd ? new Vector3Int(0, -1, 0) : new Vector3Int(-1, -1, 0),
            Direction.SE => odd ? new Vector3Int(1, -1, 0) : new Vector3Int(0, -1, 0),
            _ => Vector3Int.zero,
        };
    }
    public void SetTilemaps(Tilemap floor, Tilemap wall)
    {
        floorTilemap = floor;
        wallTilemap = wall;
    }

    // IExplorationPersistable
    public string PersistID => pid.Id;
    public ExplorationObjectState SaveState()
    {
        return new ExplorationObjectState
        {
            id = PersistID,
            kind = "Push",
            prefabName = gameObject.name.Replace("(Clone)", "").Trim(),
            position = transform.position
        };
    }
    public void LoadState(ExplorationObjectState s)
    {
        transform.position = s.position;
        // 내부 캐시/경로가 있다면 여기서 초기화
    }
}

