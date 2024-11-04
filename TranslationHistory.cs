using System;
using System.Collections.Generic;
using System.Text.Json;
using System.IO;
using System.Diagnostics;

namespace ScreenshotGPT
{
    public class TranslationRecord
    {
        public DateTime Timestamp { get; set; }
        public string Content { get; set; }
        public int Width { get; set; }
    }

    public static class TranslationHistory
    {
        private static List<TranslationRecord> _records = new List<TranslationRecord>();
        private static readonly string HistoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AruScreenSummary",
            "history.json"
        );

        public static IReadOnlyList<TranslationRecord> Records => _records.AsReadOnly();

        public static void AddRecord(string content, int width)
        {
            var record = new TranslationRecord
            {
                Timestamp = DateTime.Now,
                Content = content,
                Width = width
            };

            _records.Insert(0, record); // 在开头插入，保持最新记录在前

            // 限制历史记录数量
            if (_records.Count > 100)
            {
                _records.RemoveRange(100, _records.Count - 100);
            }

            Save();
        }

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
                        _records = records;
                        Trace.WriteLine($"加载了 {records.Count} 条历史记录");
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"加载历史记录失败: {ex.Message}");
                _records = new List<TranslationRecord>();
            }
        }

        private static void Save()
        {
            try
            {
                string directoryPath = Path.GetDirectoryName(HistoryPath);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_records, options);
                File.WriteAllText(HistoryPath, json);
                Trace.WriteLine("历史记录保存成功");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"保存历史记录失败: {ex.Message}");
            }
        }
    }
}
