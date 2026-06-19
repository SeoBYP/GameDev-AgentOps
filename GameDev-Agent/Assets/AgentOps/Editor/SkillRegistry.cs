using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AgentOps.Editor
{
    /// <summary>
    /// Skills = 작업별 상세 지침(.md). `Assets/AgentOps/Skills/` 폴더를 스캔한다.
    /// - Catalog(): system 프롬프트에 넣을 "이름: 설명" 목록 (가볍게)
    /// - Load(name): load_skill 도구가 호출 → 해당 .md 본문 전체 반환 (필요할 때만 로딩)
    /// </summary>
    public static class SkillRegistry
    {
        private static string SkillsDir => Path.Combine(Application.dataPath, "AgentOps", "Skills");

        public struct Skill
        {
            public string name;
            public string description;
            public string path;
        }

        public static List<Skill> All()
        {
            var list = new List<Skill>();
            if (!Directory.Exists(SkillsDir))
                return list;

            foreach (var file in Directory.GetFiles(SkillsDir, "*.md"))
            {
                list.Add(new Skill
                {
                    name = Path.GetFileNameWithoutExtension(file),
                    description = FirstDescription(file),
                    path = file
                });
            }
            return list;
        }

        /// <summary>system 프롬프트용 스킬 목록 텍스트 ("- name: 설명").</summary>
        public static string Catalog()
        {
            var skills = All();
            if (skills.Count == 0)
                return "(등록된 스킬 없음)";

            var sb = new StringBuilder();
            foreach (var s in skills)
                sb.AppendLine($"- {s.name}: {s.description}");
            return sb.ToString().TrimEnd();
        }

        /// <summary>이름으로 스킬 본문 전체를 반환. (load_skill 도구)</summary>
        public static string Load(string name)
        {
            foreach (var s in All())
                if (s.name == name)
                    return File.ReadAllText(s.path);

            var available = All().ConvertAll(x => x.name);
            return $"[오류] '{name}' 스킬을 찾을 수 없습니다. 사용 가능: {string.Join(", ", available)}";
        }

        // 첫 번째 "제목(#)이 아닌" 비어있지 않은 줄 = 한 줄 설명.
        private static string FirstDescription(string file)
        {
            foreach (var line in File.ReadLines(file))
            {
                var t = line.Trim();
                if (string.IsNullOrEmpty(t) || t.StartsWith("#"))
                    continue;
                return t.TrimStart('>', ' ').Trim();
            }
            return "(설명 없음)";
        }
    }
}
