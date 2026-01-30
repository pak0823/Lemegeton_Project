using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets; // 필수
using UnityEngine.ResourceManagement.AsyncOperations; // 비동기 핸들

public class AddressableSpawnerTest : MonoBehaviour
{
    [Header("1. 소환할 프리팹 주소")]
    // AssetReferenceGameObject는 GameObject 타입만 넣도록 강제함 (안전)
    public AssetReferenceGameObject monsterPrefabRef;

    [Header("2. 소환된 몬스터 관리 (메모리 해제용)")]
    // 소환된 녀석들을 리스트에 담아둬야 나중에 삭제 가능
    private List<GameObject> spawnedMonsters = new List<GameObject>();

    // 테스트용 GUI 버튼
    void OnGUI()
    {
        if (GUI.Button(new Rect(10, 10, 150, 50), "몬스터 소환 (Spawn)"))
        {
            SpawnMonster();
        }

        if (GUI.Button(new Rect(10, 70, 150, 50), "모두 제거 (Clear)"))
        {
            RemoveAllMonsters();
        }
    }

    // === 1. 생성 (Instantiate) ===
    void SpawnMonster()
    {
        if (monsterPrefabRef == null)
        {
            Debug.LogError("프리팹 주소가 비어있습니다!");
            return;
        }

        // 랜덤 위치 계산
        Vector3 randomPos = Random.insideUnitCircle * 3f;

        // [핵심] LoadAssetAsync가 아니라 InstantiateAsync를 쓴다!
        // (주소, 위치, 회전, 부모)
        // (변수) => { 실행할 내용 }
        monsterPrefabRef.InstantiateAsync(randomPos, Quaternion.identity).Completed += (handle) =>
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                GameObject newMonster = handle.Result;
                newMonster.name = $"Monster_{spawnedMonsters.Count}";
                spawnedMonsters.Add(newMonster);

                Debug.Log($"소환 성공: {newMonster.name}");
            }
            else
            {
                Debug.LogError("소환 실패!");
            }
        };
    }

    // === 2. 해제 (Release Instance) ===
    void RemoveAllMonsters()
    {
        foreach (var monster in spawnedMonsters)
        {
            if (monster != null)
            {
                // [핵심] Destroy(monster) 대신 이걸 써야 함!
                Addressables.ReleaseInstance(monster);
            }
        }
        spawnedMonsters.Clear();
        Debug.Log("모든 몬스터 제거 및 메모리 해제 완료.");
    }
}