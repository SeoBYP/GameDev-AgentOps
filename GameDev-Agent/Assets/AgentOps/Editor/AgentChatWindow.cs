using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private AgentProfile _profile = AgentProfiles.Builder; // 현재 모드(도구 권한)
        private bool _isRunning;          // AI 처리 중인가
        private string _queuedPrompt;     // 처리 중 입력한 대기 메시지(전송 전 취소 가능)
        private VisualElement _queueBar;  // 대기 메시지 표시줄
        private int _approvalPolicy;      // 승인 정책: 0=쓰기만 확인 / 1=전부 자동 / 2=전부 확인
        private DropdownField _sessionDropdown; // 상단 세션 목록
        private TextField _sessionSearch;       // 세션 이름 검색
        private List<string> _allSessionNames = new List<string>();
        private string _sessionFile;            // 현재 대화의 자동저장 파일 경로(null=아직 미저장)

        private static readonly string[] ModelChoices = { "claude-opus-4-8", "claude-sonnet-4-6", "claude-haiku-4-5" };
        private static readonly string[] ApprovalChoices = { "쓰기만 확인", "전부 자동 승인", "전부 확인" };

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

            // 0) 상단 — 세션 목록(드롭다운 + 검색)으로 과거 대화 전환
            var settings0 = AgentOpsSettings.GetOrCreate();

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.flexShrink = 0;
            header.style.marginBottom = 8;

            header.Add(StyleFlatButton(new Button(NewChat) { text = "+  새 대화" }));

            var searchIcon = new Label("세션");
            searchIcon.style.marginRight = 6;
            searchIcon.style.opacity = 0.6f;
            searchIcon.style.fontSize = 11;
            header.Add(searchIcon);

            _sessionSearch = new TextField();
            _sessionSearch.style.flexGrow = 1;
            _sessionSearch.style.marginRight = 6;
            _sessionSearch.tooltip = "세션 이름 검색";
            _sessionSearch.RegisterValueChangedCallback(_ => RefreshSessionDropdown());
            header.Add(_sessionSearch);

            _sessionDropdown = new DropdownField();
            _sessionDropdown.style.minWidth = 150;
            _sessionDropdown.style.flexShrink = 0;
            _sessionDropdown.tooltip = "저장된 세션 불러오기";
            _sessionDropdown.RegisterValueChangedCallback(evt =>
            {
                if (!string.IsNullOrEmpty(evt.newValue))
                    LoadSessionByName(evt.newValue);
            });
            header.Add(_sessionDropdown);
            root.Add(header);

            // 1) 대화 transcript — AddMessage() 가 여기에 말풍선을 추가한다.
            _transcript = new ScrollView(ScrollViewMode.Vertical);
            _transcript.style.flexGrow = 1; // 남는 세로 공간을 다 차지
            _transcript.style.minHeight = 0; // flex min-height:auto 함정 — 내용 많아도 내부 스크롤(상/하단 안 밀림)
            _transcript.style.marginBottom = 8;
            _transcript.style.paddingTop = 4;
            _transcript.style.paddingBottom = 4;
            _transcript.style.paddingLeft = 4;
            _transcript.style.paddingRight = 4;
            root.Add(_transcript); // 테두리 없는 열린 영역(Claude Desktop 풍)

            // 1b) 대기 메시지 표시줄 (AI 처리 중 입력 → 대기, ✕로 취소). 기본 숨김.
            _queueBar = new VisualElement();
            _queueBar.style.flexDirection = FlexDirection.Row;
            _queueBar.style.flexShrink = 0;
            _queueBar.style.marginBottom = 4;
            _queueBar.style.display = DisplayStyle.None;
            root.Add(_queueBar);

            // 2) 하단 입력 카드 (Claude Desktop 풍) — 둥근 박스 안에 입력 + 하단 컨트롤바
            var inputCard = new VisualElement();
            inputCard.style.flexShrink = 0;
            inputCard.style.paddingTop = 8;
            inputCard.style.paddingBottom = 8;
            inputCard.style.paddingLeft = 10;
            inputCard.style.paddingRight = 10;
            inputCard.style.backgroundColor = new Color(0.16f, 0.16f, 0.18f);
            SetBorderRadius(inputCard, 14);
            SetBorder(inputCard, 1, new Color(0.32f, 0.32f, 0.36f));

            // 2a) 입력창 — 카드 안에 녹아들도록 투명/무테
            _inputField = new TextField { multiline = true };
            _inputField.style.flexGrow = 1;
            _inputField.style.minHeight = 40;
            _inputField.style.maxHeight = 140;
            _inputField.style.whiteSpace = WhiteSpace.Normal;
            var innerInput = _inputField.Q("unity-text-input");
            if (innerInput != null) // TextField 의 실제 입력 요소 — 배경/테두리 제거(카드에 녹아들게)
            {
                innerInput.style.backgroundColor = Color.clear;
                SetBorder(innerInput, 0, Color.clear);
            }
            // Enter=전송 / Shift+Enter=줄바꿈. TrickleDown 으로 텍스트필드보다 먼저 가로챈다.
            _inputField.RegisterCallback<KeyDownEvent>(OnInputKeyDown, TrickleDown.TrickleDown);
            inputCard.Add(_inputField);

            var sendButton = new Button(OnSend) { text = "↑" };
            sendButton.style.width = 30;
            sendButton.style.height = 30;
            sendButton.style.marginLeft = 4;
            sendButton.style.marginRight = 0;
            sendButton.style.paddingTop = 0;
            sendButton.style.paddingBottom = 0;
            sendButton.style.paddingLeft = 0;
            sendButton.style.paddingRight = 0;
            sendButton.style.fontSize = 15;
            sendButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            sendButton.style.color = Color.white;
            sendButton.style.backgroundColor = new Color(0.85f, 0.46f, 0.34f); // Claude 코랄
            SetBorderRadius(sendButton, 15);
            SetBorder(sendButton, 0, Color.clear);
            
            
            root.Add(inputCard); // 카드는 텍스트 영역만 담는다

            // 3) 카드 "밑" 별도 컨트롤 줄 — 모드/모델/승인 칩 + 우측 동그란 ↑ 전송 (텍스트 영역 외부)
            var controlBar = new VisualElement();
            controlBar.style.flexDirection = FlexDirection.Row;
            controlBar.style.alignItems = Align.Center;
            controlBar.style.flexShrink = 0;
            controlBar.style.marginTop = 6;

            var modeDropdown = new DropdownField("", AgentProfiles.Names(), 1); // 기본 Builder
            StyleChip(modeDropdown);
            modeDropdown.RegisterValueChangedCallback(evt => _profile = AgentProfiles.ByName(evt.newValue));
            controlBar.Add(modeDropdown);

            int modelIdx = System.Array.IndexOf(ModelChoices, settings0.model);
            var modelDropdown = new DropdownField("", new List<string>(ModelChoices), modelIdx < 0 ? 0 : modelIdx);
            StyleChip(modelDropdown);
            modelDropdown.RegisterValueChangedCallback(evt =>
            {
                var s = AgentOpsSettings.GetOrCreate();
                s.model = evt.newValue;
                EditorUtility.SetDirty(s);
            });
            controlBar.Add(modelDropdown);

            var approvalDropdown = new DropdownField("", new List<string>(ApprovalChoices), 0);
            StyleChip(approvalDropdown);
            approvalDropdown.RegisterValueChangedCallback(evt => _approvalPolicy = System.Array.IndexOf(ApprovalChoices, evt.newValue));
            controlBar.Add(approvalDropdown);

            var controlSpacer = new VisualElement();
            controlSpacer.style.flexGrow = 1;
            controlBar.Add(controlSpacer);

            controlBar.Add(sendButton);
            root.Add(controlBar);

            _inputField.Focus();
            
            RestoreSession(); // 도메인 리로드 후 대화 복원(SessionState)
            RefreshSessionDropdown(); // 저장된 세션 목록 채우기
        }

        // --- Claude Desktop 풍 스타일 헬퍼 ---
        private static Button StyleFlatButton(Button b)
        {
            b.style.backgroundColor = Color.clear;
            SetBorder(b, 0, Color.clear);
            b.style.marginLeft = 0;
            b.style.marginRight = 4;
            b.style.paddingLeft = 8;
            b.style.paddingRight = 8;
            b.style.color = new Color(0.80f, 0.80f, 0.84f);
            return b;
        }

        private static void StyleChip(DropdownField d)
        {
            if (d.labelElement != null) d.labelElement.style.display = DisplayStyle.None; // 라벨 숨김(값만 표시)
            d.style.marginRight = 6;
            d.style.marginLeft = 0;
            d.style.fontSize = 11;
            d.style.flexShrink = 1;
            d.style.maxWidth = 150;
        }

        private static void SetBorderRadius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r;
            e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r;
            e.style.borderBottomRightRadius = r;
        }

        private static void SetBorder(VisualElement e, float w, Color c)
        {
            e.style.borderTopWidth = w;
            e.style.borderBottomWidth = w;
            e.style.borderLeftWidth = w;
            e.style.borderRightWidth = w;
            e.style.borderTopColor = c;
            e.style.borderBottomColor = c;
            e.style.borderLeftColor = c;
            e.style.borderRightColor = c;
        }

        /// <summary>
        /// 역할에 맞는 빈 말풍선을 만들어 transcript 에 추가하고, 본문(텍스트) Label 을 돌려준다.
        /// role: "user"(우측·파랑) / "assistant"(좌측·회색) / "error"(좌측·빨강)
        /// </summary>
        private Label CreateBubble(string role)
        {
            bool isUser = role == "user";
            bool isError = role == "error";

            // Claude Code 식 평면 transcript — 역할 라벨 + 전체폭(말풍선 없음).
            var item = new VisualElement();
            item.style.marginBottom = 10;
            item.style.paddingLeft = 4;
            item.style.paddingRight = 4;
            if (isUser) // 유저 입력만 왼쪽 강조선으로 살짝 구분
            {
                item.style.borderLeftWidth = 2;
                item.style.borderLeftColor = new Color(0.35f, 0.55f, 0.90f);
                item.style.paddingLeft = 8;
            }

            var roleLabel = new Label(isUser ? "You" : isError ? "Error" : "Claude");
            roleLabel.style.fontSize = 10;
            roleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            roleLabel.style.color =
                isError ? new Color(0.90f, 0.45f, 0.45f) :
                isUser ? new Color(0.45f, 0.65f, 1.00f) :
                new Color(0.55f, 0.80f, 0.60f);
            roleLabel.style.marginBottom = 2;
            item.Add(roleLabel);

            var body = new Label(string.Empty);
            body.style.whiteSpace = WhiteSpace.Normal; // 자동 줄바꿈
            body.enableRichText = false;               // 마크다운은 직접 정리 → rich text 끔(코드의 <> 안전)
            item.Add(body);

            _transcript.Add(item);
            ScrollToBottom();
            return body;
        }

        // 완성된 메시지를 한 번에 추가 (유저/에러/응답 텍스트용).
        private void AddMessage(string role, string text)
        {
            var body = CreateBubble(role);
            body.text = role == "assistant" ? CleanMarkdown(text) : text; // 답변만 마크다운 정리
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

        // 마크다운을 읽기 좋은 평문으로 정리(rich text 미사용): 굵게(**)·백틱(`)·헤딩(#) 마커 제거.
        private static string CleanMarkdown(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;
            var lines = s.Replace("\r", "").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var l = lines[i].TrimStart();
                if (l.StartsWith("#"))
                    lines[i] = l.TrimStart('#').TrimStart(); // 헤딩 마커 제거
            }
            return string.Join("\n", lines).Replace("**", "").Replace("`", "").TrimEnd();
        }

        // API 에러 본문(JSON)에서 error.message 만 뽑아 깔끔히. 파싱 실패 시 원문.
        private static string ExtractError(string raw)
        {
            try { return (string)(JObject.Parse(raw)["error"]?["message"]) ?? raw; }
            catch { return raw; }
        }

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
            // Enter = 전송 / Shift+Enter = 줄바꿈(가로채지 않고 TextField 가 처리)
            if ((evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) && !evt.shiftKey)
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

            _inputField.value = string.Empty; // 입력창 비우기
            _inputField.Focus();

            if (_isRunning)
            {
                // 처리 중 → 즉시 보내지 않고 대기열에 보관(기존 대기 메시지는 대체). 전송 전까지 ✕로 취소 가능.
                _queuedPrompt = prompt;
                ShowQueueBar(prompt);
                return;
            }

            StartSend(prompt);
        }

        // 실제 전송 시작(유저 말풍선 + 세션 누적 + 에이전트 루프). 완료되면 OnMainDone 으로 대기열 처리.
        private void StartSend(string prompt)
        {
            AddMessage("user", prompt);
            _session.AddMessage("user", prompt);
            PersistSession();
            _isRunning = true;
            EditorCoroutineUtility.StartCoroutineOwnerless(RunAgent(_session, _profile, "", _ => OnMainDone()));
        }

        // main 에이전트가 끝났을 때 — 처리 플래그 해제 + 대기 메시지가 있으면 자동 전송.
        private void OnMainDone()
        {
            _isRunning = false;
            if (_queuedPrompt == null)
                return;
            var p = _queuedPrompt;
            _queuedPrompt = null;
            HideQueueBar();
            StartSend(p);
        }

        // 대기 메시지 표시줄: "⏳ 대기 중: ... [✕]" — ✕ 로 전송 전 취소.
        private void ShowQueueBar(string prompt)
        {
            _queueBar.Clear();
            var lbl = new Label($"⏳ 대기 중: {prompt}");
            lbl.style.flexGrow = 1;
            lbl.style.whiteSpace = WhiteSpace.Normal;
            lbl.style.opacity = 0.85f;
            _queueBar.Add(lbl);
            _queueBar.Add(new Button(() => { _queuedPrompt = null; HideQueueBar(); }) { text = "✕" });
            _queueBar.style.display = DisplayStyle.Flex;
        }

        private void HideQueueBar()
        {
            _queueBar.Clear();
            _queueBar.style.display = DisplayStyle.None;
        }

        // 도구 실행 전 승인 필요 여부 — 승인 정책(드롭다운)을 도구 기본값에 덧씌운다.
        private bool NeedsApproval(string toolName)
        {
            switch (_approvalPolicy)
            {
                case 1: return false;                              // 전부 자동 승인
                case 2: return true;                               // 전부 확인
                default: return UnityTools.RequiresApproval(toolName); // 쓰기만 확인(기본)
            }
        }

        /// <summary>
        /// 에이전트 루프(범용): 주어진 session·profile 로 tool_use 가 끝날 때까지 반복.
        /// main 에이전트와 (S4c) sub-agent 가 공유. 최종 답변 텍스트는 onFinalText 로 전달.
        /// </summary>
        private IEnumerator RunAgent(AgentSession session, AgentProfile profile, string label, System.Action<string> onFinalText)
        {
            var key = AgentOpsSettings.ApiKey;
            var settings = AgentOpsSettings.GetOrCreate();
            if (string.IsNullOrWhiteSpace(key))
            {
                AddMessage("error", "API 키가 비어 있습니다. Window > AgentOps > Settings 에서 입력하세요.");
                onFinalText?.Invoke("[오류] API 키 없음");
                yield break;
            }

            var pfx = string.IsNullOrEmpty(label) ? "" : $"↳ [{label}] "; // sub-agent 활동 표시
            const int maxSteps = 6; // 무한 도구 호출 방지(ch10 감각)
            for (int step = 0; step < maxSteps; step++)
            {
                // (1) 매 회 새 요청 — messages 가 늘어나고, UnityWebRequest 는 재사용 불가.
                var body = new
                {
                    model = settings.model, // 모델 드롭다운(설정)을 실시간 반영
                    max_tokens = settings.maxTokens,
                    system = BuildSystemPrompt(profile),
                    messages = session.GetMessages(),
                    tools = UnityTools.Definitions(profile.allowedTools)
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
                    var raw = www.downloadHandler.text;
                    Debug.LogError($"[AgentOps] {(long)www.responseCode} 실패\n{raw}");
                    AddMessage("error", $"{(long)www.responseCode} 실패: {ExtractError(raw)}");
                    onFinalText?.Invoke($"[오류] {ExtractError(raw)}");
                    yield break;
                }

                // (3) 응답 파싱 — content 는 블록 배열(JArray). 통째로 세션에 저장(다음 호출에 포함).
                var res = JObject.Parse(www.downloadHandler.text);
                var content = (JArray)res["content"];
                session.AddMessage("assistant", content);
                if (session == _session) PersistSession(); // main 세션만 영속(sub 는 일회성)

                // (3a) 텍스트 블록은 화면 말풍선으로 + 최종 텍스트 누적
                string turnText = "";
                foreach (var block in content)
                    if ((string)block["type"] == "text")
                    {
                        var t = (string)block["text"];
                        AddMessage("assistant", pfx + t);
                        turnText += (turnText.Length > 0 ? "\n" : "") + t;
                    }

                // (4) 도구를 안 부르면(end_turn 등) 끝.
                if ((string)res["stop_reason"] != "tool_use")
                {
                    onFinalText?.Invoke(turnText);
                    yield break;
                }

                // (5) tool_use 블록마다 실행 → tool_result 모으기
                var results = new List<object>();
                foreach (var block in content)
                {
                    if ((string)block["type"] != "tool_use")
                        continue;

                    var name = (string)block["name"];
                    var input = (JObject)block["input"];
                    var toolUseId = (string)block["id"];

                    // (S4c) delegate: sub-agent 를 중첩 실행하고 그 최종 답을 결과로 받아온다.
                    if (name == "delegate")
                    {
                        var agentName = (string)input["agent"];
                        var task = (string)input["task"];
                        var subProfile = AgentProfiles.ForDelegate(agentName);
                        AddMessage("assistant", pfx + $"↳ {agentName} 에게 위임: {task}");

                        var subSession = new AgentSession(GUID.Generate().ToString(), session.GetModel());
                        subSession.AddMessage("user", task);

                        string subResult = null;
                        yield return RunAgent(subSession, subProfile, agentName, r => subResult = r); // 중첩 루프

                        results.Add(new { type = "tool_result", tool_use_id = toolUseId, content = subResult ?? "(빈 결과)" });
                        continue;
                    }

                    // (HITL) 승인 필요한 도구면 사용자 결정을 기다린다(승인 정책 반영).
                    if (NeedsApproval(name))
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

                    AddMessage("assistant", pfx + $"🔧 도구 실행: {name}"); // UI 표시
                    string output = UnityTools.Run(name, input);       // ← Unity 도구 실행

                    results.Add(new { type = "tool_result", tool_use_id = toolUseId, content = output });
                }

                // (6) tool_result 들을 user 턴으로 넣고 루프 → Claude 가 결과 보고 이어감
                session.AddMessage("user", results);
                if (session == _session) PersistSession();
            }

            AddMessage("error", $"도구 호출이 너무 많아 중단했어요 (최대 {maxSteps}회).");
            onFinalText?.Invoke("[중단] 도구 호출 한도 초과");
        }

        // system 프롬프트: 에이전트 역할 + 모드(profile) + 사용 가능한 Skills 목록.
        private string BuildSystemPrompt(AgentProfile profile)
        {
            return
                "당신은 \"GameDev AgentOps\", Unity Editor 안에서 동작하는 게임 개발 보조 에이전트입니다.\n" +
                "Unity 도구로 씬·콘솔 로그·컴파일 에러·파일을 읽고, 필요한 변경(GameObject 생성·파일 쓰기)은 도구로 수행합니다. 쓰기 작업은 사용자 승인이 필요합니다.\n" +
                "추측하지 말고, 도구로 직접 확인한 사실에 근거해 답하세요.\n" +
                "간결하게, 작업 위주로 답하세요. 불필요한 서론·맺음말·과장된 표현·'무엇을 도와드릴까요?' 같은 되묻기를 줄이세요. " +
                "의도가 명확하면 확인을 구하지 말고 바로 도구로 실행하세요(쓰기 도구는 별도 승인 게이트가 있으니 미리 묻지 마세요).\n\n" +
                profile.systemAddendum + "\n\n" +
                "## Skills (전문 지침)\n" +
                "아래는 특정 작업용 상세 지침 목록입니다. 관련 작업이면 `load_skill` 도구로 먼저 해당 스킬을 불러와 그 절차를 따르세요.\n" +
                SkillRegistry.Catalog();
        }

        // --- 세션 영속(도메인 리로드 생존): SessionState 에 JSON 으로 저장/복원 ---
        private const string SessionKey = "agentops.session";

        // 현재 대화를 SessionState(리로드 생존) + 세션 파일(히스토리)에 자동 저장.
        private void PersistSession()
        {
            try { SessionState.SetString(SessionKey, JsonConvert.SerializeObject(_session.GetMessages())); }
            catch { /* 직렬화 실패는 무시 — 연속성은 best-effort */ }
            AutoSaveSession();
        }

        // 세션 파일에 항상 자동 저장 → 상단 드롭다운(채팅 히스토리)에 반영. 파일명은 첫 메시지에서 1회 생성.
        private void AutoSaveSession()
        {
            try
            {
                if (_session.GetMessages().Count == 0)
                    return; // 빈 새 대화는 파일을 만들지 않음
                if (_sessionFile == null)
                    _sessionFile = Path.Combine(SessionsDir(), MakeSessionFileName());
                File.WriteAllText(_sessionFile, JsonConvert.SerializeObject(_session.GetMessages()));
                RefreshSessionDropdown();
            }
            catch { /* 자동 저장 실패는 무시 */ }
        }

        // 첫 user 메시지로 읽기 쉬운 파일명을 만든다(중복은 (2),(3)… 으로 회피).
        private string MakeSessionFileName()
        {
            string title = "대화";
            foreach (var m in _session.GetMessages())
                if (m.role == "user" && m.content is string s && !string.IsNullOrWhiteSpace(s)) { title = s; break; }

            foreach (var c in Path.GetInvalidFileNameChars())
                title = title.Replace(c, ' ');
            title = title.Trim();
            if (title.Length > 30) title = title.Substring(0, 30);
            if (title.Length == 0) title = "대화";

            var path = Path.Combine(SessionsDir(), title + ".json");
            for (int i = 2; File.Exists(path); i++)
                path = Path.Combine(SessionsDir(), $"{title} ({i}).json");
            return Path.GetFileName(path);
        }

        // 저장된 대화가 있으면 복원하고 말풍선을 다시 그린다. 없으면 새 세션.
        private void RestoreSession()
        {
            var model = AgentOpsSettings.GetOrCreate().model;
            _session = new AgentSession(GUID.Generate().ToString(), model);

            var json = SessionState.GetString(SessionKey, "");
            if (string.IsNullOrEmpty(json))
                return;

            try
            {
                var msgs = JsonConvert.DeserializeObject<List<ChatMessage>>(json);
                if (msgs == null)
                    return;
                foreach (var m in msgs)
                    _session.AddMessage(m);
                RebuildTranscript(msgs);
            }
            catch { /* 손상된 저장은 무시하고 빈 대화로 */ }
        }

        // 저장된 메시지로 transcript(말풍선) 재구성 — 텍스트 블록만 표시.
        private void RebuildTranscript(List<ChatMessage> msgs)
        {
            foreach (var m in msgs)
            {
                if (m.content is string s)
                {
                    AddMessage(m.role == "user" ? "user" : "assistant", s);
                }
                else if (m.content is JArray arr)
                {
                    // assistant 의 text 블록만 표시(tool_use/tool_result 는 지나간 도구 호출이라 생략)
                    foreach (var block in arr)
                        if ((string)block["type"] == "text")
                            AddMessage("assistant", (string)block["text"]);
                }
            }
        }

        // 새 대화: 저장 비우고 세션·화면 초기화.
        private void NewChat()
        {
            SessionState.EraseString(SessionKey);
            _session = new AgentSession(GUID.Generate().ToString(), AgentOpsSettings.GetOrCreate().model);
            _transcript.Clear();
            _sessionFile = null;                          // 다음 첫 메시지에서 새 파일 생성
            _sessionDropdown?.SetValueWithoutNotify(""); // 선택 표시 초기화
        }

        // --- Layer 2: 이름 붙여 파일로 저장/불러오기 (에디터 재시작 후에도 유지) ---
        // 프로젝트 루트/AgentOpsSessions/ — Assets 밖이라 Unity 가 임포트하지 않음.
        private static string SessionsDir()
        {
            var dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "AgentOpsSessions");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }

        // 상단 드롭다운에서 고른 세션(AgentOpsSessions/<name>.json)을 불러온다.
        private void LoadSessionByName(string name)
        {
            LoadSessionPath(Path.Combine(SessionsDir(), name + ".json"));
        }

        // 경로의 세션 파일을 현재 대화로 로드. 이후 메시지는 이 파일에 계속 자동 저장된다.
        private void LoadSessionPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            try
            {
                var msgs = JsonConvert.DeserializeObject<List<ChatMessage>>(File.ReadAllText(path));
                if (msgs == null)
                    return;

                _session = new AgentSession(GUID.Generate().ToString(), AgentOpsSettings.GetOrCreate().model);
                _transcript.Clear();
                foreach (var m in msgs)
                    _session.AddMessage(m);
                RebuildTranscript(msgs);
                _sessionFile = path; // 이어지는 대화는 이 파일에 자동 저장
                PersistSession();    // SessionState(연속성)에도 반영
            }
            catch
            {
                AddMessage("error", "세션 파일을 불러오지 못했습니다 (형식 오류).");
            }
        }

        // AgentOpsSessions/ 의 세션 파일 이름 목록(최근 저장 순).
        private static List<string> ListSessionNames()
        {
            return Directory.GetFiles(SessionsDir(), "*.json")
                .OrderByDescending(File.GetLastWriteTime)
                .Select(Path.GetFileNameWithoutExtension)
                .ToList();
        }

        // 세션 목록을 다시 스캔하고 검색어로 필터해 드롭다운 choices 갱신.
        private void RefreshSessionDropdown()
        {
            if (_sessionDropdown == null)
                return;

            _allSessionNames = ListSessionNames();
            var q = _sessionSearch != null ? _sessionSearch.value : "";
            var filtered = string.IsNullOrWhiteSpace(q)
                ? _allSessionNames
                : _allSessionNames.Where(n => n.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            var current = _sessionDropdown.value;
            _sessionDropdown.choices = filtered;
            if (string.IsNullOrEmpty(current) || !filtered.Contains(current))
                _sessionDropdown.SetValueWithoutNotify(""); // 선택값이 목록에 없으면 비움
        }
    }
}
