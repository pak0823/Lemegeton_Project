using UnityEngine;

public class TargetMarker : MonoBehaviour
{
    public Vector3 offset = new Vector3(0f, 0.3f, 0f); // 머리 위 쪽
    BattleUnit current;

    public void Attach(BattleUnit unit)
    {
        current = unit;
        gameObject.SetActive(current != null);
        if (current != null) UpdatePosition();
    }

    public void Hide()
    {
        current = null;
        gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (current != null) UpdatePosition();
    }

    void UpdatePosition()
    {
        transform.position = current.transform.position + offset;
    }
}
