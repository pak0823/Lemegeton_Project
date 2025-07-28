using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;

public class PuzzleManager : MonoBehaviour
{
    public Tilemap FloorTilemap { get; private set; }
    public Tilemap WallTilemap { get; private set; }
    private Dictionary<Vector3Int, PuzzleBox> boxes = new();

    // 초기 박스 순서를 유지하기 위해 리스트로도 저장
    private List<PuzzleBox> allBoxes = new();

    //BoxGoal을 리스트로 저장
    private List<BoxGoal> goals = new List<BoxGoal>();

    private Vector3 initialPlayerWorld;
    public Button resetBtn;

    public bool IsPuzzleActive { get; private set; }

    private void Awake()
    {
        Shared.PuzzleManager = this;


        resetBtn.gameObject.SetActive(false);//임시로 추가
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
        InitializeGoals();  //BoxGoal 리스트 갱신
        IsPuzzleActive = true;

        resetBtn.gameObject.SetActive(true); //임시로 추가
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
            allBoxes.Add(box);
            // 초기 위치 저장
            box.CacheInitialCell(c, FloorTilemap);
        }
    }
    public void InitializeGoals()
    {
        goals.Clear();
        goals.AddRange(FindObjectsOfType<BoxGoal>());
        NotifyGoalChanged();
    }

    //플레이어 초기 위치 저장
    public void CacheInitialPlayerPosition(Vector3 pos)
    {
        initialPlayerWorld = pos;
    }

    //모든 BoxGoal 위치에 박스가 위치하는지 확인
    public void NotifyGoalChanged()
    {
        // 모든 목표가 active 상태인지 확인
        bool allDone = goals.All(g => g.IsActive);
        if (allDone)
            OnPuzzleComplete();
    }

    //모든 목표가 active일 때 수행되는 포탈 생성
    private void OnPuzzleComplete()
    {
        //포탈 프리팹을 Instantiate
        //Instantiate(portalPrefab, portalSpawnPosition, Quaternion.identity);

        Debug.Log("모든 박스 배치 성공!");
        Debug.Log("포탈이 생성되었습니다.");
    }

    public void ClearMaps()
    {
        FloorTilemap = null;
        WallTilemap = null;
        boxes.Clear();
        allBoxes.Clear();
        IsPuzzleActive = false;
    }

    public void ExecutePush(Vector3Int oldCell, Vector3Int newCell)
    {
        if (!boxes.TryGetValue(oldCell, out var box)) return;
        boxes.Remove(oldCell);
        boxes[newCell] = box;
        box.SetCell(newCell, FloorTilemap);
    }

    // 플레이어가 playerCell 위치에서 dir 방향으로 상자를 밀어내고,
    // 성공 시 true 반환
    public bool TryPush(Vector3Int boxCell, Vector3Int dir)
    {

        // 1) 우선 박스가 진짜 있는 셀인지 확인
        if (!boxes.TryGetValue(boxCell, out var boxCheck))
        {
            Debug.Log($"[Push] 실패: 박스가 없는 셀입니다. boxCell={boxCell}");
            return false;
        }

        // 1) 입력 받은 박스 셀과 방향
        Debug.Log($"[TryPush] 박스 셀={boxCell}, 방향={dir}");

        // 2) 목표 셀 계산
        Vector3Int target = boxCell + dir;
        Debug.Log($"[TryPush] 목표 셀={target}");

        // 3) 이동 가능 여부 검사
        bool hasFloor = FloorTilemap.HasTile(target);
        bool hasWall = WallTilemap.HasTile(target);
        bool hasBox = boxes.ContainsKey(target);

        if (!hasFloor || hasWall || hasBox)
        {
            Debug.Log($"[TryPush] 실패 → Floor?{hasFloor} Wall?{hasWall} Box?{hasBox}");
            return false;
        }

        return true;
    }

    // Reset 버튼으로 호출
    // 저장된 초기 위치대로 모두 되돌림
    public void ResetPuzzle()
    {
        boxes.Clear();
        foreach (var box in allBoxes)
        {
            // 박스 자체를 월드 단위로 이동
            box.ResetToInitial(FloorTilemap);

            // 딕셔너리 재등록
            Vector3Int c = box.CurrentCell;
            boxes[c] = box;
        }

        // 플레이어 원위치로 돌리기
        if (initialPlayerWorld != null)
        {
            Shared.PlayerMovement.TeleportTo(initialPlayerWorld);
        }

        //BoxGoal 상태 초기화
        InitializeGoals();

    }
}