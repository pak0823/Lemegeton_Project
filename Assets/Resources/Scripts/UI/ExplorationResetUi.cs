using Project.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class ExplorationResetUi : MonoBehaviour, ISceneUiModule
{
    public GameObject resetButton;

    private void OnEnable()
    {
        if (resetButton) resetButton.SetActive(true);
    }
    private void OnDisable()
    {
        if (resetButton) resetButton.SetActive(false);
    }

    void Update()
    {
        if(Shared.PuzzleManager.IsPuzzleActive)
            resetButton.SetActive(false);
    }

    public void OnNormalMapReset()
    {
        Shared.MapManager.ResetExplorationMap();
        Debug.Log("[TestUI]:≈Ω«Ë∏  √ ±‚»≠ øœ∑·");
    }

    public void OnUiShown()
    {
        if (resetButton) resetButton.SetActive(true);
    }
    public void OnUiHidden()
    {
        if (resetButton) resetButton.SetActive(false);
    }
}
