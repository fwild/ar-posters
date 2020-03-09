using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.WSA.Input;

public class GameManager : MonoBehaviour
{
    public GameObject fruit;
    public WatsonScript watsonScript;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    public void EnableFruit()
    {
        fruit.SetActive(true);
    }

    public void ChooseFruit(Object chosenFruit)
    {
        string text = chosenFruit.name;
        Debug.Log("fruit chosen " + text);
        watsonScript.PassInteractionRequest(text);
        DisableFruit();
    }

    public void DisableFruit()
    {
        fruit.SetActive(false);
    }
}
