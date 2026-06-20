using System.Collections.Generic;
using System.Linq;

namespace AgentOps.Editor
{
    /// <summary>에이전트 역할(모드). system 프롬프트 추가문 + 허용 도구 집합으로 권한을 분리한다.</summary>
    public class AgentProfile
    {
        public string name;
        public string systemAddendum;
        public HashSet<string> allowedTools; // null = 전체 허용
    }

    /// <summary>
    /// 사전 정의된 에이전트 프로필.
    /// - Triage: 읽기/분석 도구만 (write 도구는 목록에서 빠져 '요청 자체가 불가' = 최소권한)
    /// - Builder: 전체 도구 (쓰기는 여전히 HITL 승인)
    /// </summary>
    public static class AgentProfiles
    {
        public static readonly AgentProfile Triage = new AgentProfile
        {
            name = "Triage (읽기 전용)",
            systemAddendum =
                "현재 모드: **Triage**. 읽기/분석 도구만 제공된다. 씬이나 파일을 바꾸는 도구는 없으니 " +
                "진단과 제안에 집중하라. 변경이 필요하면 사용자에게 Builder 모드로 전환을 권하라.",
            allowedTools = new HashSet<string>
            {
                "read_active_scene",
                "find_gameobjects",
                "inspect_gameobject",
                "read_console_logs",
                "get_compile_errors",
                "read_text_file",
                "load_skill"
            }
        };

        public static readonly AgentProfile Builder = new AgentProfile
        {
            name = "Builder (생성·수정)",
            systemAddendum =
                "현재 모드: **Builder**. 모든 도구를 사용할 수 있다. 씬·파일을 만들고 수정할 수 있으며, " +
                "쓰기 작업은 사용자 승인(HITL)을 거친다.",
            allowedTools = null // 전체
        };

        public static readonly AgentProfile Coordinator = new AgentProfile
        {
            name = "Coordinator (위임)",
            systemAddendum =
                "현재 모드: **Coordinator**. 직접 작업하지 말고 전문 sub-agent에게 `delegate` 도구로 위임하라. " +
                "분석·조사는 Triage 에게, 생성·수정은 Builder 에게 위임하고, 받은 결과를 종합해 최종 답을 하라. " +
                "sub-agent는 사용자 원문을 보지 못하므로 위임 task는 구체적으로 적어라.",
            allowedTools = new HashSet<string>
            {
                "read_active_scene", "find_gameobjects", "inspect_gameobject",
                "read_console_logs", "get_compile_errors",
                "read_text_file", "load_skill", "delegate"
            }
        };

        public static readonly AgentProfile[] All = { Triage, Builder, Coordinator };

        public static List<string> Names() => All.Select(p => p.name).ToList();

        public static AgentProfile ByName(string name) => All.FirstOrDefault(p => p.name == name) ?? Builder;

        // 위임 대상 이름(Claude가 적은 agent 인자) → 프로필. triage 외엔 Builder.
        public static AgentProfile ForDelegate(string agent)
            => !string.IsNullOrEmpty(agent) && agent.ToLower().Contains("triage") ? Triage : Builder;
    }
}
