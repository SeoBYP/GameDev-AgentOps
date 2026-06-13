namespace AutomationAgent;

/// <summary>파일 관리 도구 모음 (안전한 작업 디렉토리 내에서만 동작)</summary>
public class FileTools
{
    private readonly string _workDir;
    private readonly string _workDirPrefix;
    public FileTools(string? workDir = null)
    {
        var baseDir = workDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "AutomationWorkspace");
        
        _workDir = Path.GetFullPath(baseDir);
        _workDirPrefix = EnsureTrailingSeparator(_workDir);
        
        Directory.CreateDirectory(_workDir);
        Console.WriteLine($"📁 작업 디렉토리: {_workDir}");
    }
    
    private static string EnsureTrailingSeparator(string path)
    {
        var fullPath = Path.GetFullPath(path);

        if (fullPath.EndsWith(Path.DirectorySeparatorChar) ||
            fullPath.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return fullPath;
        }

        return fullPath + Path.DirectorySeparatorChar;
    }
    
    private bool IsSafePath(string path)
    {
        var fullPath = Path.GetFullPath(path);

        return fullPath.StartsWith(_workDirPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>지정 디렉토리의 파일 목록을 조회한다.</summary>
    /// <param name="pattern">검색 패턴 (예: *.txt, *.csv). 기본값은 모든 파일.</param>
    public string ListFiles(string pattern = "*.*")
    {
        try
        {
            var files = Directory.GetFiles(_workDir, pattern);
            if (files.Length == 0)
                return $"📁 {_workDir}\n파일 없음";

            var result = $"📁 {_workDir} ({files.Length}개):\n";
            foreach (var f in files)
            {
                var fi = new FileInfo(f);
                result += $"  • {fi.Name} ({FormatSize(fi.Length)}, {fi.LastWriteTime:MM-dd HH:mm})\n";
            }
            return result;
        }
        catch (Exception ex) { return $"❌ 오류: {ex.Message}"; }
    }

    /// <summary>파일의 내용을 읽는다. 최대 5000자까지 반환한다.</summary>
    /// <param name="fileName">읽을 파일 이름 (작업 디렉토리 기준)</param>
    public string ReadFile(string fileName)
    {
        var path = Path.Combine(_workDir, fileName);
        if(!IsSafePath(path))
            return $"❌ 안전하지 않은 경로: {fileName}";
        if (!File.Exists(path)) return $"❌ 파일 없음: {fileName}";
        try
        {
            var content = File.ReadAllText(path);
            if (content.Length > 5000)
                content = content[..5000] + "\n...(생략)...";
            return $"📄 {fileName}:\n\n{content}";
        }
        catch (Exception ex) { return $"❌ 읽기 오류: {ex.Message}"; }
    }

    /// <summary>파일에 내용을 쓴다. 기존 파일이 있으면 .backup 파일을 만든다.</summary>
    /// <param name="fileName">쓸 파일 이름</param>
    /// <param name="content">파일 내용</param>
    public string WriteFile(string fileName, string content)
    {
        var path = Path.Combine(_workDir, fileName);
        if(!IsSafePath(path))
            return $"❌ 안전하지 않은 경로: {fileName}";
        try
        {
            if (File.Exists(path))
                File.Copy(path, path + ".backup", overwrite: true);
            File.WriteAllText(path, content);
            return $"✅ 저장 완료: {fileName} ({content.Length}자)";
        }
        catch (Exception ex) { return $"❌ 쓰기 오류: {ex.Message}"; }
    }

    /// <summary>파일명 패턴으로 파일을 검색한다.</summary>
    /// <param name="keyword">검색할 키워드 (파일명 내 포함 여부)</param>
    public string SearchFiles(string keyword)
    {
        try
        {
            var files = Directory
                .GetFiles(_workDir, $"*{keyword}*", SearchOption.AllDirectories)
                .Where(IsSafePath)
                .ToArray();

            if (files.Length == 0)
                return $"🔍 '{keyword}' 검색 결과: 없음";

            var result = $"🔍 '{keyword}' 검색 결과 ({files.Length}개):\n";
            foreach (var f in files.Take(20))
                result += $"  • {Path.GetRelativePath(_workDir, f)}\n";
            if (files.Length > 20)
                result += $"  ... 외 {files.Length - 20}개";
            return result;
        }
        catch (Exception ex) { return $"❌ 검색 오류: {ex.Message}"; }
    }
    
    /// <summary>작업 디렉토리 경로를 반환한다.</summary>
    public string GetWorkDirectory() => $"작업 디렉토리: {_workDir}";

    private string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return $"{size:F1}{units[unit]}";
    }
}