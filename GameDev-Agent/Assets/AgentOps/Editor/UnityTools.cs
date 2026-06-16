using System.Text;
using Unity.Plastic.Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AgentOps.Editor
{
    /// <summary>
    /// Claude 가 호출할 수 있는 Unity 도구 모음 (read: read_active_scene / write: create_gameobject).
    /// - Definitions(): Claude 에게 보낼 도구 "스키마"(요청 body 의 tools 에 넣음)
    /// - Run(name, input): tool_use 가 오면 실제로 실행하고 결과 문자열을 돌려줌
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
                    properties = new { },   // 인자 없음 → 빈 객체
                    required = new string[] { }
                },
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

        /// <summary>
        /// 이 도구가 실행 전 사용자 승인(HITL)을 받아야 하는가.
        /// </summary>
        public static bool RequiresApproval(string name) => name switch
        {
            "read_active_scene" => false,   // read-only → 자동 허용 (이제 안 물어봄)
            _ => true                        // create_gameobject 등 write → 승인 필수
        };

        /// <summary>tool_use 의 name/input 을 받아 도구를 실행하고 결과 텍스트를 돌려준다.</summary>
        public static string Run(string name, JObject input)
        {
            switch (name)
            {
                case "read_active_scene":
                    return ReadActiveScene();
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
        
        private static string CreateGameObject(string goName)
        {
            var go = new GameObject(goName);
            Undo.RegisterCreatedObjectUndo(go, "Create " + goName);
            EditorSceneManager.MarkSceneDirty(go.scene);    
            return $"GameObject '{goName}' 를 생성했습니다.";
        }

        private static void AppendHierarchy(Transform t, StringBuilder sb, int depth)
        {
            sb.Append(' ', depth * 2);
            sb.AppendLine($"- {t.name}");
            foreach (Transform child in t)
                AppendHierarchy(child, sb, depth + 1);
        }
    }
}
