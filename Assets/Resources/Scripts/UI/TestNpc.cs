using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestNpc : MonoBehaviour
{
    [SerializeField]TrainingUI trainingUI;
    bool playerInRange = false;
    private void Start()
    {
        if(trainingUI == null)
            trainingUI = FindAnyObjectByType<TrainingUI>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.tag == "Player")
        {
            playerInRange = true;
            Debug.Log("playerInRange = true");
        }
            
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.tag == "Player")
        {
            playerInRange = false;
            Debug.Log("playerInRange = false");
        }
            
    }
    private void Update()
    {
        if (playerInRange && Input.GetKeyDown(KeyCode.F))
        {
            trainingUI.OnToggle();
        }    
    }
}
