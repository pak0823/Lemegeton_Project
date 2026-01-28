using System;
using UnityEngine;
using UnityEngine.UI;

public class RewardPopupUI : MonoBehaviour
{
    [SerializeField] private Button confirmButton;

    private Action _onClosed;

    public void Open(Action onClosed)
    {
        _onClosed = onClosed;

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(() =>
            {
                _onClosed?.Invoke();
                Destroy(gameObject);
            });
        }
    }
}
