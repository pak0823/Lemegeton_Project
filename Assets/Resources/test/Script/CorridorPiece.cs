using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class CorridorPiece : MonoBehaviour
{
    public List<EntranceInfo> entrances = new List<EntranceInfo>();

    private void Awake()
    {
        InitializeEntrances();
    }
    void InitializeEntrances()
    {
        entrances.Clear();
        Transform enterParent = transform.Find("Enter");
        if (enterParent == null)
        {
            Debug.LogWarning($"{name}: 'Enter' 오브젝트가 없습니다. Entrance 자동 초기화 실패!");
            return;
        }

        // HexDirection 열거형 이름들을 길이(내림차순)로 정렬하여 더 긴 이름이 먼저 검사되도록 합니다.
        // 예를 들어, "UpRight"가 "Right"보다 먼저 검사되어야 합니다.
        List<string> hexDirectionNames = new List<string>(System.Enum.GetNames(typeof(HexDirection)));
        hexDirectionNames.Sort((a, b) => b.Length.CompareTo(a.Length)); // 긴 이름을 먼저 검사하도록 정렬

        foreach (Transform t in enterParent)
        {
            // t.name에서 "Enter_" 접두사를 제거한 실제 방향 이름 부분을 가져옵니다.
            string objNameWithoutPrefix = t.name.Replace("Enter_", "");

            foreach (string dirName in hexDirectionNames)
            {
                // 오브젝트의 이름이 HexDirection 이름과 정확히 일치하는지 확인
                if (objNameWithoutPrefix == dirName)
                {
                    EntranceInfo info = new EntranceInfo();
                    info.direction = (HexDirection)System.Enum.Parse(typeof(HexDirection), dirName); // 문자열을 HexDirection으로 파싱
                    info.entranceTransform = t;
                    entrances.Add(info);
                    break; // 정확히 일치하는 것을 찾았으니 다음 오브젝트로 넘어감
                }
            }
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        foreach (var e in entrances)
        {
            if (e.entranceTransform != null)
                Gizmos.DrawLine(e.entranceTransform.position, e.entranceTransform.position + DirToVector(e.direction) * 0.5f);
        }

        // Collider Bounds를 시각화하는 Gizmos 코드 제거됨
    }

    Vector3 DirToVector(HexDirection dir)
    {
        switch (dir)
        {
            case HexDirection.Right: return new Vector3(1, 0, 0);
            case HexDirection.UpRight: return new Vector3(0.5f, 0.87f, 0);
            case HexDirection.UpLeft: return new Vector3(-0.5f, 0.87f, 0);
            case HexDirection.Left: return new Vector3(-1, 0, 0);
            case HexDirection.DownLeft: return new Vector3(-0.5f, -0.87f, 0);
            case HexDirection.DownRight: return new Vector3(0.5f, -0.87f, 0);
            default: return Vector3.zero;
        }
    }
}