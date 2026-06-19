using UnityEngine;

/// <summary>
/// 콘솔에 Debug.Log / LogWarning / LogError 를 출력하는 테스트용 스크립트.
/// 에이전트가 콘솔 로그를 읽어 동작/에러 상태를 확인하는 용도로 사용한다.
/// </summary>
public class LogTester : MonoBehaviour
{
    [Tooltip("주기적으로 로그를 찍을지 여부")]
    public bool repeat = false;

    [Tooltip("repeat가 켜져 있을 때 로그 출력 간격(초)")]
    public float interval = 2f;

    private float _timer;
    private int _tick;

    private void Awake()
    {
        Debug.Log($"[LogTester] Awake 호출됨 - GameObject: {name}");
    }

    private void Start()
    {
        Debug.Log("[LogTester] Start 호출됨 - 정상 로그(Log) 출력 테스트");
        Debug.LogWarning("[LogTester] 경고 로그(Warning) 출력 테스트");
        Debug.LogError("[LogTester] 에러 로그(Error) 출력 테스트");
    }

    private void Update()
    {
        if (!repeat) return;

        _timer += Time.deltaTime;
        if (_timer >= interval)
        {
            _timer = 0f;
            _tick++;
            Debug.Log($"[LogTester] 반복 로그 tick={_tick}, time={Time.time:F1}s");
        }
    }
}
