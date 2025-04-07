using HapticDllCs;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BstickManager : MonoBehaviour
{
    public static BstickManager Instance { get; private set; }
    private HapticDll hapticDll;
    public List<float> AccelList { get; private set; } = new List<float> ( ); // 초기화
    public List<float> GyroList { get; private set; } = new List<float> ( ); // 초기화
    private bool isInitialized = false;

    void Awake ( )
    {
        if ( Instance == null )
        {
            Instance = this;
            //DontDestroyOnLoad ( gameObject );
            InitializeHaptic ( );
        }

    }

    private void InitializeHaptic ( )
    {
        if ( !isInitialized )
        {
            hapticDll = new HapticDll ( );
            try
            {
                hapticDll. InitializeHapticDevice ( 0 , Debug. Log );
                isInitialized = true;
                Debug. Log ( "HapticDll initialized successfully" );
            }
            catch ( System. Exception e )
            {
                Debug. LogError ( $"Failed to initialize HapticDll: {e. Message}" );
            }
        }
    }

    public bool TouchButtonRelease ( )
    {
        if ( hapticDll == null || !isInitialized )
        {
            Debug. LogWarning ( "HapticDll is null or not initialized, reinitializing..." );
            InitializeHaptic ( );
        }
        try
        {
            return hapticDll. TouchButtonRelease ( );
        }
        catch ( System. Exception e )
        {
            Debug. LogError ( $"TouchButtonRelease failed: {e. Message}" );
            return false;
        }
    }

    public void IMU_DataReceive ( )
    {
        if ( hapticDll == null || !isInitialized )
        {
            Debug. LogWarning ( "HapticDll not ready, reinitializing..." );
            InitializeHaptic ( );
        }
        AccelList = hapticDll. GetAccelerometer ( ) ?? new List<float> { 0 , 0 , 0 };
        GyroList = hapticDll. GetGyroscope ( ) ?? new List<float> { 0 , 0 , 0 };
        if ( AccelList. Count < 3 || GyroList. Count < 3 )
        {
            Debug. LogWarning ( "IMU data invalid, using defaults" );
            AccelList = new List<float> { 0 , 0 , 0 };
            GyroList = new List<float> { 0 , 0 , 0 };
        }
    }

    void OnDestroy ( )
    {
        if ( Instance == this )
        {
            isInitialized = false;
            Instance = null;
        }
    }
}
