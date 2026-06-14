using UnityEditor;
using UnityEngine;

namespace AgentOps.Editor
{
    /// <summary>
    /// GameDev AgentOps 설정.
    /// 비밀이 아닌 값(모델/토큰)만 .asset 에 저장(커밋 OK)하고,
    /// API 키는 EditorPrefs(이 PC에만 저장, 레포 밖 → 커밋 안 됨)에 둔다.
    /// 열기/생성: 메뉴 Window > AgentOps > Settings
    /// </summary>
    [CreateAssetMenu(fileName = "AgentOpsSettings", menuName = "AgentOps/Settings")]
    public class AgentOpsSettings : ScriptableObject
    {
        [Tooltip("Claude 모델 ID")]
        public string model = "claude-opus-4-8";

        [Tooltip("응답 최대 토큰 수")]
        public int maxTokens = 1024;

        // --- API 키: .asset 에 직렬화하지 않는다. EditorPrefs 에만 보관 ---
        private const string ApiKeyPref = "agentops.apiKey";

        public static string ApiKey
        {
            get => EditorPrefs.GetString(ApiKeyPref, string.Empty);
            set => EditorPrefs.SetString(ApiKeyPref, value ?? string.Empty);
        }

        /// <summary>설정 에셋을 찾고, 없으면 Assets/AgentOps 아래에 만들어 반환.</summary>
        public static AgentOpsSettings GetOrCreate()
        {
            var guids = AssetDatabase.FindAssets($"t:{nameof(AgentOpsSettings)}");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<AgentOpsSettings>(path);
            }

            var settings = CreateInstance<AgentOpsSettings>();
            const string folder = "Assets/AgentOps";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "AgentOps");
            AssetDatabase.CreateAsset(settings, $"{folder}/AgentOpsSettings.asset");
            AssetDatabase.SaveAssets();
            return settings;
        }

        // 설정 에셋을 만들고(없으면) 선택 + 강조한다.
        [MenuItem("Window/AgentOps/Settings")]
        public static void OpenSettings()
        {
            var settings = GetOrCreate();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
    }
}
