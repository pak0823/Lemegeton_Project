using Project.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class ExplorationResetUi : MonoBehaviour
{
    public void OnNormalMapReset()
    {
        if (!PuzzleManager.Instance.IsPuzzleActive)
        {
            MapManager.Instance.ResetExplorationMap();
            Debug.Log("[TestUI]:≈Ω«Ë∏  √ ±‚»≠ øœ∑·");
        }
    }

    //public void OnUiShown()
    //{
    //    if (resetButton) resetButton.SetActive(true);
    //}
    //public void OnUiHidden()
    //{
    //    if (resetButton) resetButton.SetActive(false);
    //}
}
