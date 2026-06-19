using System;
using System.IO;
using System.Text;
using Unity.Plastic.Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AgentOps.Editor
{
    /// <summary>
    /// Claude 가 호출할 수 있는 Unity 도구 모음.
    /// read: read_active_scene / read_console_logs / get_compile_errors / read_text_file
    /// write: create_gameobject
    /// - Definitions(): Claude 에게 보낼 도구 "스키마"(요청 body 의 tools 에 넣음)
    /// - Run(name, input): tool_use 가 오면 실제로 실행하고 결과 문자열을 돌려줌
    /// - RequiresApproval(name): read-only=자동 / write=승인(HITL)
    /// </summary>
    public static class UnityTools
    {
        /// <summary>요청 body 의 "tools" 에 그대로 넣을 도구 정의 배열.</summary>
        public static object[] Definitions() => new object[]
        {
            new
            {
                name = "read_active_scene",
                description = "현재 Unity 에디터에 열린 활성 씬의 GameObject 계층(이름)을 반환한다. 인자 없음.",
                input_schema = new
                {
                    type = "object",
                    properties = new { },
                    required = new string[] { }
                }
            },
            new
            {
                name = "read_console_logs",
                description = "Unity 콘솔의 최근 로그(런타임 Debug.Log/경고/에러/예외)를 읽는다. 에러 원인 분석(triage)에 사용.",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        count = new { type = "integer", description = "가져올 최근 로그 개수 (기본 30)" },
                        level = new { type = "string", description = "필터: all | error | warning (기본 all)" }
                    },
                    required = new string[] { }
                }
            },
            new
            {
                name = "get_compile_errors",
                description = "현재 스크립트 컴파일 에러 목록(파일·줄·메시지)을 반환한다. 인자 없음.",
                input_schema = new
                {
                    type = "object",
                    properties = new { },
                    required = new string[] { }
                }
            },
            new
            {
                name = "read_text_file",
                description = "Assets/ 폴더 밑의 텍스트 파일(스크립트 등) 내용을 읽는다. 경로는 프로젝트 루트 기준 상대경로(예: Assets/AgentOps/Editor/UnityTools.cs).",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Assets/ 밑 상대 경로" }
                    },
                    required = new[] { "path" }
                }
            },
            new
            {
                name = "load_skill",
                description = "특정 작업용 상세 지침(Skill)을 불러온다. system 프롬프트의 'Skills' 목록에 있는 이름을 사용. 관련 작업을 시작하기 전에 먼저 호출하라.",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        name = new { type = "string", description = "불러올 스킬 이름" }
                    },
                    required = new[] { "name" }
                }
            },
            new
            {
                name = "write_file",
                description = "Assets/ 폴더 밑에 텍스트 파일을 생성하거나 덮어쓴다. C# 스크립트(.cs)도 생성 가능. 경로는 프로젝트 루트 기준(예: Assets/Scripts/Player.cs).",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Assets/ 밑 상대 경로 (예: Assets/Scripts/Player.cs)" },
                        content = new { type = "string", description = "파일에 쓸 내용 전체" }
                    },
                    required = new[] { "path", "content" }
                }
            },
            new
            {
                name = "create_gameobject",
                description = "활성 씬에 지정한 이름의 빈 GameObject 를 생성한다.",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        name = new { type = "string", description = "생성할 GameObject 의 이름" }
                    },
                    required = new[] { "name" }
                }
            }
        };

        /// <summary>이 도구가 실행 전 사용자 승인(HITL)을 받아야 하는가. read-only=자동 / write=승인(안전 기본값).</summary>
        public static bool RequiresApproval(string name) => name switch
        {
            "read_active_scene"  => false,
            "read_console_logs"  => false,
            "get_compile_errors" => false,
            "read_text_file"     => false,
            "load_skill"         => false,
            _                    => true   // create_gameobject 등 write/미지의 도구 → 승인
        };

        /// <summary>tool_use 의 name/input 을 받아 도구를 실행하고 결과 텍스트를 돌려준다.</summary>
        public static string Run(string name, JObject input)
        {
            switch (name)
            {
                case "read_active_scene":
                    return ReadActiveScene();
                case "read_console_logs":
                    return AgentOpsLog.RecentLogs(
                        input["count"] != null ? (int)input["count"] : 30,
                        input["level"] != null ? (string)input["level"] : "all");
                case "get_compile_errors":
                    return AgentOpsLog.CompileErrors();
                case "read_text_file":
                    return ReadTextFile((string)input["path"]);
                case "load_skill":
                    return SkillRegistry.Load((string)input["name"]);
                case "write_file":
                    return WriteFile((string)input["path"], (string)input["content"]);
                case "create_gameobject":
                    return CreateGameObject((string)input["name"]);
                default:
                    return $"[오류] 알 수 없는 도구: {name}";
            }
        }

        // --- read_active_scene: 활성 씬의 계층을 들여쓰기 트리로 ---
        private static string ReadActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            var sb = new StringBuilder();
            sb.AppendLine($"활성 씬: \"{scene.name}\" (루트 {scene.rootCount}개)");
            foreach (var root in scene.GetRootGameObjects())
                AppendHierarchy(root.transform, sb, 0);
            return sb.ToString();
        }

        private static void AppendHierarchy(Transform t, StringBuilder sb, int depth)
        {
            sb.Append(' ', depth * 2);
            sb.AppendLine($"- {t.name}");
            foreach (Transform child in t)
                AppendHierarchy(child, sb, depth + 1);
        }

        // --- read_text_file: Assets/ 밑 파일 읽기 (경로 샌드박스) ---
        private static string ReadTextFile(string path)
        {
            if (!IsSafePath(path, out var full))
                return $"[거부] Assets/ 밖이거나 안전하지 않은 경로입니다: {path}";
            if (!File.Exists(full))
                return $"[오류] 파일을 찾을 수 없습니다: {path}";

            var text = File.ReadAllText(full);
            const int max = 8000; // 토큰 폭주 방지
            return text.Length > max ? text.Substring(0, max) + "\n...(이하 생략)" : text;
        }

        // --- write_file (write): Assets/ 밑에 텍스트/스크립트 파일 생성·덮어쓰기 (샌드박스) ---
        private static string WriteFile(string path, string content)
        {
            if (!IsSafePath(path, out var full))
                return $"[거부] Assets/ 밖이거나 안전하지 않은 경로입니다: {path}";

            var dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(full, content ?? string.Empty);
            AssetDatabase.Refresh(); // Unity 가 임포트(.cs 면 컴파일)

            return $"파일을 저장했습니다: {path} ({(content?.Length ?? 0)}자)";
        }

        /// <summary>
        /// 경로가 Assets/ 밑이고 `../` 탈출이 없는지 (ch08 IsSafePath).
        /// GetFullPath 로 `../` 를 정규화한 뒤, 구분자까지 붙인 prefix 로 검사(형제 폴더 우회 방지).
        /// </summary>
        private static bool IsSafePath(string relativePath, out string fullPath)
        {
            fullPath = null;
            if (string.IsNullOrWhiteSpace(relativePath))
                return false;

            var projectRoot = Directory.GetParent(Application.dataPath).FullName; // Assets 의 부모
            var combined = Path.GetFullPath(Path.Combine(projectRoot, relativePath));

            var assetsPrefix = Application.dataPath.Replace('/', Path.DirectorySeparatorChar)
                                   .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!combined.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase))
                return false;

            fullPath = combined;
            return true;
        }

        // --- create_gameobject (write): 활성 씬에 빈 GameObject 생성 ---
        private static string CreateGameObject(string goName)
        {
            var go = new GameObject(goName);
            Undo.RegisterCreatedObjectUndo(go, "Create " + goName);
            EditorSceneManager.MarkSceneDirty(go.scene);
            return $"GameObject '{goName}' 를 생성했습니다.";
        }
    }
}
