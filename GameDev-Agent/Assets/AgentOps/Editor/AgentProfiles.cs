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

        public static readonly AgentProfile[] All = { Triage, Builder };

        public static List<string> Names() => All.Select(p => p.name).ToList();

        public static AgentProfile ByName(string name) => All.FirstOrDefault(p => p.name == name) ?? Builder;
    }
}
