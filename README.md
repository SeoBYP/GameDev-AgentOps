# GameDev AgentOps

> **Unity Editor 안에서 살아 움직이는 게임 개발 전용 AI 에이전트.**
> Claude를 Unity에 직접 심어, 씬 조작·로그 분석·데이터 검증 같은 반복적인 개발 운영(AgentOps) 작업을 에디터를 떠나지 않고 자동화합니다.

![GameDev AgentOps — 멀티에이전트 데모](assets/s4-multiagent-demo.gif)

> 위 데모: **Coordinator** 모드에서 한 번의 요청으로 — Triage가 씬을 분석하고, Builder가 GameObject 생성·컴포넌트 부착까지 위임받아 처리한다. 모든 쓰기는 **사용자 승인(HITL)** 을 거치고, 결과는 Coordinator가 종합한다.

---

## ✨ 무엇을 만드나

게임 개발의 많은 시간은 "코드 짜기"가 아니라 **운영 잡무**에 들어갑니다 — 크래시 로그 뒤지기, 씬 점검·정리, 데이터 검증, 기획 문서 찾아 읽기.

**GameDev AgentOps는 이 잡무를 Unity Editor 안에서 AI에게 맡깁니다.**

- 기존 AI 코딩 도구는 에디터 **밖**(웹/CLI)에 있어 Unity의 맥락(열린 씬, 콘솔 로그, 선택한 에셋)을 모릅니다.
- GameDev AgentOps는 에이전트를 **에디터 안에서** 돌려, 그 맥락에 직접 접근하고 Unity 특화 도구로 **실제로 씬을 바꿉니다**.
- 외부 프레임워크나 벤더 CLI에 묶이지 않고 **Anthropic Claude API를 직접 호출** — 가볍고, 비용이 투명하며, 원하는 만큼 특화할 수 있습니다.

> 한 줄로: **"Unity 개발자를 위한, Unity 안에서 도는 Claude 에이전트."**

---

## 🚀 주요 기능

- **실시간 스트리밍** — 응답을 토큰 단위로 흘려 보여줌 (SSE)
- **Unity 도구 실행** — 씬·콘솔·컴파일 에러·파일을 읽고, GameObject·컴포넌트·프리미티브를 만들고 수정
- **HITL 승인 게이트** — 모든 쓰기 작업은 실행 전 사용자 허용/거부. 승인 정책(쓰기만 확인 / 전부 자동 / 전부 확인) 선택 가능
- **역할별 권한 분리** — Triage(읽기 전용) · Builder(전체) · Coordinator(위임) 모드. 모드에 없는 도구는 **요청 자체가 불가**(최소 권한)
- **멀티에이전트 위임** — Coordinator가 작업을 쪼개 Triage·Builder sub-agent에게 위임하고 결과를 종합 (context 격리)
- **Skills (점진적 공개)** — 작업별 상세 지침을 `load_skill`로 필요할 때만 로딩. 스킬 추가 = `.md` 파일 떨구기
- **세션 히스토리** — 대화 자동 저장 + 상단 드롭다운·검색으로 과거 대화 복원
- **모델 선택 / 회복력** — Opus·Sonnet·Haiku 전환, 일시 오류(429/529/5xx)는 지수 백오프 자동 재시도
- **마크다운 표 → 정렬 그리드** 렌더, 멀티라인 입력(Enter 전송 / Shift+Enter 줄바꿈)

---

## 🧠 동작 방식

![GameDev AgentOps 아키텍처](assets/architecture.svg)

<sub>① 에이전트 코어(파랑): 채팅창 → Agent Loop ↔ Claude API → 도구, HITL 승인·프로필·Skills·세션영속 · ② 멀티에이전트(보라): Coordinator가 Triage/Builder sub-agent에 위임</sub>

<details><summary>텍스트 요약 다이어그램</summary>

```
┌────────────────────── Unity Editor ──────────────────────┐
│                                                          │
│   채팅 UI (UI Toolkit)        Unity 컨텍스트/도구          │
│   ┌──────────────┐           ┌───────────────────────┐   │
│   │ 질문 / 명령  │──────────▶│ 씬·로그·에셋·컴포넌트  │   │
│   └──────┬───────┘           └───────────▲───────────┘   │
│          │                                │ (도구 실행)   │
│          ▼                                │              │
│   ┌──────────────────────────────────────┴───────────┐  │
│   │   에이전트 루프 (요청 → tool_use → 결과 → 반복)    │  │
│   └──────────────────────┬───────────────────────────┘  │
└──────────────────────────┼──────────────────────────────┘
                           │ x-api-key (EditorPrefs)
                           ▼
                 Anthropic Claude Messages API (SSE 스트리밍)
```

</details>

- **두뇌**는 Claude, **손발**은 Unity 에디터 도구. 에이전트 루프가 둘을 잇습니다.
- API 키는 코드/에셋이 아니라 **EditorPrefs**(머신 로컬)에만 저장 — 레포에 절대 커밋되지 않습니다.

---

## 🧰 에이전트 도구

### 읽기 도구 — 자동 실행 (승인 불필요)
| 도구 | 설명 |
|------|------|
| `read_active_scene` | 활성 씬의 GameObject 계층(트리) |
| `find_gameobjects` | 이름·컴포넌트 타입으로 검색 (비활성 포함) |
| `inspect_gameobject` | 오브젝트 상세 — 경로·활성·태그·레이어·Transform·컴포넌트 |
| `read_console_logs` | 콘솔 로그(런타임 Debug/경고/에러) |
| `get_compile_errors` | 스크립트 컴파일 에러 목록 |
| `read_text_file` | `Assets/` 밑 텍스트·스크립트 읽기 (경로 샌드박스) |
| `load_skill` | 작업별 상세 지침(Skill) 로딩 |
| `delegate` | 전문 sub-agent에 작업 위임 (Coordinator 전용) |

### 쓰기 도구 — 사용자 승인(HITL)
| 도구 | 설명 |
|------|------|
| `create_gameobject` | 빈 GameObject 생성 |
| `create_primitive` | 보이는 도형 생성 (Cube/Sphere/Capsule/Cylinder/Plane/Quad) |
| `set_primitive_mesh` | 기존 오브젝트를 도형 모양으로 보이게 (MeshFilter·MeshRenderer 설정) |
| `add_component` / `remove_component` | 컴포넌트 추가·제거 (타입명 리플렉션 해석) |
| `write_file` | `Assets/` 밑 파일 생성·덮어쓰기 (`.cs` 포함, 샌드박스) |

> 도구는 모두 `UnityTools.cs`에 정의되며, 모드별 `AgentProfiles`의 허용 목록으로 필터링됩니다.

![HITL 승인 데모](assets/s3-agent-hitl-demo.gif)

<sub>쓰기 도구(예: `create_gameobject`)는 실행 전 허용/거부를 묻는다 — 읽기는 자동, 쓰기는 사람이 승인.</sub>

---

## 🗺️ 로드맵

| 단계 | 내용 | 상태 |
|------|------|------|
| **S1** | Hello Claude in Unity — 에디터 채팅 창에서 Claude 단발 호출·응답 | ✅ 완료 |
| **S2** | 멀티턴 대화 + 스트리밍 실시간 출력 | ✅ 완료 |
| **S3** | Tool 루프 + Unity 도구(씬 읽기·GameObject 생성) + 실행 승인(HITL) | ✅ 완료 |
| **S4** | Skills 로딩 + 역할별 권한 분리 + 멀티에이전트 위임(Coordinator→sub-agent) | ✅ 완료 |
| **+** | 실전 도구 확장(검색·검사·컴포넌트·프리미티브) · 세션 히스토리 · 스트리밍 에이전트 루프 | ✅ 완료 |

장기 목표: Unity 로그 파서 · 데이터(CSV/밸런스) 검증 · 기획 문서 RAG · MCP 공용 도구 계층을 갖춘 **게임 개발 AgentOps 허브**.

---

## 🚀 시작하기

1. `GameDev-Agent/`를 Unity Hub로 열기 (URP · Unity 2022.3+ / Unity 6)
2. 메뉴 **Window → AgentOps → Settings** → Inspector의 **API Key** 칸에 Anthropic 키 입력
   - 키 발급: [console.anthropic.com](https://console.anthropic.com) (API는 **선불 크레딧** 충전 필요 — 구독과 별개 지갑)
3. 메뉴 **Window → AgentOps → Chat** → 모드 선택 후 질문 입력
   - **Enter** = 전송 / **Shift+Enter** = 줄바꿈
   - 상단 드롭다운으로 과거 세션 불러오기, 하단 칩으로 모드·모델·승인 정책 변경

> 의존 패키지(Editor Coroutines, Newtonsoft Json)는 `Packages/manifest.json`에 기록되어 자동 복원됩니다.

---

## 🔐 보안 — API 키

키는 어떤 추적 파일에도 들어가지 않습니다. `ScriptableObject`는 비밀이 아닌 설정(`model`/`maxTokens`)만 담고, **API 키는 Unity EditorPrefs(`agentops.apiKey`)에만** 저장됩니다 → git 커밋 대상 아님. 파일 도구는 `Assets/` 밖을 막는 경로 샌드박스(`IsSafePath`)를 거칩니다.

---

## 🛠️ 기술 스택

- **엔진/UI**: Unity (URP) · UI Toolkit · EditorWindow · Editor Coroutines
- **LLM**: Anthropic Claude (`claude-opus-4-8` 기본, Sonnet/Haiku 전환) — Messages API 직접 호출, **SSE 스트리밍**
- **언어**: C#
- **라이브러리**: `UnityWebRequest`(raw JSON · `DownloadHandlerScript` SSE 파서) · Newtonsoft.Json
- **에이전트**: tool_use 루프 · 멀티에이전트(중첩 코루틴) · 세션 영속(SessionState + 파일) · 리플렉션 기반 컴포넌트/타입 해석

---

## 📚 배경

이 프로젝트는 **Microsoft Agent Framework(C#) 학습 과정**(Agent Loop · Tool Calling · Session · RAG · Multi-Agent · MCP)을 바닥부터 구현하며 익힌 원리를 토대로 합니다. 학습 단계 코드(`GameDev-AgentOps/`, `GameDev-AgentOps.McpServer/`)와 상세 기록은 별도 문서에 정리되어 있습니다.

**학습 기록**
- 📖 [학습 여정 (문제→원인→해결)](docs/learning-journey.md)
- 📑 [챕터별 코드 레퍼런스](docs/chapters-reference.md)

**참고한 자료**
- 🎓 [jacking75/edu_microsoft_agent_framework_book](https://github.com/jacking75/edu_microsoft_agent_framework_book) — 챕터별(01~11) C# 튜토리얼. 본 학습 단계가 이 책을 따라 진행됨.
- 🎮 [jacking75/edu_microsoft_agent_framework](https://github.com/jacking75/edu_microsoft_agent_framework) — 게임 개발 15스테이지 커리큘럼·목표 아키텍처. MVP 방향의 참고.
- 🧩 [microsoft/agent-framework](https://github.com/microsoft/agent-framework) — Microsoft Agent Framework 공식 레포 (`Microsoft.Agents.AI`).
- 📚 [Microsoft Agent Framework 공식 문서](https://learn.microsoft.com/en-us/agent-framework/) — Microsoft Learn.
