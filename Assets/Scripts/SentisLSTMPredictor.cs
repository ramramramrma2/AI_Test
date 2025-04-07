using UnityEngine;
using Unity. Sentis;
using System. Collections. Generic;
using System. Threading. Tasks;
using HapticDllCs;
using System. IO;

public class SentisLSTMPredictor : MonoBehaviour
{

    private bool isMeasuring = false; // 데이터 측정 여부
    private List<float [ ]> collectedIMUData = new List<float [ ]> ( ); // 측정된 데이터를 저장할 리스트

    [Header ( "IMU" )]
    HapticDll hapticDll;
    public List<float> AccelList; // 가속도 데이터 (x, y, z)
    public List<float> GyroList; // 자이로스코프 데이터 (x, y, z 각속도)



    public ModelAsset onnxModel;  // ONNX 모델 (Unity Sentis)
    Worker worker;
    Tensor<float> tensor;

    private Queue<float [ ]> imuDataQueue = new Queue<float [ ]> ( );  // 시계열 데이터를 저장할 큐
    private const int windowSize = 10;  // LSTM 입력 시퀀스 길이 (10 프레임)


    void Start ( )
    {
        // Sentis ONNX 모델 로드
        var runtimeModel = ModelLoader. Load ( onnxModel );
        worker = new Worker ( runtimeModel , BackendType. CPU );

        // 초기화
        hapticDll = new HapticDll ( );
        hapticDll. InitializeHapticDevice ( 1 , Debug. Log );

    }

    void Update ( )
    {
        // 버튼이 눌렸을 때 측정 시작/종료 토글
        if ( TouchButtonRelease ( ) )
        {
            ToggleMeasurement ( );
        }

        if ( isMeasuring )
        {
            CollectIMUData ( ); // 데이터를 리스트에 저장
        }
    }

    void ToggleMeasurement ( )
    {
        if ( !isMeasuring ) // 측정 시작
        {
            isMeasuring = true;
            collectedIMUData. Clear ( ); // 기존 데이터 초기화
            Debug. Log ( "측정 시작" );
        }
        else // 측정 종료 및 예측 실행
        {
            isMeasuring = false;
            Debug. Log ( "측정 종료, 예측 시작" );

            if ( collectedIMUData. Count >= windowSize )
            {
                PredictSuccessAsync ( collectedIMUData );
            }
            else
            {
                Debug. LogWarning ( "데이터가 충분하지 않습니다." );
            }
        }
    }

    void CollectIMUData ( )
    {
        IMU_DataReceive ( ); // IMU 데이터 갱신

        // 현재 IMU 데이터 저장
        float [ ] imuFrame = {
            AccelList[0], AccelList[1], AccelList[2],  // 가속도
            GyroList[0], GyroList[1], GyroList[2]      // 자이로스코프
        };

        collectedIMUData. Add ( imuFrame );
    }

    async void PredictSuccessAsync ( List<float [ ]> collectedIMUData )
    {
        float [ ] inputArray = new float [ windowSize * 6 ]; // 6 (IMU 데이터 수)
        int index = 0;

        // 최신 windowSize 개수의 데이터만 사용
        int startIndex = Mathf. Max ( 0 , collectedIMUData. Count - windowSize );
        for ( int i = startIndex ; i < collectedIMUData. Count ; i++ )
        {
            collectedIMUData [ i ]. CopyTo ( inputArray , index );
            index += 6;
        }

        tensor = new Tensor<float> ( new TensorShape ( 1 , windowSize , 6 ) , inputArray );
        worker. Schedule ( tensor );

        Tensor<float> outputTensor = worker. PeekOutput ( "dense_3" ) as Tensor<float>;
        outputTensor. ReadbackRequest ( );

        while ( !outputTensor. IsReadbackRequestDone ( ) )
        {
            await Task. Yield ( );
        }

        Tensor<float> clonedTensor = outputTensor. ReadbackAndClone ( );
        float prediction = outputTensor [ 0 ];

        tensor. Dispose ( );
        clonedTensor. Dispose ( );

        // 예측 결과 출력
        if ( prediction > 0.5f )
        {
            Debug. Log ( $"✅ 동작 성공! (확률: {prediction:F2})" );
            //successPanel. SetActive ( true );
            //failurePanel. SetActive ( false );
        }
        else
        {
            Debug. Log ( $"❌ 동작 실패! (확률: {prediction:F2})" );
            //successPanel. SetActive ( false );
            //failurePanel. SetActive ( true);
        }
    }

    // 터치 버튼이 눌렸는지 확인
    public bool TouchButtonPress ( )
    {
        return hapticDll. TouchButtonPress ( );
    }

    public bool TouchButtonRelease ( )
    {
        return hapticDll. TouchButtonRelease ( );
    }

    void IMU_DataReceive ( )

    {
        AccelList = hapticDll. GetAccelerometer ( );
        GyroList = hapticDll. GetGyroscope ( );
    }

    void OnDestroy ( )
    {
        worker. Dispose ( );
    }

}