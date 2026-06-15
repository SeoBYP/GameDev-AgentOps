using System;
using System.Text;
using Unity.Plastic.Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace AgentOps.Editor
{
    /// <summary>
    /// Anthropic Messages API 의 스트리밍(SSE) 응답을 청크 단위로 파싱하는 DownloadHandler.
    /// DownloadHandlerBuffer 와 달리 응답 완성을 기다리지 않고, ReceiveData 가 호출될 때마다
    /// content_block_delta 의 text_delta 를 뽑아 onDelta 콜백으로 흘려보낸다.
    ///
    /// 사용:
    ///   www.downloadHandler = new SseDownloadHandler(delta => 화면에 append);
    ///   ...
    ///   실패 시 진단은 RawText (수신한 원문 전체) 로.
    /// </summary>
    public class SseDownloadHandler : DownloadHandlerScript
    {
        private readonly Action<string> _onDelta;   // 토큰(델타) 도착 콜백
        private string _buffer = string.Empty;       // 줄 경계용 — 청크가 줄 중간에서 끊기므로
        private readonly StringBuilder _raw = new StringBuilder(); // 원문 전체(에러 진단용)

        /// <summary>수신 원문 전체. 에러(비 SSE 응답) 시 원인 파악용.</summary>
        public string RawText => _raw.ToString();

        // 미리 할당한 버퍼로 생성 → ReceiveData 마다 새 배열 할당 안 함.
        public SseDownloadHandler(Action<string> onDelta) : base(new byte[8192])
            => _onDelta = onDelta;

        // Unity 가 청크 도착마다 호출. (메인 스레드에서 호출되므로 콜백에서 UI 갱신 안전)
        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength == 0)
                return false;

            var chunk = Encoding.UTF8.GetString(data, 0, dataLength);
            _raw.Append(chunk);
            _buffer += chunk;

            // 완성된 줄(\n 까지)만 처리하고, 마지막 미완성 조각은 버퍼에 남긴다.
            int nl;
            while ((nl = _buffer.IndexOf('\n')) >= 0)
            {
                var line = _buffer.Substring(0, nl).Trim();
                _buffer = _buffer.Substring(nl + 1);

                // SSE 는 "data: {...}" 줄만 의미 있음. event:/빈 줄/ping 은 무시.
                if (!line.StartsWith("data:"))
                    continue;

                var payload = line.Substring(5).Trim();
                if (payload.Length == 0 || payload[0] != '{')
                    continue; // "[DONE]" 류·비 JSON 방어

                JObject o;
                try { o = JObject.Parse(payload); }
                catch { continue; } // 혹시 모를 파싱 실패는 건너뜀

                // 우리가 원하는 건 텍스트 델타뿐.
                if ((string)o["type"] == "content_block_delta"
                    && (string)(o["delta"]?["type"]) == "text_delta")
                {
                    var t = (string)o["delta"]["text"];
                    if (!string.IsNullOrEmpty(t))
                        _onDelta(t);
                }
            }

            return true; // 계속 수신
        }
    }
}
