using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine. UI;
using HapticDllCs;

public class ModelActivation : MonoBehaviour
{
    public Button buttonA, buttonB;
    public GameObject objectA, objectB;

    private bool isAActive = false;
    private bool isBActive = false;

    void Start ( )
    {
        buttonA. onClick. AddListener ( ( ) => ToggleObject ( "A" ) );
        buttonB. onClick. AddListener ( ( ) => ToggleObject ( "B" ) );
    }

    void ToggleObject ( string objectType )
    {
        switch ( objectType )
        {
            case "A":
                isAActive = !isAActive;
                objectA. SetActive ( isAActive );
                SetButtonInteractable ( buttonA , isAActive );
                break;

            case "B":
                isBActive = !isBActive;
                objectB. SetActive ( isBActive );
                SetButtonInteractable ( buttonB , isBActive );
                break;
        }
    }

    void SetButtonInteractable ( Button activeButton , bool isActive )
    {
        buttonA. interactable = !isActive || activeButton == buttonA;
        buttonB. interactable = !isActive || activeButton == buttonB;
    }


}
