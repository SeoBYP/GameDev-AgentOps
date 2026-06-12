using System.Text;
using UglyToad.PdfPig;

namespace GameDev_AgentOps;

/// <summary>문서 청크 단위</summary>
public record DocumentChunk(
    string DocumentId,
    string FileName,
    string Text,
    int ChunkIndex,
    int PageNumber = 0
);

/// <summary>로드된 문서 정보</summary>
public record DocumentInfo(
    string Id,
    string FileName,
    string FilePath,
    int TotalChunks,
    int TotalChars
);

/// <summary>
/// 문서 로딩, 청킹, 검색을 담당하는 관리자 클래스
/// </summary>
public class DocumentManager
{
    private readonly List<DocumentInfo> _documents = new();
    private readonly List<DocumentChunk> _chunks = new();
    private readonly int _chunkSize;
    private readonly int _overlap;
    
    public IReadOnlyList<DocumentInfo> Documents => _documents;

    public DocumentManager(int chunkSize = 800, int overlap = 100)
    {
        _chunkSize = chunkSize;
        _overlap = overlap;
    }
    
    
    /// <summary>파일을 로드하고 청킹한다 (TXT, PDF 지원)</summary>
    public DocumentInfo LoadDocument(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"파일을 찾을 수 없다: {filePath}");

        var ext = Path.GetExtension(filePath).ToLower();
        var fileName = Path.GetFileName(filePath);
        var docId = Guid.NewGuid().ToString("N")[..8];

        Console.WriteLine($"\n📄 문서 로딩: {fileName}");

        string text;
        int pageCount = 1;

        if (ext == ".pdf")
        {
            (text, pageCount) = ExtractPdfText(filePath, docId);
        }
        else
        {
            text = File.ReadAllText(filePath, Encoding.UTF8);
        }

        var chunks = ChunkText(text, docId, fileName);
        _chunks.AddRange(chunks);

        var info = new DocumentInfo(docId, fileName, filePath, chunks.Count, text.Length);
        _documents.Add(info);

        Console.WriteLine($"✅ 로딩 완료: {fileName}");
        Console.WriteLine($"   청크 수: {chunks.Count}개, 전체 길이: {text.Length:N0}자");

        return info;
    }
    /// <summary>PDF에서 텍스트를 추출한다</summary>
    private (string text, int pageCount) ExtractPdfText(string filePath, string docId)
    {
        var sb = new StringBuilder();
        int pageCount = 0;

        using var pdf = PdfDocument.Open(filePath);
        foreach (var page in pdf.GetPages())
        {
            pageCount++;
            sb.AppendLine($"[페이지 {pageCount}]");
            sb.AppendLine(string.Join(" ", page.GetWords().Select(w => w.Text)));
            sb.AppendLine();

            if (pageCount % 10 == 0)
                Console.Write($"\r   {pageCount}페이지 처리 중...");
        }
        Console.WriteLine();

        return (sb.ToString(), pageCount);
    }

    /// <summary>텍스트를 겹치는 청크로 분할한다</summary>
    private List<DocumentChunk> ChunkText(string text, string docId, string fileName)
    {
        var chunks = new List<DocumentChunk>();
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" },
            StringSplitOptions.RemoveEmptyEntries);

        var current = new StringBuilder();
        int idx = 0;

        foreach (var para in paragraphs)
        {
            if (current.Length + para.Length > _chunkSize && current.Length > 0)
            {
                chunks.Add(new DocumentChunk(docId, fileName,
                    current.ToString().Trim(), idx));
                idx++;

                // 오버랩: 이전 청크 끝부분을 다음 청크 시작에 포함
                var overlap = current.ToString();
                current.Clear();
                if (overlap.Length > _overlap)
                    current.Append(overlap[^_overlap..]);
            }
            current.AppendLine(para);
        }

        if (current.Length > 0)
            chunks.Add(new DocumentChunk(docId, fileName,
                current.ToString().Trim(), idx));

        return chunks;
    }

    /// <summary>질문과 관련된 청크를 검색한다</summary>
    public List<DocumentChunk> SearchChunks(string query, int topK = 5)
    {
        if (_chunks.Count == 0) return new();

        var queryTerms = Tokenize(query);

        return _chunks
            .Select(chunk => new
            {
                Chunk = chunk,
                Score = CalculateScore(chunk.Text, queryTerms)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Chunk)
            .ToList();
    }

    private List<string> Tokenize(string text) =>
        text.ToLower()
            .Split(new[] { ' ', ',', '.', '!', '?', '\n', '\r', '은', '는', '이', '가', '을', '를' },
                StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 1)
            .ToList();

    private double CalculateScore(string text, List<string> queryTerms)
    {
        var textTerms = Tokenize(text);
        return queryTerms.Sum(term =>
            textTerms.Count(t => t.Contains(term)) * (1.0 / Math.Max(1, textTerms.Count)));
    }

    public void RemoveAll()
    {
        _documents.Clear();
        _chunks.Clear();
    }
}