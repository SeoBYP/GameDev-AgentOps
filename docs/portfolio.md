# GameDev AgentOps — 포트폴리오

> **Unity Editor 안에서 도는 AI 에이전트를 외부 프레임워크 없이 바닥부터 구현한 프로젝트.**
> Anthropic Claude Messages API를 직접 호출해 tool_use 루프·멀티에이전트 위임·HITL 승인까지 손으로 짰습니다.

![멀티에이전트 데모](../assets/s4-multiagent-demo.gif)

> Coordinator가 한 요청을 받아 Triage(분석)·Builder(생성)에게 위임 → Unity 씬을 실제로 조작 → 결과 종합.

---

## 📌 TL;DR

| 항목 | 내용 |
|------|------|
| **무엇** | Unity 에디터에 통합된 게임 개발용 AI 에이전트 — 씬·로그를 읽고 GameObject·컴포넌트를 **실제로 생성·수정** |
| **어떻게** | Claude API **직접 호출** + **직접 구현한** 에이전트 루프 (LangChain·MAF 등 에이전트 프레임워크 미사용) |
| **차별점** | 에디터 *밖*의 코딩 도구와 달리 Unity의 살아있는 맥락(열린 씬·콘솔·선택 에셋)에 직접 접근 |
| **규모** | C# · Unity Editor · 도구 13개(읽기 8/쓰기 5) · 3종 역할 모드 · 멀티에이전트 |
| **출처** | 인프런 「[바닥부터 만드는 언리얼 에이전트](https://www.inflearn.com/course/building-an-unreal-a?cid=341317)」(Rookiss·Liu)에서 익힌 에이전트 아키텍처를 **Unity로 전이·재구현** |

**한 줄 피치(이력서용):** *"Claude API를 직접 호출하는 에이전트 루프를 바닥부터 구현해, Unity 에디터 안에서 멀티에이전트가 씬을 진단·조작하도록 만든 프로젝트."*

---

## 🎯 어필 포인트 — 이 프로젝트로 증명하는 역량

| # | 역량 | 무엇으로 보여주나 |
|---|------|------------------|
| 1 | **원리 이해 (프레임워크 의존 X)** | Agent Loop·`tool_use` 프로토콜·SSE 스트리밍을 직접 구현. "도구를 쓸 줄 안다"가 아니라 **"내부가 어떻게 도는지 안다"** |
| 2 | **멀티에이전트 설계** | Coordinator가 작업을 분해해 Triage/Builder에 위임 → **중첩 코루틴**으로 sub-agent 실행 → **context 격리**(더러운 일은 sub가, 결론만 회수) |
| 3 | **안전한 자동화 (production sense)** | 모든 쓰기 작업에 **HITL 승인 게이트** + 역할별 **최소 권한**(읽기전용 모드엔 쓰기 도구가 *존재하지 않음*) + 파일 **경로 샌드박스** |
| 4 | **회복력 엔지니어링** | 429/529/5xx **지수 백오프 재시도**, 빈 메시지 가드(잘못된 400 방지), **도메인 리로드를 넘어 생존하는** 세션 영속 |
| 5 | **엔진 통합 깊이** | UI Toolkit 채팅 UI, `EditorCoroutine`로 비동기 처리, **리플렉션**으로 컴포넌트/타입을 동적 해석·조작 |
| 6 | **개념 전이 능력** | 강의는 **Unreal·C#** 기반 — 그 원리를 **엔진이 다른 Unity**에 옮겨 처음부터 재구현. 도구가 아니라 *원리*를 익혔다는 증거 |
| 7 | **문제 해결 기록** | [문제→원인→해결 로그](learning-journey.md): HTTP 상태코드 진단표, SDK 버전 불일치 대응, 코루틴/이벤트 처리 함정 등 |

---

## 🧩 핵심 구현 (기술 깊이)

**에이전트 루프 (`tool_use`)** — 매 턴 `messages`를 키워 Claude 호출 → `stop_reason == "tool_use"`면 도구 실행 후 `tool_result`를 다음 user 턴으로 회신 → 반복. `max_tokens`·무한루프 가드까지 직접 관리.

**멀티에이전트 위임** — `delegate` 도구의 *실행*이 곧 또 하나의 에이전트 루프(`yield return RunAgent(subSession, subProfile, …)`). sub-agent는 독립 세션·프로필로 작업하고 **결론만** 반환 → 메인 컨텍스트를 깨끗하게 유지. sub 프로필엔 `delegate`가 없어 **위임 깊이 1단계로 제한**.

**스트리밍 (SSE)** — `DownloadHandlerScript`를 상속한 파서로 `content_block_delta`를 실시간 표시하면서, `content_block_start`/`input_json_delta`/`message_delta`로 **tool_use·stop_reason을 재구성**해 루프에 넘김.

**권한 모델 (이중 게이트)** — ① **도구 존재**: 모드별 허용 목록으로 필터(읽기전용 모드는 쓰기 도구를 아예 못 봄) ② **실행 승인**: 쓰기 도구는 코루틴을 멈추고 사용자 결정을 대기. 승인 정책(쓰기만 확인/전부 자동/전부 확인) 선택 가능.

**Unity 도구** — 씬 계층 읽기, 이름·컴포넌트 검색, 오브젝트 상세 검사, 콘솔/컴파일 에러, 파일 읽기·쓰기, GameObject·프리미티브 생성, **컴포넌트 add/remove**(리플렉션 타입 해석 + `Undo`).

---

## 🏗 아키텍처

![아키텍처](../assets/architecture.svg)

① 에이전트 코어(채팅 → 루프 → API → 도구) · ② 멀티에이전트(Coordinator → Triage/Builder 위임).

---

## 🛠 기술 스택

- **엔진/UI**: Unity (URP) · UI Toolkit · EditorWindow · Editor Coroutines
- **LLM**: Anthropic Claude (`claude-opus-4-8`, Sonnet/Haiku 전환) — Messages API 직접 호출, **SSE 스트리밍**
- **언어/통신**: C# · `UnityWebRequest`(raw JSON) · `DownloadHandlerScript`(SSE) · Newtonsoft.Json
- **에이전트 패턴**: tool_use 루프 · 멀티에이전트(중첩 코루틴) · 세션 영속(SessionState+파일) · 리플렉션 컴포넌트/타입 해석 · 경로 샌드박스

---

## 📚 학습 배경 · 출처

- 🎓 인프런 **「[바닥부터 만드는 언리얼 에이전트](https://www.inflearn.com/course/building-an-unreal-a?cid=341317)」** (Rookiss·Liu) — Claude Code의 핵심 구조(Agent·Tool·MCP·Skill·AgentTeam)를 C#·Unreal로 from scratch 구현하는 강의. **본 프로젝트는 그 원리를 Unity 에디터에 적용해 재구현한 것.**
- 📖 [학습 여정 (문제→원인→해결)](learning-journey.md) · [챕터별 코드 레퍼런스](chapters-reference.md)
- 🧩 [microsoft/agent-framework](https://github.com/microsoft/agent-framework) · [jacking75/edu_microsoft_agent_framework_book](https://github.com/jacking75/edu_microsoft_agent_framework_book)

---

## 🔭 범위와 다음 단계 (솔직하게)

- 이 레포는 **원리를 진지하게 구현한 포폴 MVP** — 실제 배포 제품은 별도로 진행합니다. (예: `Unity.Plastic.Newtonsoft` 의존은 실배포 시 정식 패키지로 교체 예정)
- 로드맵: Unity 로그 파서 · 데이터(CSV/밸런스) 검증 · 기획 문서 RAG · **MCP 공용 도구 계층**(JSON-RPC 2.0)으로 Unity/Unreal 공용화.

> 제품 소개·설치는 [README](../README.md) 참고.
