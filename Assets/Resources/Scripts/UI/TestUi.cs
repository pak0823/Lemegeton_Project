using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestUi : MonoBehaviour
{
    public GameObject resetButton;
    void Start()
    {
        bool isExploration = !Shared.PuzzleManager.IsPuzzleActive; // ∆€¡Ò∏ ¿Ã æ∆¥— ªÛ≈¬
        resetButton.SetActive(isExploration);
    }

    public void OnNormalMapReset()
    {
        Shared.MapManager.ResetExplorationMap();
        Debug.Log("[TestUI]:≈Ω«Ë∏  √ ±‚»≠ øœ∑·");
    }
}
