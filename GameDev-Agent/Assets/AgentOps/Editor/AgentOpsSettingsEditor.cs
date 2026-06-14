using UnityEditor;
using UnityEngine;

namespace AgentOps.Editor
{
    /// <summary>
    /// AgentOpsSettings 의 커스텀 인스펙터.
    /// model/maxTokens 는 일반 필드로, API 키는 EditorPrefs 에 저장되는 PasswordField 로 그린다.
    /// (키가 .asset 에 직렬화되지 않으므로 git 에 커밋되지 않는다.)
    /// </summary>
    [CustomEditor(typeof(AgentOpsSettings))]
    public class AgentOpsSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // model, maxTokens 등 일반 직렬화 필드
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("API Key", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "키는 이 에셋이 아니라 EditorPrefs(이 PC에만 저장)에 보관됩니다. git 에 커밋되지 않습니다.",
                MessageType.Info);

            var current = AgentOpsSettings.ApiKey;
            var edited = EditorGUILayout.PasswordField("Anthropic API Key", current);
            if (edited != current)
                AgentOpsSettings.ApiKey = edited;

            if (string.IsNullOrWhiteSpace(edited))
                EditorGUILayout.HelpBox("API 키가 비어 있습니다.", MessageType.Warning);
        }
    }
}
