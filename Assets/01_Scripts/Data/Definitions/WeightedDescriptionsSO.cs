using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "WD_Preset", menuName = "Interaction/WeightedDescriptions Preset")]
public class WeightedDescriptionsSO : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public int weight = 10;
        [TextArea(1, 3)] public string text;
        [Tooltip("이 항목이 선택되면 실행할 이벤트(선택사항)")]
        public UnityEvent onPicked;
    }

    [Header("엔트리(가중치/문구/이벤트 세트)")]
    public Entry[] entries = new Entry[]
    {
        new Entry{ weight=40, text="(기본) 40% 문구" },
        new Entry{ weight=30, text="(기본) 30% 문구" },
        new Entry{ weight=20, text="(기본) 20% 문구" },
        new Entry{ weight=10, text="(기본) 10% 문구" },
    };

    [Tooltip("모든 텍스트가 비어있으면 출력 생략")]
    public bool skipIfAllEmpty = true;

    public Entry PickEntry()
    {
        if (entries == null || entries.Length == 0) return null;

        bool anyText = false;
        int total = 0;
        foreach (var e in entries)
        {
            if (e == null) continue;
            if (!string.IsNullOrWhiteSpace(e.text)) anyText = true;
            if (e.weight > 0 && (!string.IsNullOrWhiteSpace(e.text) || e.onPicked != null))
                total += e.weight;
        }
        if (total <= 0) return null;
        if (skipIfAllEmpty && !anyText)
            return null;

        int roll = Random.Range(0, total);
        int acc = 0;
        foreach (var e in entries)
        {
            if (e == null) continue;
            if (e.weight <= 0) continue;
            if (string.IsNullOrWhiteSpace(e.text) && e.onPicked == null) continue;

            acc += e.weight;
            if (roll < acc) return e;
        }
        return null;
    }

    /// <summary>뽑고, 텍스트와 이벤트를 한 번에 처리하고 싶을 때.</summary>
    public string PickAndInvoke()
    {
        var e = PickEntry();
        if (e == null) return null;
        e.onPicked?.Invoke();
        return e.text;
    }

    public int PickIndex()
    {
        var e = PickEntry();
        if (e == null) return -1;
        for (int i = 0; i < entries.Length; i++)
            if (entries[i] == e) return i;
        return -1;
    }
}
