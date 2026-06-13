namespace AutomationAgent;

/// <summary>시스템 정보 도구 모음</summary>
public class SystemTools
{
    /// <summary>현재 날짜와 시간을 반환한다.</summary>
    public string GetCurrentTime()
        => $"현재 시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss} (KST)";

    /// <summary>현재 디스크 사용량 정보를 반환한다.</summary>
    public string GetDiskUsage()
    {
        try
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .Select(d => $"  {d.Name}: 사용 {FormatSize(d.TotalSize - d.AvailableFreeSpace)} / {FormatSize(d.TotalSize)} ({(1.0 - (double)d.AvailableFreeSpace / d.TotalSize) * 100:F1}%)");

            return "💾 디스크 사용량:\n" + string.Join("\n", drives);
        }
        catch (Exception ex) { return $"❌ 오류: {ex.Message}"; }
    }

    /// <summary>현재 실행 중인 환경 정보를 반환한다.</summary>
    public string GetSystemInfo()
        => $"""
            💻 시스템 정보:
              OS: {Environment.OSVersion}
              .NET: {Environment.Version}
              CPU 코어: {Environment.ProcessorCount}개
              머신명: {Environment.MachineName}
              사용자: {Environment.UserName}
            """;

    private string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return $"{size:F1} {units[unit]}";
    }
}