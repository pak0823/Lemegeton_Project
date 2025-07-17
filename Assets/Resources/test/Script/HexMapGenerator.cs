using System.Collections.Generic;
using UnityEngine;

public class HexMapGenerator : MonoBehaviour
{
    // 맵 프리팹 리스트
    public List<GameObject> mapPrefabs;

    // 타일이 배치될 위치 (예시: gridParent 위치)
    public Transform gridParent;

    // Start 함수에서 맵을 랜덤으로 배치
    void Start()
    {
        // 랜덤으로 프리팹 하나 선택
        GameObject selectedPrefab = mapPrefabs[Random.Range(0, mapPrefabs.Count)];

        // 랜덤 위치 설정 (예시로 (0, 0, 0) 위치에 배치)
        Vector3 randomPosition = Vector3.zero; // 원하는 위치로 변경 가능

        // 선택된 프리팹을 해당 위치에 배치
        Instantiate(selectedPrefab, randomPosition, Quaternion.identity, gridParent);
    }
}
