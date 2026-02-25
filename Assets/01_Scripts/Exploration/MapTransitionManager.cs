using System.Collections;
using UnityEngine;

/// <summary>
/// 맵 간 이동을 관리합니다.
/// ExplorationScene 내에서 씬 재로드 없이 맵 프리팹을 교체하는 방식으로 동작합니다.
/// 일반 포탈 이동과 숨겨진 포탈 이동 모두 이 매니저를 통해 처리됩니다.
/// </summary>
public class MapTransitionManager : MonoBehaviour
{
    public static MapTransitionManager Instance { get; private set; }

    [Header("페이드 효과")]
    [Tooltip("페이드에 사용할 CanvasGroup (SceneTransitionManager의 기존 fader를 재사용 가능)")]
    [SerializeField] private CanvasGroup fader;

    [SerializeField] private float fadeDuration = 0.4f;

    // 현재 맵의 로컬 ID (예: "moat", "camp")
    private string _currentMapId = "";
    public string CurrentMapId => _currentMapId;

    // 숨겨진 맵 복귀 정보
    private string _hiddenReturnMapId = "";
    private string _hiddenReturnTag = "";
    public bool IsInHiddenMap => !string.IsNullOrEmpty(_hiddenReturnMapId);

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// MapManager에서 맵이 로드된 후 현재 맵 ID를 설정합니다.
    /// </summary>
    public void SetCurrentMapId(string mapId)
    {
        _currentMapId = mapId;
        Debug.Log($"[MapTransitionManager] 현재 맵 설정됨: {_currentMapId}");
    }

    /// <summary>
    /// 연결 가능 여부를 확인합니다.
    /// MapConnectionData는 MapManager에서 스테이지 로드 시 참조합니다.
    /// </summary>
    public bool CanTravel(string from, string to)
    {
        var connectionData = MapManager.Instance?.CurrentConnectionData;
        if (connectionData == null)
        {
            Debug.LogWarning("[MapTransitionManager] ConnectionData가 없습니다.");
            return false;
        }
        return connectionData.CanTravel(from, to);
    }

    /// <summary>
    /// 일반 포탈을 통한 맵 이동.
    /// </summary>
    public void TravelToMap(string destinationMapId)
    {
        if (!CanTravel(_currentMapId, destinationMapId))
        {
            Debug.LogWarning($"[MapTransitionManager] '{_currentMapId}' → '{destinationMapId}' 이동 불가: 연결되지 않은 맵");
            return;
        }
        StartCoroutine(Co_TransitionToMap(destinationMapId, _currentMapId));
    }

    /// <summary>
    /// 숨겨진 포탈 진입. 복귀 시 원래 맵의 지정 탈출 위치로 되돌아옵니다.
    /// </summary>
    public void EnterHiddenMap(string hiddenMapId, string returnMapId, string returnTag, GameObject hiddenMapPrefab)
    {
        _hiddenReturnMapId = returnMapId;
        _hiddenReturnTag = returnTag;
        Debug.Log($"[MapTransitionManager] 숨겨진 맵 진입: {hiddenMapId} / 복귀: {returnMapId}");
        StartCoroutine(Co_HiddenTransition(hiddenMapId, hiddenMapPrefab, returnMapId));
    }

    /// <summary>
    /// 숨겨진 맵 탈출. 저장된 원래 맵과 탈출구 태그로 복귀합니다.
    /// </summary>
    public void ExitHiddenMap()
    {
        if (!IsInHiddenMap)
        {
            Debug.LogWarning("[MapTransitionManager] 숨겨진 맵이 아닌 상태에서 ExitHiddenMap 호출됨");
            return;
        }
        string returnMap = _hiddenReturnMapId;
        string returnTag = _hiddenReturnTag;

        // 복귀 정보 초기화
        _hiddenReturnMapId = "";
        _hiddenReturnTag = "";

        Debug.Log($"[MapTransitionManager] 숨겨진 맵 탈출: {returnMap} / 탈출 태그: {returnTag}");
        StartCoroutine(Co_TransitionToMap(returnMap, _currentMapId, returnTag));
    }

    /// <summary>
    /// 맵 전환 코루틴.
    /// 페이드 아웃 → 맵 교체 → 플레이어 스폰 → 페이드 인 순으로 동작합니다.
    /// </summary>
    private void SaveCurrentMapSnapshot()
    {
        if (StageRuntimeContext.Instance != null && MapManager.Instance != null && MapManager.Instance.CurrentMapData != null)
        {
            var mapData = MapManager.Instance.CurrentMapData;
            if (SceneTransitionManager.Instance != null)
            {
                // 현재 씬 전체(또는 맵 하위)의 스냅샷 생성 후 StageRuntimeContext에 저장
                var snap = SceneTransitionManager.Instance.BuildExplorationSnapshotFromScene(MapManager.Instance.mapLoader?.CurrentMap);
                StageRuntimeContext.Instance.SaveMapSnapshot(mapData.mapId, snap);
            }
        }
    }

    /// <summary>
    /// 숨겨진 맵 전용 코루틴. 프리팩을 직접 받아 LoadHiddenMap으로 로드합니다.
    /// </summary>
    private IEnumerator Co_HiddenTransition(string hiddenMapId, GameObject prefab, string fromMapId)
    {
        PlayerMovement.Instance?.LockMovementIndefinite();
        SaveCurrentMapSnapshot(); // 기존 맵 스냅샷 저장
        yield return StartCoroutine(Co_Fade(0f, 1f));

        // 숨겨진 맵은 프리팩을 직접 지정하여 로드
        MapManager.Instance?.LoadHiddenMap(hiddenMapId, prefab);
        _currentMapId = hiddenMapId;

        yield return null;

        // 도착 포인트: 숨겨진 맵의 기본(빈 fromMapId) 도착 포인트 사용
        SpawnPlayerAtArrivalPoint(fromMapId);

        yield return StartCoroutine(Co_Fade(1f, 0f));
        PlayerMovement.Instance?.UnlockMovementIndefinite();

        Debug.Log($"[MapTransitionManager] 숨겨진 맵 전환 완료 → {hiddenMapId}");
    }


private IEnumerator Co_TransitionToMap(string destinationMapId, string fromMapId, string forceArrivalTag = null)
    {
        // 1. 이동 중 입력 차단
        PlayerMovement.Instance?.LockMovementIndefinite();

        SaveCurrentMapSnapshot(); // 기존 맵 스냅샷 저장

        // 2. 페이드 아웃
        yield return StartCoroutine(Co_Fade(0f, 1f));

        // 3. 맵 교체 (MapManager에 위임)
        MapManager.Instance?.LoadSpecificMap(destinationMapId);
        _currentMapId = destinationMapId;

        // 4. 플레이어가 생성될 때까지 한 프레임 대기
        yield return null;

        // 5. 도착 스폰 포인트로 이동
        var arrivalTag = forceArrivalTag ?? fromMapId;
        SpawnPlayerAtArrivalPoint(arrivalTag);

        // 6. 페이드 인
        yield return StartCoroutine(Co_Fade(1f, 0f));

        // 7. 입력 잠금 해제
        PlayerMovement.Instance?.UnlockMovementIndefinite();

        Debug.Log($"[MapTransitionManager] 전환 완료 → {destinationMapId}");
    }

    /// <summary>
    /// fromMapId에 해당하는 도착 스폰 포인트에 플레이어를 위치시킵니다.
    /// </summary>
    private void SpawnPlayerAtArrivalPoint(string fromMapId)
    {
        var player = PlayerMovement.Instance;
        if (player == null) return;

        // 현재 맵의 ExplorationMapData에서 도착 포인트 검색
        var mapData = MapManager.Instance?.CurrentMapData;
        if (mapData == null) return;

        // fromMapId와 일치하는 도착 포인트 탐색
        PortalArrivalPoint arrival = mapData.arrivalPoints.Find(p => p.fromMapId == fromMapId);

        // 없으면 fromMapId가 비어있는(기본) 도착 포인트 사용
        if (arrival == null)
            arrival = mapData.arrivalPoints.Find(p => string.IsNullOrEmpty(p.fromMapId));

        if (arrival?.spawnTransform != null)
        {
            player.TeleportTo(arrival.spawnTransform.position);
            Debug.Log($"[MapTransitionManager] 플레이어 스폰 위치: {arrival.spawnTransform.position} (fromMap: {fromMapId})");
        }
        else
        {
            Debug.LogWarning($"[MapTransitionManager] '{fromMapId}'에 대한 도착 포인트를 찾지 못했습니다. 기본 스폰 위치 유지.");
        }
    }

    /// <summary>
    /// 페이드 코루틴.
    /// </summary>
    private IEnumerator Co_Fade(float from, float to)
    {
        if (fader == null)
        {
            // fader가 없으면 SceneTransitionManager의 fader를 시도
            if (SceneTransitionManager.Instance != null)
                fader = SceneTransitionManager.Instance.fader;
        }

        if (fader == null) yield break;

        fader.blocksRaycasts = true;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            fader.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }
        fader.alpha = to;
        if (to == 0f) fader.blocksRaycasts = false;
    }
}
