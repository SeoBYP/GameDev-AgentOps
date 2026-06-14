using System.Collections;
using System.Net;
using Unity.EditorCoroutines.Editor;
using Unity.Plastic.Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace AgentOps.Editor
{
    /// <summary>
    /// GameDev AgentOps — Unity Editor 안에서 Claude에게 묻고 답받는 채팅 창 (S1).
    /// 메뉴: Window > AgentOps > Chat
    /// </summary>
    public class AgentChatWindow : EditorWindow
    {
        private TextField _inputField;
        private Label _outputLabel;

        [MenuItem("Window/AgentOps/Chat")]
        public static void Open()
        {
            var window = GetWindow<AgentChatWindow>();
            window.titleContent = new GUIContent("AgentOps Chat");
            window.minSize = new Vector2(360, 300);
        }

        // 창이 열릴 때 한 번 호출 — 여기서 UI 트리를 조립한다 (보존모드).
        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;

            // 1) 출력 영역 — 답변이 길면 스크롤된다.
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1; // 남는 세로 공간을 다 차지
            scroll.style.marginBottom = 6;
            var border = new Color(0.3f, 0.3f, 0.3f);
            scroll.style.borderTopWidth = 1;
            scroll.style.borderBottomWidth = 1;
            scroll.style.borderLeftWidth = 1;
            scroll.style.borderRightWidth = 1;
            scroll.style.borderTopColor = border;
            scroll.style.borderBottomColor = border;
            scroll.style.borderLeftColor = border;
            scroll.style.borderRightColor = border;

            _outputLabel = new Label("여기에 Claude의 답변이 표시됩니다.");
            _outputLabel.style.whiteSpace = WhiteSpace.Normal; // 자동 줄바꿈
            _outputLabel.style.paddingTop = 4;
            _outputLabel.style.paddingBottom = 4;
            _outputLabel.style.paddingLeft = 4;
            _outputLabel.style.paddingRight = 4;
            scroll.Add(_outputLabel);
            root.Add(scroll);

            // 2) 입력 줄 (TextField + Send 버튼을 가로로)
            var inputRow = new VisualElement();
            inputRow.style.flexDirection = FlexDirection.Row;

            _inputField = new TextField();
            _inputField.style.flexGrow = 1;
            _inputField.style.marginRight = 4;
            // Enter로도 전송. TrickleDown으로 텍스트필드가 키를 먹기 전에 가로챈다.
            _inputField.RegisterCallback<KeyDownEvent>(OnInputKeyDown, TrickleDown.TrickleDown);
            inputRow.Add(_inputField);

            var sendButton = new Button(OnSend) { text = "Send" };
            inputRow.Add(sendButton);

            root.Add(inputRow);
            _inputField.Focus();
        }

        private void OnInputKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                OnSend();
                evt.StopPropagation();
                evt.StopImmediatePropagation();
            }
        }

        // Send 버튼 / Enter 공통 진입점.
        // ★ 여기 안쪽이 네가 구현할 부분.
        private void OnSend()
        {
            var prompt = _inputField.value;
            if (string.IsNullOrWhiteSpace(prompt))
                return;

            // 지금은 배선 확인용 로그만.
            Debug.Log($"[AgentOps] Send 호출됨. 입력: \"{prompt}\"");

            EditorCoroutineUtility.StartCoroutineOwnerless(OnSendWebRequest(prompt));
            
            // 모델/토큰은 설정 에셋에서: var settings = AgentOpsSettings.GetOrCreate();
            //   → settings.model, settings.maxTokens 사용.
            // TODO(너):
            //   1. UnityWebRequest 로 POST https://api.anthropic.com/v1/messages
            //      헤더: x-api-key=key / anthropic-version: 2023-06-01 / content-type: application/json
            //      본문: { model, max_tokens, messages: [{ role: "user", content: prompt }] }
            //   2. 응답 JSON content[0].text 를 _outputLabel.text 에 표시
            //   ※ HTTP는 비동기라 이 메서드를 async 로 바꾸게 됨.

            _inputField.value = string.Empty; // 입력창 비우기
            _inputField.Focus();
        }
        
        private IEnumerator OnSendWebRequest(string prompt)
        {
            
            var key = AgentOpsSettings.ApiKey;
            var settings = AgentOpsSettings.GetOrCreate(); 
            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogError("[AgentOps] API 키가 비어 있습니다. 메뉴 Window > AgentOps > Settings 에서 키를 입력하세요.");
                yield break;
            }
            
            var json = "{\"model\": \"" + settings.model + "\", \"max_tokens\": " + settings.maxTokens
                       + ", \"messages\": [{\"role\": \"user\", \"content\": \"" + prompt + "\"}]}";
            var www = new UnityWebRequest("https://api.anthropic.com/v1/messages", "POST");
            www.uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("x-api-key", key);
            www.SetRequestHeader("anthropic-version", "2023-06-01");
            www.SetRequestHeader("content-type", "application/json");
            
            Debug.Log($"[AgentOps] 요청 본문: {json}");
            
            yield return www.SendWebRequest();
            
            if (www.result != UnityWebRequest.Result.Success) {
                Debug.LogError($"[AgentOps] {(long)www.responseCode} 실패\n{www.downloadHandler.text}");
            }
            else {
                // Show results as text
                Debug.Log(www.downloadHandler.text);
 
                // Or retrieve results as binary data
                var text = JObject.Parse(www.downloadHandler.text)["content"]?[0]?["text"]?.ToString();
                _outputLabel.text = text ?? www.downloadHandler.text;
            }
        }
    }
}