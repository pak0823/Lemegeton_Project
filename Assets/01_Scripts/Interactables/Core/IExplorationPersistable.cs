using System;
using System.Collections.Generic;
using UnityEngine;

public interface IExplorationPersistable
{
    string PersistID { get; }
    ExplorationObjectState SaveState();
    void LoadState(ExplorationObjectState s);
}

[Serializable]
public struct ExplorationObjectState
{
    public string id;          // PersistID
    public string kind;        // "Chest" | "Trap" | "Push" 등
    public string prefabName;  // 재생성용 프리팹명
    public Vector3 position;   // 월드 좌표
    public bool b1;            // Chest: isOpened / Trap: isTriggered 등
    public bool b2;            // Trap: isActive 등 확장용
}

[Serializable]
public class ExplorationSnapshot
{
    public List<ExplorationObjectState> objects = new();
    // Object 게이지 스냅샷
    public int totalBoxes, openedBoxes, triggeredTraps;
    public bool thresholdReached;
}
