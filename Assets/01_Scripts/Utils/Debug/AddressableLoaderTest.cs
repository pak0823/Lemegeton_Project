using UnityEngine;
using UnityEngine.AddressableAssets; // 어드레서블 필수 네임스페이스
using UnityEngine.ResourceManagement.AsyncOperations; // 비동기 작업 관리용

public class AddressableLoaderTest : MonoBehaviour
{
    [Header("1. 연결할 데이터 (직접 연결 대신 주소를 씀)")]
    // 기존: public UnitData unitData;
    // 변경: AssetReference를 사용하면 인스펙터에서 드롭다운으로 선택 가능
    public AssetReference unitDataReference;

    [Header("2. 로딩된 데이터 보관통")]
    // 실제로 로딩이 완료되면 여기에 담아둘 것임
    private ScriptableObject loadedData;

    // 테스트를 위해 게임 시작하자마자 로딩 시도
    void Start()
    {
        LoadData();
    }

    // 데이터 로딩 함수
    public void LoadData()
    {
        if (unitDataReference == null)
        {
            Debug.LogWarning("인스펙터에서 AssetReference를 먼저 연결해주세요!");
            return;
        }

        Debug.Log("데이터 로딩 시작...");

        // 핵심 코드: LoadAssetAsync<T>()
        // "이 주소에 있는 에셋을 비동기로 가져와라"
        unitDataReference.LoadAssetAsync<ScriptableObject>().Completed += OnLoadCompleted;
    }

    // 로딩이 끝나면 호출되는 콜백 함수
    private void OnLoadCompleted(AsyncOperationHandle<ScriptableObject> handle)
    {
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            Debug.Log($"로딩 성공! 불러온 파일 이름: {handle.Result.name}");

            // 결과물을 변수에 저장 (나중에 쓰려고)
            loadedData = handle.Result;

            // 만약 UnitData라면 캐스팅해서 데이터 확인 가능
            if (loadedData is UnitData unit)
            {
                Debug.Log($"유닛 이름: {unit.DisplayName}, 공격력: {unit.baseSTR}");
            }
        }
        else
        {
            Debug.LogError("로딩 실패! 주소가 틀렸거나 파일이 없습니다.");
        }
    }

    // 메모리 해제 (매우 중요!)
    // 이 오브젝트가 파괴될 때 메모리에서도 내려줘야 함
    void OnDestroy()
    {
        if (loadedData != null)
        {
            Debug.Log("데이터 메모리 해제(Unload)");
            // 더 이상 안 쓰면 메모리에서 방 빼라고 명령
            unitDataReference.ReleaseAsset();
            loadedData = null;
        }
    }
}