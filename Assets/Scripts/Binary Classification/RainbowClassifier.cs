using UnityEngine;
using Unity.Sentis;
using System.Collections. Generic;
using HapticDllCs;
using System.IO;
using System.Linq;
using TMPro;

public class RainbowClassifier : MonoBehaviour
{
    [Header ( "Prediction Result UI" )]
    public TextMeshProUGUI resultText; //예측 결과 텍스트
    public TextMeshProUGUI averagePredictionText; //예측 결과 평균 확률

    [Header ( "IMU Data" )]
    public List<float> AccelList; // 3축 가속도 데이터 (x, y, z)
    public List<float> GyroList; // 3축 각속도 데이터 (x, y, z)
    HapticDll hapticDll;
    private List<float [ ]> imuDataList = new List<float [ ]> ( );
    private List<float [ ]> collectedIMUData = new List<float [ ]> ( ); // 측정된 데이터를 저장할 리스트

    [Header ( "CSV" )]
    public string saveFilePath = "C:\\Users\\HARAM\\Desktop\\BsitckData\\Rainbow_Interpol";
    private bool isMeasuring = false; // 데이터 측정 여부
    private int fileCount = 1; // 파일 이름에 사용할 카운터
    private float startTime = 0f; // 측정 시작 시간  

    [Header ( "AI" )]
    public ModelAsset rainbowPredictionModel;  // ONNX 모델
    Worker worker;
    Tensor<float> tensor;
    private const int windowSize = 20;  // LSTM 입력 시퀀스 길이 (10 프레임)
    private const int targetLength = 334; // 학습에 사용한 보간 데이터 길이
    private const int numFeatures = 7;
    //학습에 사용된 표준화의 평균과 표준편차
    //private readonly float [ ] means = new float [ numFeatures ] { 0.9324445f , -0.19492703f , 0.06843478f , -0.29762672f , -19.30974239f , 67.34333922f , -29.4099034f };
    //private readonly float [ ] stdDevs = new float [ numFeatures ] { 0.55839907f , 0.8227768f , 0.29892986f , 0.46210457f , 33.79528767f , 62.58226755f , 45.76436032f };

    private readonly float [ ] means = new float [ numFeatures ] { 1.13008671e+00f, -2.13395840e-01f , 3.43820440e-02f , -2.98798245e-01f , -1.14793941e+01f , 6.25999314e+01f , -2.42798605e+01f };
    private readonly float [ ] stdDevs = new float [ numFeatures ] { 0.72183749f , 0.80504122f , 0.26499925f , 0.47182335f , 24.34893828f , 57.89901415f , 38.24527135f };

    void Start ( )
    {
        // Sentis ONNX 모델 로드
        var runtimeModel = ModelLoader. Load ( rainbowPredictionModel );
        worker = new Worker ( runtimeModel , BackendType. CPU );


    }

    void Update ( )
    {
        if ( BstickManager. Instance == null )
        {
            Debug. LogError ( "BstickManager.Instance is null!" );
            return;
        }
        if ( BstickManager. Instance. TouchButtonRelease ( ) )
        {
            ToggleMeasurement ( );
        }
        if ( isMeasuring )
        {
            CollectIMUData ( );
        }
    }

    void ToggleMeasurement ( )
    {
        if ( !isMeasuring )
        {
            startTime = Time. time;
            isMeasuring = true;
            collectedIMUData. Clear ( );
            imuDataList. Clear ( );
            BstickManager. Instance. IMU_DataReceive ( ); // 초기 데이터 갱신
            Debug. Log ( "측정 시작" );
        }
        else
        {
            isMeasuring = false;
            Debug. Log ( "측정 종료, 예측 시작" );
            //SaveDataToCSV ( );
            if ( collectedIMUData. Count >= windowSize )
            {
                PredictSuccess ( );
            }
            else
            {
                Debug. LogWarning ( "데이터가 충분하지 않습니다." );
            }
        }
    }

    void CollectIMUData ( )
    {
        BstickManager. Instance. IMU_DataReceive ( );
        var accelList = BstickManager. Instance. AccelList;
        var gyroList = BstickManager. Instance. GyroList;

        if ( accelList == null || gyroList == null || accelList. Count < 3 || gyroList. Count < 3 )
        {
            Debug. LogError ( "IMU data is invalid!" );
            return;
        }

        float currentTime = Time. time - startTime;
        float [ ] imuFrame = {
            currentTime,
            accelList[0], accelList[1], accelList[2],
            gyroList[0], gyroList[1], gyroList[2]
        };
        imuDataList. Add ( imuFrame );
        collectedIMUData. Add ( imuFrame );
    }

    List<float [ ]> InterpolateToTargetLength ( List<float [ ]> dataList , int targetLength )
    {
        int currentLength = dataList. Count;
        List<float [ ]> interpolatedData = new List<float [ ]> ( targetLength );

        // 보간하려는 비율 계산
        float stepSize = ( float ) ( currentLength - 1 ) / ( targetLength - 1 );

        for ( int i = 0 ; i < targetLength ; i++ )
        {
            int index1 = Mathf.FloorToInt ( i * stepSize );
            int index2 = Mathf.Min ( index1 + 1 , currentLength - 1 );
            float t = ( i * stepSize ) - index1;


            // 가속도와 자이로스코프 데이터를 보간
            float interpolatedTime = Mathf. Lerp ( dataList [ index1 ] [ 0 ] , dataList [ index2 ] [ 0 ] , t );
            float interpolatedAccelX = Mathf. Lerp ( dataList [ index1 ] [ 1 ] , dataList [ index2 ] [ 1 ] , t );
            float interpolatedAccelY = Mathf. Lerp ( dataList [ index1 ] [ 2 ] , dataList [ index2 ] [ 2 ] , t );
            float interpolatedAccelZ = Mathf. Lerp ( dataList [ index1 ] [ 3 ] , dataList [ index2 ] [ 3 ] , t );
            float interpolatedGyroX = Mathf. Lerp ( dataList [ index1 ] [ 4 ] , dataList [ index2 ] [ 4 ] , t );
            float interpolatedGyroY = Mathf. Lerp ( dataList [ index1 ] [ 5 ] , dataList [ index2 ] [ 5 ] , t );
            float interpolatedGyroZ = Mathf. Lerp ( dataList [ index1 ] [ 6 ] , dataList [ index2 ] [ 6 ] , t );
            
            // 보간된 값을 새로운 프레임에 추가
            float [ ] interpolatedFrame = new float [ 7 ];
            interpolatedFrame[ 0 ] = interpolatedTime;
            interpolatedFrame[ 1 ] = interpolatedAccelX;
            interpolatedFrame[ 2 ] = interpolatedAccelY;
            interpolatedFrame[ 3 ] = interpolatedAccelZ;
            interpolatedFrame[ 4 ] = interpolatedGyroX;
            interpolatedFrame[ 5 ] = interpolatedGyroY;
            interpolatedFrame[ 6 ] = interpolatedGyroZ;

            interpolatedData. Add ( interpolatedFrame );
        }

        return interpolatedData;
    }

    // ✅ 표준화 함수 추가
    float [ ] StandardizeInput ( float [ ] inputArray )
    {
        float [ ] standardized = new float [ inputArray. Length ];
        for ( int i = 0 ; i < inputArray. Length ; i++ )
        {
            int featureIdx = i % numFeatures;
            standardized [ i ] = ( inputArray [ i ] - means [ featureIdx ] ) / stdDevs [ featureIdx ];
        }
        return standardized;
    }

    void PredictSuccess ( )
    {
        if ( collectedIMUData. Count < windowSize )
        {
            Debug. LogWarning ( "데이터가 충분하지 않습니다." );
            return;
        }

        // 보간한 데이터 불러옴
        List<float [ ]> interpolatedData = InterpolateToTargetLength ( collectedIMUData , targetLength );
        int totalFrames = interpolatedData. Count; // 실시간 데이터에 targetLength만큼 보간된 데이터 길이 = 2174
        int stepSize = 10; // 슬라이딩 윈도우 이동 간격 (10 프레임씩 이동)
        int numPredictions = ( totalFrames - windowSize + 1 + stepSize - 1 ) / stepSize; // 2174 / 10 = 217
        List<float> predictions = new List<float> ( );

        for ( int startIdx = 0 ; startIdx < numPredictions * stepSize ; startIdx += stepSize )
        {
            // 10 프레임 데이터 추출
            float [ ] inputArray = new float [ windowSize * 7 ];
            int index = 0;

            for ( int i = startIdx ; i < startIdx + windowSize ; i++ )
            {
                for ( int j = 0 ; j < numFeatures ; j++ )
                {
                    inputArray [ index++ ] = interpolatedData [ i ] [ j ];
                }
            }

            inputArray = StandardizeInput ( inputArray );

            // 모델 예측
            using ( var tensor = new Tensor<float> ( new TensorShape ( 1 , windowSize , numFeatures ) , inputArray ) )
            {
                worker. Schedule ( tensor );

                // 출력 텐서 처리
                using ( var outputTensor = worker. PeekOutput ( "output_layer" ) as Tensor<float> )
                {
                    outputTensor. ReadbackRequest ( );
                    outputTensor. ReadbackAndClone ( ); // 복제된 텐서로 작업
                    float prediction = outputTensor [ 0 ];
                    predictions. Add ( prediction );
                }
            }
        }
        // 예측 결과 처리
        ProcessPredictions ( predictions );
    }

    // 예측 결과 처리 함수
    void ProcessPredictions ( List<float> predictions )
    {
        int failcount = predictions. Count ( p => p > 0.5f ); //0.5보다 크면 레이블 1 = 실패
        int successCount = predictions. Count ( p => p <= 0.5f ); //0.5보다 작으면 레이블 0 = 성공

        float averagePrediction = predictions. Average ( );
        float maxPrediction = predictions. Max ( );



        // 전체 결과로 UI 업데이트
        if ( averagePrediction > 0.5f )
        {
            Debug. Log ( "동작 실패!" );
            Debug. Log ( $"총 예측 구간: {predictions. Count}" );
            Debug. Log ( $"실패 구간 수: {failcount}" );
            Debug. Log ( $"성공 구간 수: {successCount}" );
            Debug. Log ( $"최대 확률: {maxPrediction:F2}" );
            Debug. Log ( $"평균 확률: {averagePrediction:F2}" );


            resultText. text = "무지개 그리기 동작 실패입니다.";
            averagePredictionText. text = ( $"Average Prediction: {averagePrediction:F2}" );
        }
        else
        {
            Debug. Log ( "동작 성공!" );
            Debug. Log ( $"총 예측 구간: {predictions. Count}" );
            Debug. Log ( $"실패 구간 수: {failcount}" );
            Debug. Log ( $"성공 구간 수: {successCount}" );
            Debug. Log ( $"최대 확률: {maxPrediction:F2}" );
            Debug. Log ( $"평균 확률: {averagePrediction:F2}" );


            resultText. text = "무지개 그리기 동작 성공입니다.";
            averagePredictionText. text = ( $"Average Prediction: {averagePrediction:F2}" );
        }
    }

        void SaveDataToCSV ( )
    {
        try
        {
            if ( imuDataList. Count == 0 )
            {
                Debug. LogError ( "imuDataList가 비어 있습니다." );
                return;
            }

            // 보간 수행
            List<float [ ]> interpolatedData = InterpolateToTargetLength ( imuDataList , targetLength );

            if ( interpolatedData. Count == 0 )
            {
                Debug. LogError ( "보간된 데이터가 비어 있습니다." );
                return;
            }
            foreach ( var data in interpolatedData )
            {
                if ( data. Length < 7 )
                {
                    Debug. LogError ( $"잘못된 데이터 크기: {data. Length} (7이어야 함)" );
                    return;
                }
            }

            string fileName = Path. Combine ( saveFilePath , "data_" + fileCount + ".csv" );
            fileCount++;

            using ( StreamWriter writer = new StreamWriter ( fileName , false ) )
            {
                writer. WriteLine ( "TimeStamp,Accel_X,Accel_Y,Accel_Z,Gyro_X,Gyro_Y,Gyro_Z" );
                foreach ( float [ ] data in interpolatedData ) // 보간된 데이터 저장
                {
                    writer. WriteLine ( $"{data [ 0 ]},{data [ 1 ]},{data [ 2 ]},{data [ 3 ]},{data [ 4 ]},{data [ 5 ]},{data [ 6 ]}" );
                }
            }
            Debug. Log ( $"보간 데이터 저장 완료: {fileName}" );
        }
        catch ( System. Exception e )
        {
            Debug. LogError ( $"보간 데이터 저장 실패: {e. Message}" );
        }
    }



    // 터치 버튼이 눌렸는지 확인
    public bool TouchButtonPress ( )
    {
        return hapticDll. TouchButtonPress ( );
    }

    public bool TouchButtonRelease ( )
    {
        return BstickManager. Instance. TouchButtonRelease ( );
    }

    void IMU_DataReceive ( )

    {
        BstickManager. Instance. IMU_DataReceive ( );

    }

    void OnDestroy ( )
    {
        worker. Dispose ( );
    }

}