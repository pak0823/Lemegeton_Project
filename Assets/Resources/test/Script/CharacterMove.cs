using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.Linq;

public class PlayerMovement : MonoBehaviour
{
    public Tilemap tilemap;  // 타일맵을 MapManager에서 받아옴
    public float moveSpeed = 5f;  // 이동 속도
    public float stepSize = 0.1f; // 경로를 한 번에 이동할 만큼의 거리 (애니메이션처럼)

    private Vector3 targetPosition;  // 목표 위치
    private bool canMove = true;     // 이동 가능 여부
    private List<Vector3> path = new List<Vector3>();  // 최단 경로
    private int currentPathIndex = 0; // 현재 경로 인덱스
    private bool isMouseMove = false; // 마우스로 이동하는지 여부

    void Update()
    {
        // 마우스 오른쪽 클릭 시 목표 위치 설정
        if (Input.GetMouseButtonDown(1)) // 마우스 오른쪽 클릭 (0: 왼쪽, 1: 오른쪽, 2: 휠)
        {
            Vector3 worldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            worldPosition.z = Camera.main.transform.position.z;
            SetTargetPosition(worldPosition);
            isMouseMove = true;
        }

        // 기존 키 입력으로 이동
        if (!isMouseMove)
        {
            // 이동 방향 계산 (예: W, A, S, D 키)
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            Vector3 moveDirection = new Vector3(horizontal, vertical, 0).normalized;

            // 목표 위치로 이동
            if (moveDirection != Vector3.zero && canMove)
            {
                targetPosition = transform.position + moveDirection * moveSpeed * Time.deltaTime;
                if (IsValidMove(targetPosition)) // 이동이 가능한지 검사
                {
                    transform.position = targetPosition;
                }
            }
        }

        // 목표 위치로 이동 (경로가 있을 경우)
        if (path.Count > 0)
        {
            MoveAlongPath();
        }
    }

    // 목표 지점 설정
    void SetTargetPosition(Vector3 target)
    {
        // 월드 좌표 -> 타일 셀 좌표로 변환
        Vector3Int targetCell = tilemap.WorldToCell(target);

        // 디버깅 로그 추가: 타일맵 좌표 변환 확인
        Debug.Log($"클릭한 월드 위치: {target}, 타일 셀 위치: {targetCell}");

        // 타일맵 외부 클릭을 방지 (범위 체크)
        if (!tilemap.cellBounds.Contains(targetCell))
        {
            Debug.Log("클릭한 위치가 타일맵 범위 밖입니다.");
            return;  // 맵 범위 밖으로 이동하지 않음
        }

        // 클릭한 위치가 맵 내에서 이동 가능한지 확인
        if (IsValidMove(target))
        {
            // A* 알고리즘을 이용한 경로 탐색
            path = FindPath(transform.position, target);
            currentPathIndex = 0;
        }
        else
        {
            Debug.Log("목표 위치가 이동할 수 없는 곳입니다.");
        }
    }

    // 목표 위치까지 경로 탐색 (A* 알고리즘 구현)
    List<Vector3> FindPath(Vector3 startPos, Vector3 targetPos)
    {
        List<Vector3> newPath = new List<Vector3>();

        // 디버깅 로그 추가: 시작점과 목표점 출력
        Debug.Log($"경로 계산 시작: {startPos} -> {targetPos}");

        // A* 알고리즘을 사용하여 최단 경로 계산
        List<Vector3Int> openList = new List<Vector3Int>();
        List<Vector3Int> closedList = new List<Vector3Int>();
        Dictionary<Vector3Int, Vector3Int> cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        Dictionary<Vector3Int, float> gScore = new Dictionary<Vector3Int, float>();
        Dictionary<Vector3Int, float> fScore = new Dictionary<Vector3Int, float>();

        Vector3Int start = tilemap.WorldToCell(startPos);
        Vector3Int goal = tilemap.WorldToCell(targetPos);

        openList.Add(start);
        gScore[start] = 0;
        fScore[start] = Vector3Int.Distance(start, goal);

        while (openList.Count > 0)
        {
            Vector3Int current = openList.OrderBy(v => fScore.ContainsKey(v) ? fScore[v] : float.MaxValue).First();

            if (current == goal)
            {
                // 경로가 완성되면, 거꾸로 따라가면서 경로 생성
                while (cameFrom.ContainsKey(current))
                {
                    newPath.Insert(0, tilemap.CellToWorld(current));
                    current = cameFrom[current];
                }
                break;
            }

            openList.Remove(current);
            closedList.Add(current);

            foreach (Vector3Int neighbor in GetNeighbors(current))
            {
                if (closedList.Contains(neighbor))
                    continue;

                float tentativeGScore = gScore[current] + 1; // 기본 이동 비용 (가중치 설정 가능)

                if (!openList.Contains(neighbor))
                    openList.Add(neighbor);
                else if (tentativeGScore >= gScore.GetValueOrDefault(neighbor, float.MaxValue))
                    continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeGScore;
                fScore[neighbor] = gScore[neighbor] + Vector3Int.Distance(neighbor, goal);
            }
        }

        // 경로가 제대로 생성되었는지 디버깅
        if (newPath.Count > 0)
        {
            Debug.Log($"경로 계산 완료: {string.Join(" -> ", newPath)}");
        }
        else
        {
            Debug.Log("경로 계산 실패.");
        }

        return newPath;
    }

    // 인접한 네이버 타일을 찾는 함수
    List<Vector3Int> GetNeighbors(Vector3Int current)
    {
        List<Vector3Int> neighbors = new List<Vector3Int>();

        Vector3Int[] directions = new Vector3Int[]
        {
            new Vector3Int(1, 0, 0),  // 오른쪽
            new Vector3Int(-1, 0, 0), // 왼쪽
            new Vector3Int(0, 1, 0),  // 위
            new Vector3Int(0, -1, 0)  // 아래
        };

        foreach (var direction in directions)
        {
            Vector3Int neighbor = current + direction;
            if (IsValidMove(tilemap.CellToWorld(neighbor)))
            {
                neighbors.Add(neighbor);
            }
        }

        return neighbors;
    }

    // 목표 위치가 바닥 타일에만 이동 가능한지 확인
    bool IsValidMove(Vector3 targetPos)
    {
        // 타일맵에서 해당 위치의 셀을 가져옵니다.
        Vector3Int targetCell = tilemap.WorldToCell(targetPos);

        // 해당 위치의 타일을 가져오기
        TileBase tile = tilemap.GetTile(targetCell);

        // 타일이 존재하고, 바닥 타일이면 이동 가능
        if (tile != null && tile.name.Contains("Floor"))  // "Floor" 이름이 포함된 타일만 이동 가능
        {
            return true;  // 바닥 타일일 경우 이동 가능
        }

        return false;  // 다른 타일일 경우 이동 불가
    }

    // 타일맵을 설정하는 메소드 (MapManager에서 호출)
    public void SetTilemap(Tilemap map)
    {
        tilemap = map;  // 타일맵 정보 설정
    }

    // 목표 위치까지 경로를 따라 이동
    void MoveAlongPath()
    {
        if (currentPathIndex < path.Count)
        {
            Vector3 target = path[currentPathIndex];

            // 현재 경로로 캐릭터 이동
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);

            // 목표 위치에 도달하면 다음 경로로 이동
            if (transform.position == target)
            {
                currentPathIndex++;
            }
        }
    }

    // 경로를 시각적으로 표시 (Gizmos)
    void OnDrawGizmos()
    {
        if (path.Count > 0)
        {
            Gizmos.color = Color.green;  // 경로 색상 설정

            for (int i = 0; i < path.Count - 1; i++)
            {
                // 경로 점들 사이를 선으로 연결
                Gizmos.DrawLine(path[i], path[i + 1]);
            }
        }
    }
}
