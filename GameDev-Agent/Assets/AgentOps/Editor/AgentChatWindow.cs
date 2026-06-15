using System.Collections;
using System.Net;
using AgentOps.Sessions;
using Unity.EditorCoroutines.Editor;
using Unity.Plastic.Newtonsoft.Json;
using Unity.Plastic.Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace AgentOps.Editor
{
    /// <summary>
    /// GameDev AgentOps — Unity Editor 안에서 Claude와 대화하는 채팅 창 (S2: 멀티턴 UI).
    /// 메뉴: Window > AgentOps > Chat
    /// UI(말풍선 transcript)는 이 파일에서, 대화 기록 List/history 전송 로직은 네가 채운다.
    /// </summary>
    public class AgentChatWindow : EditorWindow
    {
        private TextField _inputField;
        private ScrollView _transcript; // 메시지 말풍선이 위로 쌓이는 영역
        private AgentSession _session;

        [MenuItem("Window/AgentOps/Chat")]
        public static void Open()
        {
            var window = GetWindow<AgentChatWindow>();
            window.titleContent = new GUIContent("AgentOps Chat");
            window.minSize = new Vector2(360, 320);
        }

        // 창이 열릴 때 한 번 호출 — 여기서 UI 트리를 조립한다 (보존모드).
        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;

            // 1) 대화 transcript — AddMessage() 가 여기에 말풍선을 추가한다.
            _transcript = new ScrollView(ScrollViewMode.Vertical);
            _transcript.style.flexGrow = 1; // 남는 세로 공간을 다 차지
            _transcript.style.marginBottom = 6;
            _transcript.style.paddingTop = 4;
            _transcript.style.paddingBottom = 4;
            _transcript.style.paddingLeft = 4;
            _transcript.style.paddingRight = 4;
            var border = new Color(0.3f, 0.3f, 0.3f);
            _transcript.style.borderTopWidth = 1;
            _transcript.style.borderBottomWidth = 1;
            _transcript.style.borderLeftWidth = 1;
            _transcript.style.borderRightWidth = 1;
            _transcript.style.borderTopColor = border;
            _transcript.style.borderBottomColor = border;
            _transcript.style.borderLeftColor = border;
            _transcript.style.borderRightColor = border;
            root.Add(_transcript);

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

            _session = new AgentSession(GUID.Generate().ToString(), AgentOpsSettings.GetOrCreate().model);
        }

        /// <summary>
        /// 역할에 맞는 빈 말풍선을 만들어 transcript 에 추가하고, 본문(텍스트) Label 을 돌려준다.
        /// role: "user"(우측·파랑) / "assistant"(좌측·회색) / "error"(좌측·빨강)
        /// </summary>
        private Label CreateBubble(string role)
        {
            bool isUser = role == "user";
            bool isError = role == "error";

            var bubble = new VisualElement();
            bubble.style.marginTop = 4;
            bubble.style.marginBottom = 4;
            bubble.style.paddingTop = 6;
            bubble.style.paddingBottom = 6;
            bubble.style.paddingLeft = 8;
            bubble.style.paddingRight = 8;
            bubble.style.borderTopLeftRadius = 8;
            bubble.style.borderTopRightRadius = 8;
            bubble.style.borderBottomLeftRadius = 8;
            bubble.style.borderBottomRightRadius = 8;
            bubble.style.maxWidth = Length.Percent(85);
            bubble.style.alignSelf = isUser ? Align.FlexEnd : Align.FlexStart;
            bubble.style.backgroundColor =
                isError ? new Color(0.45f, 0.20f, 0.20f) :
                isUser ? new Color(0.18f, 0.32f, 0.50f) :
                new Color(0.24f, 0.24f, 0.26f);

            var roleLabel = new Label(isUser ? "You" : isError ? "Error" : "Claude");
            roleLabel.style.fontSize = 10;
            roleLabel.style.opacity = 0.6f;
            roleLabel.style.marginBottom = 2;
            bubble.Add(roleLabel);

            var body = new Label(string.Empty);
            body.style.whiteSpace = WhiteSpace.Normal; // 자동 줄바꿈
            bubble.Add(body);

            _transcript.Add(bubble);
            ScrollToBottom();
            return body;
        }

        // 완성된 메시지를 한 번에 추가 (비스트리밍 — 유저/에러/단발 응답용).
        private void AddMessage(string role, string text)
        {
            var body = CreateBubble(role);
            body.text = text;
            ScrollToBottom();
        }

        // 스트리밍용: 빈 assistant 말풍선을 만들고, 델타를 이어붙일 Label 을 돌려준다.
        private Label AddStreamingMessage()
            => CreateBubble("assistant");

        // 스트리밍 델타 한 조각을 본문 Label 에 이어붙이고 하단으로 스크롤.
        private void AppendDelta(Label body, string delta)
        {
            body.text += delta;
            ScrollToBottom();
        }

        // transcript 를 맨 아래로 스크롤 (레이아웃 계산 후 — 즉시 하면 높이가 0이라 안 됨).
        private void ScrollToBottom()
            => _transcript.schedule.Execute(() =>
                _transcript.scrollOffset = new Vector2(0, float.MaxValue)).ExecuteLater(1);

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
        private void OnSend()
        {
            var prompt = _inputField.value;
            if (string.IsNullOrWhiteSpace(prompt))
                return;

            AddMessage("user", prompt); // UI: 유저 말풍선
            _session.AddMessage("user", prompt);
            
            Debug.Log($"[AgentOps] Send 호출됨. 입력: \"{prompt}\"");
            EditorCoroutineUtility.StartCoroutineOwnerless(OnSendWebRequest());

            _inputField.value = string.Empty; // 입력창 비우기
            _inputField.Focus();
        }

        private IEnumerator OnSendWebRequest()
        {
            var key = AgentOpsSettings.ApiKey;
            var settings = AgentOpsSettings.GetOrCreate();
            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogError("[AgentOps] API 키가 비어 있습니다. 메뉴 Window > AgentOps > Settings 에서 키를 입력하세요.");
                AddMessage("error", "API 키가 비어 있습니다. Window > AgentOps > Settings 에서 입력하세요.");
                yield break;
            }

            var body = new
            {
                model = _session.GetModel(),
                max_tokens = settings.maxTokens,
                messages = _session.GetMessages(),
                stream = true
            };
            string json = JsonConvert.SerializeObject(body);
            
            var streamBody = AddStreamingMessage();                          // 빈 Claude 풍선
            var handler = new SseDownloadHandler(delta => AppendDelta(streamBody, delta));
            
            var www = new UnityWebRequest("https://api.anthropic.com/v1/messages", "POST");
            www.uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            www.downloadHandler = handler;      
            
            www.SetRequestHeader("x-api-key", key);
            www.SetRequestHeader("anthropic-version", "2023-06-01");
            www.SetRequestHeader("content-type", "application/json");
            yield return www.SendWebRequest();
            
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[AgentOps] {(long)www.responseCode} 실패\n{handler.RawText}");
                AddMessage("error", $"{(long)www.responseCode} 실패: {handler.RawText}");
            }
            else
            {
                // 풍선 Label 에 이미 전체 답변이 누적돼 있음 → 그대로 세션에 저장
                _session.AddMessage("assistant", streamBody.text);
            }
        }
    }
}