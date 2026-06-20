using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                name = "find_gameobjects",
                description = "활성 씬에서 이름(부분 일치) 또는 컴포넌트 타입으로 GameObject 를 검색한다. 비활성 포함. 둘 다 선택(없으면 전체). 경로와 컴포넌트 목록을 반환. read_active_scene 보다 구체적으로 찾을 때 사용.",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        name = new { type = "string", description = "이름 부분 일치(선택)" },
                        component = new { type = "string", description = "컴포넌트 타입 이름 부분 일치, 예: Rigidbody, Collider, Camera (선택)" }
                    },
                    required = new string[] { }
                }
            },
            new
            {
                name = "inspect_gameobject",
                description = "특정 GameObject 의 상세 정보(경로·활성 상태·태그·레이어·Transform·컴포넌트 목록)를 반환한다. read_active_scene 이 못 주는 컴포넌트/속성을 확인할 때 사용.",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        target = new { type = "string", description = "GameObject 이름 또는 전체 경로(예: Player/Weapon)" }
                    },
                    required = new[] { "target" }
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
                name = "delegate",
                description = "전문 sub-agent에게 작업을 위임하고 그 결과를 받는다. agent: 'Triage'(읽기·분석) 또는 'Builder'(생성·수정). task: sub-agent가 수행할 구체적 작업. Coordinator 모드에서만 사용.",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        agent = new { type = "string", description = "위임 대상: Triage 또는 Builder" },
                        task = new { type = "string", description = "수행할 구체적 작업 설명" }
                    },
                    required = new[] { "agent", "task" }
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
            },
            new
            {
                name = "add_component",
                description = "GameObject 에 컴포넌트를 추가한다. target: 이름 또는 경로. component: 컴포넌트 타입 이름(예: Rigidbody, BoxCollider, AudioSource). 빌트인·사용자 스크립트 모두 가능.",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        target = new { type = "string", description = "GameObject 이름 또는 경로" },
                        component = new { type = "string", description = "추가할 컴포넌트 타입 이름 (예: Rigidbody)" }
                    },
                    required = new[] { "target", "component" }
                }
            },
            new
            {
                name = "remove_component",
                description = "GameObject 에서 컴포넌트를 제거한다. target: 이름 또는 경로. component: 제거할 컴포넌트 타입 이름. Transform 은 제거 불가.",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        target = new { type = "string", description = "GameObject 이름 또는 경로" },
                        component = new { type = "string", description = "제거할 컴포넌트 타입 이름" }
                    },
                    required = new[] { "target", "component" }
                }
            },
            new
            {
                name = "create_primitive",
                description = "화면에 보이는 기본 도형 GameObject 를 생성한다(MeshFilter·MeshRenderer·Collider 포함). shape: Cube|Sphere|Capsule|Cylinder|Plane|Quad.",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        shape = new { type = "string", description = "Cube / Sphere / Capsule / Cylinder / Plane / Quad" },
                        name = new { type = "string", description = "생성할 이름(선택, 기본은 도형 이름)" }
                    },
                    required = new[] { "shape" }
                }
            },
            new
            {
                name = "set_primitive_mesh",
                description = "기존 GameObject 를 기본 도형 모양으로 '보이게' 만든다. MeshFilter 의 mesh 와 MeshRenderer 의 material 을 해당 프리미티브 것으로 설정(없으면 컴포넌트 추가). 빈 오브젝트를 화면에 표시할 때 사용.",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        target = new { type = "string", description = "GameObject 이름 또는 경로" },
                        shape = new { type = "string", description = "Cube / Sphere / Capsule / Cylinder / Plane / Quad" }
                    },
                    required = new[] { "target", "shape" }
                }
            }
        };

        /// <summary>허용된 도구 이름 집합으로 필터링한 정의(프로필별 권한 분리). allowed=null 이면 전체.</summary>
        public static object[] Definitions(HashSet<string> allowed)
        {
            if (allowed == null)
                return Definitions();
            return Definitions()
                .Where(d => allowed.Contains((string)d.GetType().GetProperty("name").GetValue(d)))
                .ToArray();
        }

        /// <summary>이 도구가 실행 전 사용자 승인(HITL)을 받아야 하는가. read-only=자동 / write=승인(안전 기본값).</summary>
        public static bool RequiresApproval(string name) => name switch
        {
            "read_active_scene"  => false,
            "find_gameobjects"   => false,
            "inspect_gameobject" => false,
            "read_console_logs"  => false,
            "get_compile_errors" => false,
            "read_text_file"     => false,
            "load_skill"         => false,
            "delegate"           => false, // 위임 자체는 승인 X (sub 의 write 는 sub-loop 에서 승인)
            _                    => true   // create_gameobject 등 write/미지의 도구 → 승인
        };

        /// <summary>tool_use 의 name/input 을 받아 도구를 실행하고 결과 텍스트를 돌려준다.</summary>
        public static string Run(string name, JObject input)
        {
            switch (name)
            {
                case "read_active_scene":
                    return ReadActiveScene();
                case "find_gameobjects":
                    return FindGameObjects(
                        input["name"] != null ? (string)input["name"] : null,
                        input["component"] != null ? (string)input["component"] : null);
                case "inspect_gameobject":
                    return InspectGameObject((string)input["target"]);
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
                case "add_component":
                    return AddComponent((string)input["target"], (string)input["component"]);
                case "remove_component":
                    return RemoveComponent((string)input["target"], (string)input["component"]);
                case "create_primitive":
                    return CreatePrimitive((string)input["shape"],
                        input["name"] != null ? (string)input["name"] : null);
                case "set_primitive_mesh":
                    return SetPrimitiveMesh((string)input["target"], (string)input["shape"]);
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

        // 활성 씬의 모든 GameObject(비활성 포함). 각 루트의 자식 Transform 을 전부 훑는다.
        private static IEnumerable<GameObject> AllInScene()
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    yield return t.gameObject;
        }

        // 루트부터의 전체 경로(A/B/C).
        private static string PathOf(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent)
                sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        // --- find_gameobjects: 이름/컴포넌트로 검색 ---
        private static string FindGameObjects(string nameQuery, string componentQuery)
        {
            const int max = 50;
            var matches = new List<string>();
            int total = 0;

            foreach (var go in AllInScene())
            {
                if (!string.IsNullOrEmpty(nameQuery) &&
                    go.name.IndexOf(nameQuery, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (!string.IsNullOrEmpty(componentQuery) &&
                    !go.GetComponents<Component>().Any(c => c != null &&
                        c.GetType().Name.IndexOf(componentQuery, StringComparison.OrdinalIgnoreCase) >= 0))
                    continue;

                total++;
                if (matches.Count < max)
                {
                    var comps = string.Join(", ", go.GetComponents<Component>()
                        .Where(c => c != null).Select(c => c.GetType().Name));
                    matches.Add($"- {PathOf(go.transform)}  [{comps}]");
                }
            }

            if (total == 0)
                return "조건에 맞는 GameObject 가 없습니다.";

            var sb = new StringBuilder();
            sb.AppendLine($"일치 {total}개" + (total > max ? $" (상위 {max}개만 표시)" : "") + ":");
            foreach (var m in matches)
                sb.AppendLine(m);
            return sb.ToString();
        }

        // --- inspect_gameobject: 한 오브젝트의 컴포넌트·Transform 상세 ---
        private static string InspectGameObject(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
                return "[오류] target(이름/경로)이 비었습니다.";

            var found = FindGameObject(target);
            if (found == null)
                return $"'{target}' 에 해당하는 GameObject 를 찾지 못했습니다.";

            var t = found.transform;
            var sb = new StringBuilder();
            sb.AppendLine($"GameObject: {found.name}");
            sb.AppendLine($"경로: {PathOf(t)}");
            sb.AppendLine($"활성: self={found.activeSelf}, inHierarchy={found.activeInHierarchy}");
            sb.AppendLine($"태그: {found.tag} / 레이어: {LayerMask.LayerToName(found.layer)}");
            sb.AppendLine($"Transform: pos={t.localPosition}, rot(euler)={t.localEulerAngles}, scale={t.localScale}");
            sb.AppendLine($"자식 {t.childCount}개");
            sb.AppendLine("컴포넌트:");
            foreach (var c in found.GetComponents<Component>())
            {
                if (c == null) { sb.AppendLine("  - (Missing Script)"); continue; }
                var off = (c is Behaviour b && !b.enabled) ? " (비활성)" : "";
                sb.AppendLine($"  - {c.GetType().Name}{off}");
            }
            return sb.ToString();
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

        // 이름 또는 경로(A/B/C)로 활성 씬에서 GameObject 를 찾는다(정확 일치 → 이름 부분 일치 폴백).
        private static GameObject FindGameObject(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
                return null;

            bool byPath = target.IndexOf('/') >= 0;
            foreach (var go in AllInScene())
                if (byPath
                    ? PathOf(go.transform).Equals(target, StringComparison.OrdinalIgnoreCase)
                    : go.name.Equals(target, StringComparison.OrdinalIgnoreCase))
                    return go;

            foreach (var go in AllInScene())
                if (go.name.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0)
                    return go;

            return null;
        }

        // 컴포넌트 타입 이름 → Type. UnityEngine 빌트인 우선, 없으면 로드된 어셈블리에서 Component 파생 검색.
        private static Type ResolveComponentType(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var builtin = typeof(Transform).Assembly.GetType("UnityEngine." + name, false, true);
            if (builtin != null && typeof(Component).IsAssignableFrom(builtin))
                return builtin;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; } // 일부 어셈블리는 로드 실패할 수 있음 — 건너뜀
                foreach (var ty in types)
                    if (typeof(Component).IsAssignableFrom(ty) &&
                        (string.Equals(ty.Name, name, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(ty.FullName, name, StringComparison.OrdinalIgnoreCase)))
                        return ty;
            }
            return null;
        }

        // --- add_component (write): GameObject 에 컴포넌트 추가 ---
        private static string AddComponent(string target, string component)
        {
            var go = FindGameObject(target);
            if (go == null)
                return $"'{target}' GameObject 를 찾지 못했습니다.";

            var type = ResolveComponentType(component);
            if (type == null)
                return $"컴포넌트 타입 '{component}' 을(를) 찾지 못했습니다. (정확한 클래스명이 필요합니다)";
            if (type == typeof(Transform))
                return "Transform 은 모든 GameObject 에 이미 있어 추가할 수 없습니다.";

            var comp = Undo.AddComponent(go, type);
            if (comp == null)
                return $"'{type.Name}' 추가에 실패했습니다 (중복 불가 컴포넌트일 수 있음).";

            EditorSceneManager.MarkSceneDirty(go.scene);
            return $"'{go.name}' 에 컴포넌트 '{type.Name}' 를 추가했습니다.";
        }

        // --- remove_component (write): GameObject 에서 컴포넌트 제거 ---
        private static string RemoveComponent(string target, string component)
        {
            var go = FindGameObject(target);
            if (go == null)
                return $"'{target}' GameObject 를 찾지 못했습니다.";

            var type = ResolveComponentType(component);
            if (type == null)
                return $"컴포넌트 타입 '{component}' 을(를) 찾지 못했습니다.";
            if (type == typeof(Transform))
                return "Transform 은 제거할 수 없습니다.";

            var comp = go.GetComponent(type);
            if (comp == null)
                return $"'{go.name}' 에 '{type.Name}' 컴포넌트가 없습니다.";

            Undo.DestroyObjectImmediate(comp);
            EditorSceneManager.MarkSceneDirty(go.scene);
            return $"'{go.name}' 에서 컴포넌트 '{type.Name}' 를 제거했습니다.";
        }

        // 도형 이름 → PrimitiveType.
        private static bool TryParsePrimitive(string shape, out PrimitiveType type)
        {
            type = PrimitiveType.Cube;
            if (string.IsNullOrWhiteSpace(shape)) return false;
            switch (shape.Trim().ToLowerInvariant())
            {
                case "cube":     type = PrimitiveType.Cube;     return true;
                case "sphere":   type = PrimitiveType.Sphere;   return true;
                case "capsule":  type = PrimitiveType.Capsule;  return true;
                case "cylinder": type = PrimitiveType.Cylinder; return true;
                case "plane":    type = PrimitiveType.Plane;    return true;
                case "quad":     type = PrimitiveType.Quad;     return true;
                default: return false;
            }
        }

        // --- create_primitive (write): 보이는 기본 도형 생성 ---
        private static string CreatePrimitive(string shape, string goName)
        {
            if (!TryParsePrimitive(shape, out var type))
                return $"알 수 없는 도형: '{shape}'. (Cube/Sphere/Capsule/Cylinder/Plane/Quad)";

            var go = GameObject.CreatePrimitive(type);
            if (!string.IsNullOrWhiteSpace(goName))
                go.name = goName;
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            EditorSceneManager.MarkSceneDirty(go.scene);
            return $"{type} 프리미티브 '{go.name}' 를 생성했습니다 (MeshFilter·MeshRenderer·Collider 포함).";
        }

        // --- set_primitive_mesh (write): 기존 GameObject 를 도형 모양으로 보이게 ---
        private static string SetPrimitiveMesh(string target, string shape)
        {
            var go = FindGameObject(target);
            if (go == null)
                return $"'{target}' GameObject 를 찾지 못했습니다.";
            if (!TryParsePrimitive(shape, out var type))
                return $"알 수 없는 도형: '{shape}'. (Cube/Sphere/Capsule/Cylinder/Plane/Quad)";

            // 빌트인 메시·머티리얼은 임시 프리미티브에서 가져온다.
            var temp = GameObject.CreatePrimitive(type);
            var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            var mat = temp.GetComponent<MeshRenderer>().sharedMaterial;
            UnityEngine.Object.DestroyImmediate(temp);

            var mf = go.GetComponent<MeshFilter>() ?? Undo.AddComponent<MeshFilter>(go);
            var mr = go.GetComponent<MeshRenderer>() ?? Undo.AddComponent<MeshRenderer>(go);

            Undo.RecordObject(mf, "Set Mesh");
            mf.sharedMesh = mesh;
            Undo.RecordObject(mr, "Set Material");
            mr.sharedMaterial = mat;

            EditorSceneManager.MarkSceneDirty(go.scene);
            return $"'{go.name}' 를 {type} 모양으로 보이게 했습니다 (MeshFilter mesh + MeshRenderer material 설정).";
        }
    }
}
