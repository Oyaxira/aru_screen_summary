using System.Text.Json;
using System.Diagnostics;

public class TranslationRecord
{
    public DateTime Timestamp { get; set; }
    public string Content { get; set; }
    public int Width { get; set; }
    public Usage TokenUsage { get; set; }

    public int PromptTokens => TokenUsage?.prompt_tokens ?? 0;
    public int CompletionTokens => TokenUsage?.completion_tokens ?? 0;
    public int TotalTokens => TokenUsage?.total_tokens ?? 0;

    public TranslationRecord(string content, int width, Usage tokenUsage)
    {
        Content = content;
        Timestamp = DateTime.Now;
        Width = width;
        TokenUsage = tokenUsage;
    }
}

public class TranslationHistory
{
    private const int MAX_HISTORY = 400;  // 最多保存100条记录
    private static string HistoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AruScreenSummary",
        "history.json"
    );

    public static List<TranslationRecord> Records { get; private set; } = new List<TranslationRecord>();

    public static void Load()
    {
        try
        {
            if (File.Exists(HistoryPath))
            {
                string json = File.ReadAllText(HistoryPath);
                var records = JsonSerializer.Deserialize<List<TranslationRecord>>(json);
                if (records != null)
                {
                    Records = records;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"加载历史记录失败: {ex.Message}");
        }
    }

    public static void Save()
    {
        try
        {
            string directoryPath = Path.GetDirectoryName(HistoryPath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(Records, options);
            File.WriteAllText(HistoryPath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"保存历史记录失败: {ex.Message}");
        }
    }

    public static void AddRecord(string content, int width, Usage usage)
    {
        Records.Insert(0, new TranslationRecord(content, width, usage));
        if (Records.Count > MAX_HISTORY)
        {
            Records.RemoveRange(MAX_HISTORY, Records.Count - MAX_HISTORY);
        }
        Save();
    }
}
