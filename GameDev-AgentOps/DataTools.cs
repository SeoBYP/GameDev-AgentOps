using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace AutomationAgent;

/// <summary>데이터 처리 도구 모음</summary>
public class DataTools
{
    private readonly string _workDir;

    public DataTools(string workDir)
    {
        _workDir = workDir;
    }

    /// <summary>CSV 파일을 분석하고 기본 통계를 반환한다.</summary>
    /// <param name="fileName">CSV 파일 이름 (작업 디렉토리 기준)</param>
    public string AnalyzeCsv(string fileName)
    {
        var path = Path.Combine(_workDir, fileName);
        if (!File.Exists(path)) return $"❌ 파일 없음: {fileName}";

        try
        {
            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader,
                new CsvConfiguration(CultureInfo.InvariantCulture));

            var records = csv.GetRecords<dynamic>().ToList();
            if (records.Count == 0) return "❌ 빈 CSV 파일";

            var first = (IDictionary<string, object>)records[0];
            var headers = first.Keys.ToList();

            var result = new System.Text.StringBuilder();
            result.AppendLine($"📊 CSV 분석: {fileName}");
            result.AppendLine($"  행 수: {records.Count:N0}");
            result.AppendLine($"  열 수: {headers.Count}");
            result.AppendLine($"  열 목록: {string.Join(", ", headers)}");
            result.AppendLine();

            // 숫자 열의 기본 통계
            foreach (var header in headers)
            {
                var values = records
                    .Select(r => ((IDictionary<string, object>)r)[header]?.ToString())
                    .Where(v => double.TryParse(v, out _))
                    .Select(v => double.Parse(v!))
                    .ToList();

                if (values.Count > 0)
                {
                    result.AppendLine($"  [{header}] (숫자 {values.Count}개)");
                    result.AppendLine($"    합계: {values.Sum():N2}");
                    result.AppendLine($"    평균: {values.Average():N2}");
                    result.AppendLine($"    최소: {values.Min():N2}");
                    result.AppendLine($"    최대: {values.Max():N2}");
                }
            }

            // 샘플 데이터 (첫 3행)
            result.AppendLine("\n  샘플 데이터 (첫 3행):");
            result.AppendLine("  " + string.Join(" | ", headers));
            foreach (var record in records.Take(3))
            {
                var row = (IDictionary<string, object>)record;
                result.AppendLine("  " + string.Join(" | ", headers.Select(h => row[h]?.ToString() ?? "")));
            }

            return result.ToString();
        }
        catch (Exception ex) { return $"❌ CSV 분석 오류: {ex.Message}"; }
    }

    /// <summary>텍스트 데이터의 기본 통계를 계산한다 (줄 수, 단어 수, 문자 수).</summary>
    /// <param name="fileName">분석할 텍스트 파일 이름</param>
    public string AnalyzeText(string fileName)
    {
        var path = Path.Combine(_workDir, fileName);
        if (!File.Exists(path)) return $"❌ 파일 없음: {fileName}";

        try
        {
            var content = File.ReadAllText(path);
            var lines = content.Split('\n').Length;
            var words = content.Split(new[] { ' ', '\n', '\r', '\t' },
                StringSplitOptions.RemoveEmptyEntries).Length;

            return $"""
📊 텍스트 분석: {fileName}
  문자 수: {content.Length:N0}
  줄 수:   {lines:N0}
  단어 수: {words:N0}
  파일 크기: {new FileInfo(path).Length:N0} bytes
""";
        }
        catch (Exception ex) { return $"❌ 분석 오류: {ex.Message}"; }
    }

    /// <summary>간단한 수학 계산을 수행한다 (합, 평균, 최대, 최소).</summary>
    /// <param name="numbers">쉼표로 구분된 숫자 목록 (예: "1, 2, 3, 4, 5")</param>
    public string Calculate(string numbers)
    {
        try
        {
            var vals = numbers.Split(',')
                .Select(s => double.Parse(s.Trim()))
                .ToList();

            return $"""
📊 계산 결과:
  데이터: {string.Join(", ", vals)}
  합계:   {vals.Sum():N2}
  평균:   {vals.Average():N2}
  최대:   {vals.Max():N2}
  최소:   {vals.Min():N2}
  표준편차: {Math.Sqrt(vals.Average(v => Math.Pow(v - vals.Average(), 2))):N2}
""";
        }
        catch (Exception ex) { return $"❌ 계산 오류: {ex.Message}"; }
    }
}