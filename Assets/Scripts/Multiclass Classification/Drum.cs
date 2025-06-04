using UnityEngine;
using Unity. Sentis;
using System. Collections. Generic;
using HapticDllCs;
using System. IO;
using System. Linq;
using TMPro;

public class Drum : MonoBehaviour
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
    public ModelAsset drumPredictionModel;  // ONNX 모델
    Worker worker;
    Tensor<float> tensor;
    private const int windowSize = 20;  // LSTM 입력 시퀀스 길이
    private const int targetLength = 327; // 학습에 사용한 보간 데이터 길이
    private const int numFeatures = 7; //학습에 사용된 피처의 개수 = Timestamp, Acc x, y, z, Gyro x, y, z
    private readonly float [ ] means = new float [ numFeatures ] { 0.94993674f,  -0.94236681f , 0.09469404f , 0.21920955f , -48.42145678f , 3.17734557f , 10.92245527f , };
    private readonly float [ ] stdDevs = new float [ numFeatures ] { 0.6001626f , 0.08435997f , 0.19742526f , 0.21477787f , 39.5622675f , 14.01326057f , 19.0116651f , };



    void Start ( )
    {
        // Sentis ONNX 모델 로드
        var runtimeModel = ModelLoader. Load ( drumPredictionModel );
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
            int index1 = Mathf. FloorToInt ( i * stepSize );
            int index2 = Mathf. Min ( index1 + 1 , currentLength - 1 );
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
            interpolatedFrame [ 0 ] = interpolatedTime;
            interpolatedFrame [ 1 ] = interpolatedAccelX;
            interpolatedFrame [ 2 ] = interpolatedAccelY;
            interpolatedFrame [ 3 ] = interpolatedAccelZ;
            interpolatedFrame [ 4 ] = interpolatedGyroX;
            interpolatedFrame [ 5 ] = interpolatedGyroY;
            interpolatedFrame [ 6 ] = interpolatedGyroZ;

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

        List<float [ ]> interpolatedData = InterpolateToTargetLength ( collectedIMUData , targetLength );
        int totalFrames = interpolatedData. Count;
        int stepSize = 10;
        int numPredictions = ( totalFrames - windowSize + 1 + stepSize - 1 ) / stepSize;

        List<int> predictions = new List<int> ( );

        for ( int startIdx = 0 ; startIdx < numPredictions * stepSize ; startIdx += stepSize )
        {
            float [ ] inputArray = new float [ windowSize * numFeatures ];
            int index = 0;

            for ( int i = startIdx ; i < startIdx + windowSize ; i++ )
            {
                for ( int j = 0 ; j < numFeatures ; j++ )  // ✅ 0부터 7개
                    inputArray [ index++ ] = interpolatedData [ i ] [ j ];
            }

            // 표준화 적용
            inputArray = StandardizeInput ( inputArray );

            using ( var tensor = new Tensor<float> ( new TensorShape ( 1 , windowSize , numFeatures ) , inputArray ) )
            {
                worker. Schedule ( tensor );

                using ( var outputTensor = worker. PeekOutput ( "output_layer" ) as Tensor<float> )
                {
                    outputTensor. ReadbackRequest ( );
                    outputTensor. ReadbackAndClone ( );

                    int numClasses = outputTensor. shape [ 1 ];
                    float maxVal = float. MinValue;
                    int predictedClass = -1;

                    for ( int c = 0 ; c < numClasses ; c++ )
                    {
                        float val = outputTensor [ 0 , c ];
                        if ( val > maxVal )
                        {
                            maxVal = val;
                            predictedClass = c;
                        }
                    }

                    predictions. Add ( predictedClass );
                }
            }
        }

        // 다중분류 예측 결과 처리
        ProcessPredictions ( predictions );
    }

    // ✅ 다중분류 결과 처리 함수
    void ProcessPredictions ( List<int> predictions )
    {
        int totalCount = predictions. Count;
        int [ ] classCounts = new int [ 5 ]; // 클래스 수에 맞게 조절

        foreach ( var p in predictions )
        {
            classCounts [ p ]++;
        }

        int majorityClass = classCounts. ToList ( ). IndexOf ( classCounts. Max ( ) );

        Debug. Log ( $"총 예측 구간: {totalCount}" );
        Debug. Log ( $"클래스별 카운트: 0:{classCounts [ 0 ]}, 1:{classCounts [ 1 ]}, 2:{classCounts [ 2 ]}, 3:{classCounts [ 3 ]}, 4:{classCounts [ 4 ]}" );

        // 클래스별 확률(%) 출력
        for ( int i = 0 ; i < classCounts. Length ; i++ )
        {
            float percentage = ( float ) classCounts [ i ] / totalCount * 100f;
            Debug. Log ( $"클래스 {i} 확률: {percentage:F2}%" );
        }

        // 평균 확률도 예시로 출력 (여긴 majority class의 확률만 표시)
        float majorityPercentage = ( float ) classCounts [ majorityClass ] / totalCount * 100f;
        averagePredictionText. text = $"Majority 확률: {majorityPercentage:F2}%";



        // ✅ Majority class별 메시지 출력
        switch ( majorityClass )
        {

            case 0:
                Debug. Log ( "북 치기 동작 성공입니다." );
                resultText. text = $"북치기 동작 성공입니다.";
                break;
            case 1:
                Debug. Log ( "실패 1: 팔의 높이가 너무 높습니다." );
                resultText. text = $"실패 1: 팔의 높이가 너무 높습니다.";
                break;
            case 2:
                Debug. Log ( "실패 2: 팔의 높이가 너무 낮습니다." );
                resultText. text = $"실패 2: 팔의 높이가 너무 낮습니다.";
                break;
            case 3:
                Debug. Log ( "실패 3: 회전 각도가 큽니다. 너무 과하게 회전하지 않도록 주의하세요!" );
                resultText. text = $"실패 3: 회전 각도가 큽니다. 너무 과하게 회전하지 않도록 주의하세요!";
                break;
            case 4:
                Debug. Log ( "실패 4: 회전 각도가 작습니다. 팔을 조금만 더 움직여서 북을 쳐 보도록 하세요!" );
                resultText. text = $"실패 4: 회전 각도가 작습니다. 팔을 조금만 더 움직여서 북을 쳐 보도록 하세요!";
                break;
            default:
                Debug. Log ( "알 수 없는 클래스입니다." );
                break;
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
