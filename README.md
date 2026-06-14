# GameDev AgentOps

> **Unity Editor 안에서 살아 움직이는 게임 개발 전용 AI 에이전트.**
> Claude를 Unity에 직접 심어, 로그 분석·데이터 검증·문서 질의 같은 반복적인 개발 운영(AgentOps) 작업을 에디터를 떠나지 않고 자동화합니다.

---

## ✨ 무엇을 만드나

게임 개발의 많은 시간은 "코드 짜기"가 아니라 **운영 잡무**에 들어갑니다 — 크래시 로그 뒤지기, CSV/밸런스 데이터 검증, 기획 문서 찾아 읽기, 빌드/테스트 결과 확인.

**GameDev AgentOps는 이 잡무를 Unity Editor 안에서 AI에게 맡깁니다.**

- 기존 AI 코딩 도구는 에디터 **밖**(웹/CLI)에 있어 Unity의 맥락(열린 씬, 콘솔 로그, 선택한 에셋)을 모릅니다.
- GameDev AgentOps는 에이전트를 **에디터 안에서** 돌려, 그 맥락에 직접 접근하고 Unity 특화 도구로 행동합니다.
- 외부 프레임워크나 벤더 CLI에 묶이지 않고 **Anthropic Claude API를 직접 호출** — 가볍고, 비용이 투명하며, 원하는 만큼 특화할 수 있습니다.

> 한 줄로: **"Unity 개발자를 위한, Unity 안에서 도는 Claude 에이전트."**

---

## 🧠 동작 방식

```
┌────────────────────── Unity Editor ──────────────────────┐
│                                                          │
│   채팅 UI (UI Toolkit)        Unity 컨텍스트/도구          │
│   ┌──────────────┐           ┌───────────────────────┐   │
│   │ 질문 / 명령  │──────────▶│ 로그·씬·에셋·데이터    │   │
│   └──────┬───────┘           └───────────▲───────────┘   │
│          │                                │ (도구 실행)   │
│          ▼                                │              │
│   ┌──────────────────────────────────────┴───────────┐  │
│   │   에이전트 루프 (요청 → tool_use → 결과 → 반복)    │  │
│   └──────────────────────┬───────────────────────────┘  │
└──────────────────────────┼──────────────────────────────┘
                           │ x-api-key (EditorPrefs)
                           ▼
                 Anthropic Claude Messages API
```

- **두뇌**는 Claude, **손발**은 Unity 에디터 도구. 에이전트 루프가 둘을 잇습니다.
- API 키는 코드/에셋이 아니라 **EditorPrefs**(머신 로컬)에만 저장 — 레포에 절대 커밋되지 않습니다.

---

## 🗺️ 로드맵

| 단계 | 내용 | 상태 |
|------|------|------|
| **S1** | Hello Claude in Unity — 에디터 채팅 창에서 Claude 단발 호출·응답 | ✅ 완료 |
| **S2** | 멀티턴 대화 + 스트리밍 실시간 출력 | 🔜 진행 예정 |
| **S3** | Tool 루프 + Unity 도구(로그 읽기 등) + 실행 승인(HITL) | ⏳ 계획 |
| **S4** | 작업별 Skills 로딩 + 역할 분담 Multi-Agent 팀 | ⏳ 계획 |

장기 목표: Unity 로그 파서 · 데이터(CSV/밸런스) 검증 · 기획 문서 RAG · MCP 공용 도구 계층을 갖춘 **게임 개발 AgentOps 허브**.

---

## 🚀 시작하기

1. `GameDev-Agent/`를 Unity Hub로 열기 (URP · Unity 2022.3+ / Unity 6)
2. 메뉴 **Window → AgentOps → Settings** → Inspector의 **API Key** 칸에 Anthropic 키 입력
   - 키 발급: [console.anthropic.com](https://console.anthropic.com) (API는 **선불 크레딧** 충전 필요 — 구독과 별개 지갑)
3. 메뉴 **Window → AgentOps → Chat** → 질문 입력 후 **Send / Enter**

> 의존 패키지(Editor Coroutines, Newtonsoft Json)는 `Packages/manifest.json`에 기록되어 자동 복원됩니다.

---

## 🔐 보안 — API 키

키는 어떤 추적 파일에도 들어가지 않습니다. `ScriptableObject`는 비밀이 아닌 설정(`model`/`maxTokens`)만 담고, **API 키는 Unity EditorPrefs(`agentops.apiKey`)에만** 저장됩니다 → git 커밋 대상 아님.

---

## 🛠️ 기술 스택

- **엔진/UI**: Unity (URP) · UI Toolkit · EditorWindow
- **LLM**: Anthropic Claude (`claude-opus-4-8`) — Messages API 직접 호출
- **언어**: C#
- **라이브러리**: `UnityWebRequest`(raw JSON) · Newtonsoft.Json · Unity Editor Coroutines

---

## 📚 배경

이 프로젝트는 **Microsoft Agent Framework(C#) 학습 과정**(Agent Loop · Tool Calling · Session · RAG · Multi-Agent · MCP)을 바닥부터 구현하며 익힌 원리를 토대로 합니다. 학습 단계 코드(`GameDev-AgentOps/`, `GameDev-AgentOps.McpServer/`)와 상세 기록은 별도 문서에 정리되어 있습니다.

- 📖 [학습 여정 (문제→원인→해결)](docs/learning-journey.md)
- 📑 [챕터별 코드 레퍼런스](docs/chapters-reference.md)
