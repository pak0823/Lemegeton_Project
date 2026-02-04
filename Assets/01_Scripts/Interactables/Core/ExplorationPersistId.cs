using System.Linq;
using UnityEngine;

public class ExplorationPersistId : MonoBehaviour
{
    [SerializeField] private string id;
    public string Id => id;

#if UNITY_EDITOR
    private void Reset()
    {
        if (string.IsNullOrEmpty(id))
            id = System.Guid.NewGuid().ToString("N");
    }
#endif

    private void Awake()
    {
        if (string.IsNullOrEmpty(id))
            id = System.Guid.NewGuid().ToString("N");
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        if (string.IsNullOrEmpty(id))
            id = System.Guid.NewGuid().ToString("N");

        // 같은 맵(루트) 아래에서 중복 검사
        var root = transform;
        while (root.parent != null) root = root.parent;

        var all = root.GetComponentsInChildren<ExplorationPersistId>(true);
        if (all.Count(x => x != this && x.id == id) > 0)
        {
            // 중복이면 새로 발급
            id = System.Guid.NewGuid().ToString("N");
        }
    }
#endif

    public void OverrideIdForRestore(string newId) => id = newId;
}
