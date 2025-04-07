using System. Collections;
using System. Collections. Generic;
using System. IO; // 파일 입출력을 위한 네임스페이스
using UnityEngine;
using HapticDllCs;

public class Bstick_Data : MonoBehaviour
{
    private bool isMeasuring = false; // 데이터 측정 여부

    [Header("IMU")]
    HapticDll hapticDll;
    public List<float> AccelList; // 가속도 데이터 (x, y, z)
    public List<float> GyroList; // 자이로스코프 데이터 (x, y, z 각속도)

    [Header("CSV")]
    private string filePath; // 현재 데이터 저장 경로
    private float previousTime; // 이전 프레임의 시간
    private float deltaTime; // 델타 타임
    private float startTime = 0f; // 측정 시작 시간  private float lastSaveTime = 0f; // 마지막으로 저장한 시간



    private void Start()
    {
        // 초기화
        hapticDll = new HapticDll();
        hapticDll. InitializeHapticDevice(0 , Debug. Log);

        // CSV 파일 초기화
        InitializeCSV();

        // 시간 초기화
        previousTime = Time. time;
    }

    void Update()
    {
        // 터치 버튼 눌림 및 릴리스 처리
        if ( TouchButtonRelease() )
        {
            ToggleMeasurement();
        }

        if ( isMeasuring )
        {
            
                IMU_DataReceive(); // 데이터 받기
                SaveDataToCSV();   // CSV에 저장
             
        }
    }

    void ToggleMeasurement()
    {
        if ( !isMeasuring ) // 측정 시작
        {

            // 측정 시작 시간 초기화
            startTime = Time. time;

            //  저장할 기본 파일 이름 설정
            string directoryPath = Path. Combine(Application. dataPath , "Bstick_Data");
            string baseFileName = "Bstick_Data.csv";
            filePath = Path. Combine(directoryPath , baseFileName);

            // ✅ 폴더가 없으면 생성
            if ( !Directory. Exists(directoryPath) )
            {
                Directory. CreateDirectory(directoryPath);
            }

            // ✅ 파일이 존재하면 숫자를 붙여 중복 방지
            filePath = GetUniqueFilePath(directoryPath , baseFileName);

            // CSV 파일 초기화
            InitializeCSV();
            isMeasuring = true; // 데이터 측정 시작
            Debug. Log("측정 시작: 파일 저장 경로 - " + filePath);
        }
        else // 측정 종료
        {
            isMeasuring = false; // 데이터 측정 중단
            Debug. Log("측정 종료");
        }
    }

    void InitializeCSV()
    {
        if ( string. IsNullOrEmpty(filePath) )
        {
            Debug. LogError("파일 경로가 설정되지 않았습니다!");
            return;
        }

        // ✅ CSV 헤더 작성
        string header = "TimeStamp,Accel_X,Accel_Y,Accel_Z,Gyro_X,Gyro_Y,Gyro_Z";

        // ✅ 파일 생성 및 헤더 작성
        try
        {
            File. WriteAllText(filePath , header + "\n");
            Debug. Log("CSV 파일 초기화 완료: " + filePath);
        }
        catch ( System. Exception e )
        {
            Debug. LogError("CSV 파일 생성 중 오류 발생: " + e. Message);
        }
    }

    // ✅ 중복된 파일이 있을 경우 숫자를 붙이는 함수
    string GetUniqueFilePath(string directoryPath , string baseFileName)
    {
        string fullPath = Path. Combine(directoryPath , baseFileName);
        int count = 1;

        while ( File. Exists(fullPath) )
        {
            string fileNameWithoutExtension = Path. GetFileNameWithoutExtension(baseFileName);
            string extension = Path. GetExtension(baseFileName);
            string newFileName = $"{fileNameWithoutExtension}_{count}{extension}";
            fullPath = Path. Combine(directoryPath , newFileName);
            count++;
        }

        return fullPath;
    }


    void SaveDataToCSV()
    {
        // 경과 시간 계산 (측정 시작부터)
        float timeStamp = Time. time - startTime;

        // 델타 타임 계산
        deltaTime = Time. deltaTime;
        previousTime = Time. time;

        // CSV 데이터 작성
        string data = $"{timeStamp},{AccelList [ 0 ]},{AccelList [ 1 ]},{AccelList [ 2 ]},{GyroList [ 0 ]},{GyroList [ 1 ]},{GyroList [ 2 ]}";


        // 데이터 파일에 추가
        File. AppendAllText(filePath , data + "\n");
    }

   

   

    void IMU_DataReceive()
    {
        // IMU 데이터 가져오기
        AccelList = hapticDll. GetAccelerometer();
        GyroList = hapticDll. GetGyroscope();
    }


    // 터치 버튼이 눌렸는지 확인
    public bool TouchButtonPress()
    {
        return hapticDll. TouchButtonPress();
    }
    public bool TouchButtonRelease()
    {
        return hapticDll. TouchButtonRelease();
    }
}
