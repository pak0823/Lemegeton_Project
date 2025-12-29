using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestNpc : MonoBehaviour
{
    [SerializeField]TrainingUI trainingUI;


    private void Start()
    {
        if(trainingUI == null)
            trainingUI = FindAnyObjectByType<TrainingUI>();
    }

    public string GetHintLabel() => "¥Î»≠";

    public void Talk()
    {
        if (trainingUI == null)
            trainingUI = FindAnyObjectByType<TrainingUI>();

        trainingUI?.OnToggle();
    }
}
