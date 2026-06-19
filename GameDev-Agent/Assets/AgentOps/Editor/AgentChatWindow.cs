using System.Collections;
using System.Collections.Generic;
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
    /// GameDev AgentOps — Unity Editor 안에서 Claude와 대화하는 채팅 창
    /// (S3: tool_use 에이전트 루프 — Claude가 Unity 도구를 호출).
    /// 메뉴: Window > AgentOps > Chat
    /// </summary>
    public class AgentChatWindow : EditorWindow
    {
        private TextField _inputField;
        private ScrollView _transcript; // 메시지 말풍선이 위로 쌓이는 영역
        private AgentSession _session;
        private int _approval; // 도구 승인 상태: 0=대기중 / 1=허용 / -1=거부

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

        // 완성된 메시지를 한 번에 추가 (유저/에러/응답 텍스트용).
        private void AddMessage(string role, string text)
        {
            var body = CreateBubble(role);
            body.text = text;
            ScrollToBottom();
        }

        // (스트리밍용 — S3 에선 안 쓰지만 S2/추후를 위해 보존)
        private Label AddStreamingMessage()
            => CreateBubble("assistant");

        private void AppendDelta(Label body, string delta)
        {
            body.text += delta;
            ScrollToBottom();
        }

        // transcript 를 맨 아래로 스크롤 (레이아웃 계산 후 — 즉시 하면 높이가 0이라 안 됨).
        private void ScrollToBottom()
            => _transcript.schedule.Execute(() =>
                _transcript.scrollOffset = new Vector2(0, float.MaxValue)).ExecuteLater(1);

        // 도구 실행 승인 요청 UI (도구명 + 입력 + 허용/거부 버튼). 버튼이 _approval 을 0→1/-1 로 바꾼다.
        private void AddApprovalRequest(string toolName, string inputPreview)
        {
            _approval = 0; // 대기 상태로 초기화

            var box = new VisualElement();
            box.style.marginTop = 4;
            box.style.marginBottom = 4;
            box.style.paddingTop = 6;
            box.style.paddingBottom = 6;
            box.style.paddingLeft = 8;
            box.style.paddingRight = 8;
            box.style.borderTopLeftRadius = 8;
            box.style.borderTopRightRadius = 8;
            box.style.borderBottomLeftRadius = 8;
            box.style.borderBottomRightRadius = 8;
            box.style.alignSelf = Align.FlexStart;
            box.style.maxWidth = Length.Percent(85);
            box.style.backgroundColor = new Color(0.40f, 0.34f, 0.12f); // amber — 주의 환기
            var bc = new Color(0.70f, 0.60f, 0.20f);
            box.style.borderTopWidth = 1;
            box.style.borderBottomWidth = 1;
            box.style.borderLeftWidth = 1;
            box.style.borderRightWidth = 1;
            box.style.borderTopColor = bc;
            box.style.borderBottomColor = bc;
            box.style.borderLeftColor = bc;
            box.style.borderRightColor = bc;

            var title = new Label($"🔐 Claude가 도구 \"{toolName}\" 실행을 요청합니다");
            title.style.whiteSpace = WhiteSpace.Normal;
            title.style.marginBottom = 4;
            box.Add(title);

            if (!string.IsNullOrEmpty(inputPreview) && inputPreview != "{}")
            {
                var pre = new Label($"입력: {inputPreview}");
                pre.style.whiteSpace = WhiteSpace.Normal;
                pre.style.fontSize = 11;
                pre.style.opacity = 0.8f;
                pre.style.marginBottom = 4;
                box.Add(pre);
            }

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            var allow = new Button(() => ResolveApproval(box, 1, "✅ 허용됨")) { text = "허용" };
            var deny = new Button(() => ResolveApproval(box, -1, "🚫 거부됨")) { text = "거부" };
            allow.style.marginRight = 4;
            row.Add(allow);
            row.Add(deny);
            box.Add(row);

            _transcript.Add(box);
            ScrollToBottom();
        }

        // 결정 반영 + 버튼을 결과 텍스트로 교체(중복 클릭 방지).
        private void ResolveApproval(VisualElement box, int decision, string resultText)
        {
            _approval = decision;
            box.Clear();
            var lbl = new Label(resultText);
            lbl.style.whiteSpace = WhiteSpace.Normal;
            box.Add(lbl);
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
        private void OnSend()
        {
            var prompt = _inputField.value;
            if (string.IsNullOrWhiteSpace(prompt))
                return;

            AddMessage("user", prompt);            // UI: 유저 말풍선
            _session.AddMessage("user", prompt);   // 데이터: 세션에 누적

            EditorCoroutineUtility.StartCoroutineOwnerless(AgentLoop());

            _inputField.value = string.Empty; // 입력창 비우기
            _inputField.Focus();
        }

        /// <summary>
        /// 에이전트 루프: tool_use 가 끝날 때까지(end_turn) 반복 호출.
        /// 매 회 messages 가 늘어나므로 요청을 새로 만들어 보낸다. (비스트리밍)
        /// </summary>
        private IEnumerator AgentLoop()
        {
            var key = AgentOpsSettings.ApiKey;
            var settings = AgentOpsSettings.GetOrCreate();
            if (string.IsNullOrWhiteSpace(key))
            {
                AddMessage("error", "API 키가 비어 있습니다. Window > AgentOps > Settings 에서 입력하세요.");
                yield break;
            }

            const int maxSteps = 6; // 무한 도구 호출 방지(ch10 감각)
            for (int step = 0; step < maxSteps; step++)
            {
                // (1) 매 회 새 요청 — messages 가 늘어나고, UnityWebRequest 는 재사용 불가.
                var body = new
                {
                    model = _session.GetModel(),
                    max_tokens = settings.maxTokens,
                    system = BuildSystemPrompt(),
                    messages = _session.GetMessages(),
                    tools = UnityTools.Definitions()
                };
                string json = JsonConvert.SerializeObject(body);

                var www = new UnityWebRequest("https://api.anthropic.com/v1/messages", "POST");
                www.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                www.downloadHandler = new DownloadHandlerBuffer(); // 비스트리밍 — 전체 응답을 한 번에
                www.SetRequestHeader("x-api-key", key);
                www.SetRequestHeader("anthropic-version", "2023-06-01");
                www.SetRequestHeader("content-type", "application/json");

                yield return www.SendWebRequest();

                // (2) 실패 처리 — 본문에 원인이 들어있음
                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[AgentOps] {(long)www.responseCode} 실패\n{www.downloadHandler.text}");
                    AddMessage("error", $"{(long)www.responseCode} 실패: {www.downloadHandler.text}");
                    yield break;
                }

                // (3) 응답 파싱 — content 는 블록 배열(JArray). 통째로 세션에 저장(다음 호출에 포함).
                var res = JObject.Parse(www.downloadHandler.text);
                var content = (JArray)res["content"];
                _session.AddMessage("assistant", content);

                // (3a) 텍스트 블록은 화면 말풍선으로
                foreach (var block in content)
                    if ((string)block["type"] == "text")
                        AddMessage("assistant", (string)block["text"]);

                // (4) 도구를 안 부르면(end_turn 등) 끝.
                if ((string)res["stop_reason"] != "tool_use")
                    yield break;

                // (5) tool_use 블록마다 실행 → tool_result 모으기
                var results = new List<object>();
                foreach (var block in content)
                {
                    if ((string)block["type"] != "tool_use")
                        continue;

                    var name = (string)block["name"];
                    var input = (JObject)block["input"];
                    var toolUseId = (string)block["id"];

                    // (HITL) 승인 필요한 도구면 사용자 결정을 기다린다.
                    if (UnityTools.RequiresApproval(name))
                    {
                        AddApprovalRequest(name, input.ToString());
                        while (_approval == 0)
                            yield return null; // 버튼 누를 때까지 매 에디터 업데이트마다 대기

                        if (_approval == -1) // 거부 → 실행 안 함, Claude 에 거부 통보
                        {
                            results.Add(new
                            {
                                type = "tool_result",
                                tool_use_id = toolUseId,
                                content = "사용자가 이 도구 실행을 거부했습니다.",
                                is_error = true
                            });
                            continue;
                        }
                    }

                    AddMessage("assistant", $"🔧 도구 실행: {name}"); // UI 표시
                    string output = UnityTools.Run(name, input);       // ← Unity 도구 실행

                    results.Add(new { type = "tool_result", tool_use_id = toolUseId, content = output });
                }

                // (6) tool_result 들을 user 턴으로 넣고 루프 → Claude 가 결과 보고 이어감
                _session.AddMessage("user", results);
            }

            AddMessage("error", $"도구 호출이 너무 많아 중단했어요 (최대 {maxSteps}회).");
        }

        // system 프롬프트: 에이전트 역할 + 사용 가능한 Skills 목록.
        private static string BuildSystemPrompt()
        {
            return
                "당신은 \"GameDev AgentOps\", Unity Editor 안에서 동작하는 게임 개발 보조 에이전트입니다.\n" +
                "Unity 도구로 씬·콘솔 로그·컴파일 에러·파일을 읽고, 필요한 변경(GameObject 생성·파일 쓰기)은 도구로 수행합니다. 쓰기 작업은 사용자 승인이 필요합니다.\n" +
                "추측하지 말고, 도구로 직접 확인한 사실에 근거해 답하세요.\n\n" +
                "## Skills (전문 지침)\n" +
                "아래는 특정 작업용 상세 지침 목록입니다. 관련 작업이면 `load_skill` 도구로 먼저 해당 스킬을 불러와 그 절차를 따르세요.\n" +
                SkillRegistry.Catalog();
        }
    }
}
