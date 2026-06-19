using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace AgentOps.Editor
{
    /// <summary>
    /// 콘솔 로그(런타임 Debug.Log*/예외)와 컴파일 에러를 수집해 도구(read_console_logs /
    /// get_compile_errors)가 읽을 수 있게 보관한다.
    /// [InitializeOnLoad] 로 에디터 시작·도메인 리로드 때마다 자동 구독.
    ///
    /// - 런타임 로그: 정적 링버퍼(도메인 리로드 시 초기화됨 — 플레이 세션 단위로는 충분).
    /// - 컴파일 에러: SessionState 에 저장(도메인 리로드를 넘어 유지됨).
    /// </summary>
    [InitializeOnLoad]
    public static class AgentOpsLog
    {
        private struct LogItem
        {
            public LogType type;
            public string message;
            public string stack;
        }

        private const int Max = 300;
        private static readonly LinkedList<LogItem> _logs = new LinkedList<LogItem>();
        private const string CompileKey = "agentops.compileErrors";

        static AgentOpsLog()
        {
            Application.logMessageReceived += OnLog;
            CompilationPipeline.compilationStarted += _ => SessionState.SetString(CompileKey, "");
            CompilationPipeline.assemblyCompilationFinished += OnCompiled;
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            _logs.AddLast(new LogItem { type = type, message = message, stack = stack });
            while (_logs.Count > Max)
                _logs.RemoveFirst();
        }

        private static void OnCompiled(string assemblyPath, CompilerMessage[] messages)
        {
            var sb = new StringBuilder(SessionState.GetString(CompileKey, ""));
            foreach (var m in messages)
                if (m.type == CompilerMessageType.Error)
                    sb.AppendLine($"{m.file}({m.line}): {m.message}");
            SessionState.SetString(CompileKey, sb.ToString());
        }

        /// <summary>최근 로그를 필터링해 텍스트로. level: all / error / warning</summary>
        public static string RecentLogs(int count, string level)
        {
            var lines = new List<string>();
            var node = _logs.Last; // 최신부터 거꾸로
            while (node != null && lines.Count < count)
            {
                var it = node.Value;
                bool isError = it.type == LogType.Error || it.type == LogType.Exception || it.type == LogType.Assert;
                bool match = level == "all"
                             || (level == "error" && isError)
                             || (level == "warning" && it.type == LogType.Warning);
                if (match)
                {
                    // 에러/예외는 스택까지(원인 추적), 나머지는 메시지만.
                    lines.Add(isError && !string.IsNullOrEmpty(it.stack)
                        ? $"[{it.type}] {it.message}\n{it.stack.TrimEnd()}"
                        : $"[{it.type}] {it.message}");
                }
                node = node.Previous;
            }

            if (lines.Count == 0)
                return "(해당하는 로그가 없습니다)";

            lines.Reverse(); // 오래된 → 최신 순서로
            return string.Join("\n", lines);
        }

        /// <summary>현재 컴파일 에러(파일·줄·메시지). 없으면 안내 문자열.</summary>
        public static string CompileErrors()
        {
            var s = SessionState.GetString(CompileKey, "");
            return string.IsNullOrWhiteSpace(s) ? "(컴파일 에러 없음)" : s.TrimEnd();
        }
    }
}
