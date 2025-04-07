using UnityEngine;
using UnityEngine. UI;  // UI 업데이트를 위한 네임스페이스 추가
using Unity. Sentis;
using System;
using System. IO;
using System. Collections. Generic;
using System. Linq;

public class CSV_IMU_Predictor : MonoBehaviour
{
    public string csvFilePath;  // 예측할 CSV 파일 경로
    public ModelAsset lstmModel;  // ONNX 모델 (Unity Sentis)
    private Worker worker;

    public Text resultText; // UI 텍스트 업데이트를 위한 변수

    private const int windowSize = 10;  // LSTM 입력 시퀀스 길이
    private const int targetLength = 1348; // 학습에 사용한 보간 데이터 길이
    private float [ ] mean = new float [ ] { -0.57971025f , -0.33235549f , -0.1392433f , -27.73248365f , -12.32507753f , 1.69499003f }; // 훈련 시 사용한 평균값
    private float [ ] std = new float [ ] { 0.44397135f , 0.44205617f , 0.41637899f , 57.0969152f , 23.65217753f , 18.10137737f }; // 훈련 시 사용한 표준편차값


    void Start ( )
    {
        // Sentis ONNX 모델 로드
        var runtimeModel = ModelLoader. Load ( lstmModel );
        worker = new Worker ( runtimeModel , BackendType. CPU );

        // CSV 데이터 불러와서 예측 수행
        List<float [ ]> imuData = LoadCSV ( csvFilePath );
        if ( imuData. Count > 0 )
        {
            imuData = InterpolateToTargetLength ( imuData , targetLength ); // 보간 수행
            imuData = StandardizeData ( imuData ); // 표준화 수행
            int finalClass = PredictCSVData ( imuData ); // 최종 예측 클래스

            // 최종 예측 결과를 로그와 UI에 출력
            if ( finalClass == 0 )
            {
                Debug. Log ( "✅ 아령 들기 성공" );
                if ( resultText != null ) resultText. text = "✅ 아령 들기 성공";
            }
            else
            {
                Debug. Log ( "❌ 아령 들기 실패" );
                if ( resultText != null ) resultText. text = "❌ 아령 들기 실패";
            }
        }
        else
        {
            Debug. LogError ( "CSV 파일에서 데이터를 읽을 수 없습니다." );
        }
    }

    List<float [ ]> LoadCSV ( string filePath )
    {
        List<float [ ]> imuDataList = new List<float [ ]> ( );

        try
        {
            using ( StreamReader reader = new StreamReader ( filePath ) )
            {
                // 첫 번째 줄(헤더) 건너뛰기
                reader. ReadLine ( );

                while ( !reader. EndOfStream )
                {
                    string line = reader. ReadLine ( );
                    string [ ] values = line. Split ( ',' );

                    if ( values. Length < 7 )
                        continue; // 데이터가 불완전하면 건너뛰기

                    // TimeStamp 제외한 IMU 데이터 추출 (Accel_X ~ Gyro_Z)
                    float [ ] imuFrame = new float [ 6 ];
                    for ( int i = 1 ; i < 7 ; i++ )
                    {
                        imuFrame [ i - 1 ] = float. Parse ( values [ i ] );
                    }

                    imuDataList. Add ( imuFrame );
                }
            }
        }
        catch ( Exception e )
        {
            Debug. LogError ( "CSV 파일 읽기 오류: " + e. Message );
        }

        return imuDataList;
    }

    List<float [ ]> InterpolateToTargetLength ( List<float [ ]> data , int targetLength )
    {
        int currentLength = data. Count;
        Debug. Log ( $"현재 길이: {currentLength}, 목표 길이: {targetLength}" );

        if ( currentLength == targetLength )
            return data;

        List<float [ ]> interpolatedData = new List<float [ ]> ( );

        for ( int i = 0 ; i < targetLength ; i++ )
        {
            float t = ( float ) i / ( targetLength - 1 ) * ( currentLength - 1 );
            int index = Mathf. FloorToInt ( t );
            float alpha = t - index;

            float [ ] interpolatedFrame = new float [ 6 ];

            for ( int j = 0 ; j < 6 ; j++ )
            {
                float v1 = data [ index ] [ j ];
                float v2 = ( index + 1 < currentLength ) ? data [ index + 1 ] [ j ] : v1;
                interpolatedFrame [ j ] = Mathf. Lerp ( v1 , v2 , alpha );
            }

            interpolatedData. Add ( interpolatedFrame );
        }

        Debug. Log ( $"보간된 데이터 길이: {interpolatedData. Count}" );
        return interpolatedData;
    }

    List<float [ ]> StandardizeData ( List<float [ ]> data )
    {
        List<float [ ]> standardizedData = new List<float [ ]> ( );

        foreach ( float [ ] frame in data )
        {
            float [ ] standardizedFrame = new float [ 6 ];

            for ( int i = 0 ; i < 6 ; i++ )
            {
                standardizedFrame [ i ] = ( frame [ i ] - mean [ i ] ) / std [ i ];
            }

            standardizedData. Add ( standardizedFrame );
        }

        return standardizedData;
    }

    int PredictCSVData ( List<float [ ]> imuData )
    {
        if ( imuData. Count < windowSize )
        {
            Debug. LogError ( "데이터가 충분하지 않아 예측할 수 없습니다." );
            return -1;
        }

        List<float> predictions = new List<float> ( );

        for ( int startIdx = 0 ; startIdx <= imuData. Count - windowSize ; startIdx++ )
        {
            float [ ] inputArray = new float [ windowSize * 6 ];
            int index = 0;

            for ( int i = 0 ; i < windowSize ; i++ )
            {
                imuData [ startIdx + i ]. CopyTo ( inputArray , index );
                index += 6;
            }

            Tensor<float> inputTensor = new Tensor<float> ( new TensorShape ( 1 , windowSize , 6 ) , inputArray );
            worker. Schedule ( inputTensor );

            Tensor<float> outputTensor = worker. PeekOutput ( "dense_7" ) as Tensor<float>;
            outputTensor. ReadbackRequest ( );
            outputTensor. ReadbackAndClone ( );

            float prediction = outputTensor [ 0 ];
            predictions. Add ( prediction );

            inputTensor. Dispose ( );
            outputTensor. Dispose ( );
        }

        return AggregatePredictions ( predictions );
    }

    int AggregatePredictions ( List<float> predictions )
    {
        int class0Count = predictions. Count ( p => p <= 0.5f );
        int class1Count = predictions. Count ( p => p > 0.5f );

        return class0Count > class1Count ? 0 : 1;
    }

    List<float [ ]> ReadCSV ( string csvText )
    {
        List<float [ ]> data = new List<float [ ]> ( );
        string [ ] lines = csvText. Split ( '\n' );

        for ( int i = 1 ; i < lines. Length ; i++ ) // 첫 번째 줄 제외 (헤더)
        {
            string [ ] values = lines [ i ]. Split ( ',' );

            if ( values. Length < 7 ) continue; // 데이터 부족 시 무시

            float [ ] imuFrame = new float [ 6 ];

            for ( int j = 1 ; j <= 6 ; j++ ) // TimeStamp 제외, IMU 데이터만 가져오기
            {
                if ( float. TryParse ( values [ j ] , out float parsedValue ) )
                {
                    imuFrame [ j - 1 ] = parsedValue;
                }
            }

            data. Add ( imuFrame );
        }

        return data;
    }


    void OnDestroy ( )
    {
        worker. Dispose ( );
    }
}
