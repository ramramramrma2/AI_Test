using System. Collections;
using System. Collections. Generic;
using System. IO; // 파일 입출력을 위한 네임스페이스
using UnityEngine;
using HapticDllCs;

public class DataProcess : MonoBehaviour
{
   
    [Header("IMU")]
    HapticDll hapticDll;
    public List<float> AccelList; // 가속도 데이터 (x, y, z)
    public List<float> GyroList; // 자이로스코프 데이터 (x, y, z 각속도)


    private void Start()
    {
        hapticDll = new HapticDll();
        hapticDll. InitializeHapticDevice(1 , Debug. Log);     
    }

    void Update()
    {
        IMU_DataReceive();
    }

    void IMU_DataReceive()
    {
        // IMU 데이터 가져오기
        AccelList = hapticDll. GetAccelerometer();
        GyroList = hapticDll. GetGyroscope();
    }

}
