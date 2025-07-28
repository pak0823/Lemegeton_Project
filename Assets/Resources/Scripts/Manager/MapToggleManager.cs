using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

//  ScriptableObject 기반으로 스테이지별, 랜덤 퀴즈맵 전환 관리
public class MapToggleManager : MonoBehaviour
{
    [Header("맵 참조")]
    public Transform gridParent;
    public GameObject mainMap;  // MapManager가 생성한 던전 루트 오브젝트
    public Transform playerTransform;   // 씬에 있는 플레이어 Transform

    [Header("StageData")]
    public StageDatabase stageDB;   //  에디터에 할당
    public int currentStage = 1;    //  진입할 스테이지 번호(1부터)

    private GameObject activeQuizMap;

    void Awake()
    {
        if (Shared.MapToggleManager == null) Shared.MapToggleManager = this;
        else Destroy(gameObject);
    }

    // 현재 스테이지에 해당하는 퀴즈맵 중 하나를 랜덤 생성하여 전환
    public void EnterQuizMap()
    {
        //입력된 모든키 상태 초기화
        Input.ResetInputAxes();

        // 메인 맵 비활성화
        mainMap.SetActive(false);

        // 이전 퀴즈맵 제거
        if (activeQuizMap != null)
            Destroy(activeQuizMap);

        // 데이터베이스에서 스테이지 찾기
        var data = stageDB.stages.FirstOrDefault(s => s.stageNumber == currentStage);
        if (data == null || data.quizMapPrefabs.Length == 0)
        {
            Debug.LogWarning($"Stage {currentStage} 데이터 또는 퀴즈맵이 없습니다.");
            return;
        }

        // 퀴즈맵 랜덤 선택
        int idx = Random.Range(0, data.quizMapPrefabs.Length);
        //activeQuizMap = Instantiate(data.quizMapPrefabs[idx]);
        activeQuizMap = Instantiate(
            data.quizMapPrefabs[idx],
            Vector3.zero,                     // 월드 스폰 위치가 따로 없으면 Vector3.zero
            Quaternion.identity,
            gridParent                        
        );

        // 3) Floor/Wall Tilemap 자동 검색
        Tilemap floorMap = null, wallMap = null;
        foreach (var tm in activeQuizMap.GetComponentsInChildren<Tilemap>())
        {
            var n = tm.gameObject.name.ToLower();
            if (floorMap == null && n.Contains("floor")) floorMap = tm;
            if (wallMap == null && n.Contains("wall")) wallMap = tm;
            if (floorMap != null && wallMap != null) break;
        }
        if (floorMap == null) Debug.LogError("Floor 타일맵을 찾을 수 없습니다!");
        if (wallMap == null) Debug.LogWarning("Wall 타일맵을 찾을 수 없습니다.");


        // 새 타일맵을 PlayerMovement, PuzzleManager에 전달
        Shared.PlayerMovement.SetTilemap(floorMap, wallMap);
        Shared.PuzzleManager.SetMaps(floorMap, wallMap);
        Shared.PlayerMovement.ClearPath();

        // 플레이어 위치 이동
        var playerSpawn = activeQuizMap.transform.Find("PlayerStart");
        if (playerSpawn != null)
        {
            playerTransform.position = playerSpawn.position;
            Shared.PuzzleManager.CacheInitialPlayerPosition(playerTransform.position);
        }
        else
        {
            Debug.LogWarning("퀴즈맵에 'PlayerStart' 오브젝트가 없습니다.");
        }
    }

    // 퀴즈맵 종료 후 메인 맵으로 복귀
    //public void ExitQuizMap(Vector3 returnPosition)
    //{
    //    if (activeQuizMap != null) Destroy(activeQuizMap);
    //    mainMap.SetActive(true);

    //    // 메인맵에서도 Floor/Wall Tilemap 재검색
    //    Tilemap floorMap = null, wallMap = null;
    //    foreach (var tm in mainMap.GetComponentsInChildren<Tilemap>())
    //    {
    //        var n = tm.gameObject.name.ToLower();
    //        if (floorMap == null && n.Contains("floor")) floorMap = tm;
    //        if (wallMap == null && n.Contains("wall")) wallMap = tm;
    //        if (floorMap != null && wallMap != null) break;
    //    }
    //    if (floorMap == null) Debug.LogError("Floor 타일맵을 찾을 수 없습니다!");
    //    if (wallMap == null) Debug.LogWarning("Wall 타일맵을 찾을 수 없습니다.");

    //    Shared.PlayerMovement.SetTilemap(floorMap, wallMap);
    //    Shared.PlayerMovement.ClearPath();
    //    Shared.PuzzleManager.ClearMaps();
    //    playerTransform.position = returnPosition;
    //}
}