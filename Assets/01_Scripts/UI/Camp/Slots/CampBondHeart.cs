using UnityEngine;
using UnityEngine.UI;

public enum HeartState
{
    Empty,
    Half,
    Full
}

public class CampBondHeart : MonoBehaviour
{
    [SerializeField] private Image heartImage;

    // Sprite references set by CampStatusPage when instantiated/initialized
    private Sprite _emptySprite;
    private Sprite _halfSprite;
    private Sprite _fullSprite;

    // To store original size
    private float _originalWidth;
    private RectTransform _rectTransform;

    public void Setup(Sprite empty, Sprite half, Sprite full)
    {
        _emptySprite = empty;
        _halfSprite = half;
        _fullSprite = full;

        if (heartImage == null) heartImage = GetComponent<Image>();
        _rectTransform = heartImage.GetComponent<RectTransform>();
        
        if (_rectTransform != null)
        {
            _originalWidth = _rectTransform.sizeDelta.x;
        }
    }

    public void SetState(HeartState state)
    {
        if (heartImage == null) return;

        switch (state)
        {
            case HeartState.Empty:
                heartImage.sprite = _emptySprite;
                heartImage.enabled = (_emptySprite != null);
                SetWidth(_originalWidth);
                break;
            case HeartState.Half:
                heartImage.sprite = _halfSprite;
                heartImage.enabled = (_halfSprite != null);
                SetWidth(_originalWidth * 0.5f); // Width 50%
                break;
            case HeartState.Full:
                heartImage.sprite = _fullSprite;
                heartImage.enabled = (_fullSprite != null);
                SetWidth(_originalWidth);
                break;
        }
    }

    private void SetWidth(float width)
    {
        if (_rectTransform != null)
        {
            Vector2 size = _rectTransform.sizeDelta;
            size.x = width;
            _rectTransform.sizeDelta = size;
        }
    }
}
