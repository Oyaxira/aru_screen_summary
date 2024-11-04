using System.Text.Json;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace ScreenshotGPT
{
    public class AppSettings
    {
        private string _apiKey = "";
        public string ApiKey
        {
            get => Decrypt(_apiKey);
            set => _apiKey = Encrypt(value);
        }

        public string Endpoint { get; set; } = "https://api.openai.com/v1/chat/completions";
        public string Model { get; set; } = "gpt-4-vision-preview";
        public int MaxTokens { get; set; } = 1000;
        public Keys[] HotKeys { get; set; } = new Keys[] { Keys.LControlKey, Keys.LMenu, Keys.P }; // 添加快捷键设置

        private static string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AruScreenSummary",
            "settings.json"
        );

        public static AppSettings Load()
        {
            try
            {
                Debug.WriteLine($"尝试从路径加载设置: {SettingsPath}");

                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    Debug.WriteLine($"读取到的设置内容: {json}");

                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        Debug.WriteLine("设置加载成功");
                        return settings;
                    }
                }
                else
                {
                    Debug.WriteLine("设置文件不存在，将创建默认设置");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载设置失败: {ex.Message}");
                Debug.WriteLine($"异常堆栈: {ex.StackTrace}");
            }

            var defaultSettings = new AppSettings();
            defaultSettings.Save(); // 保存默认设置
            return defaultSettings;
        }

        public void Save()
        {
            try
            {
                string directoryPath = Path.GetDirectoryName(SettingsPath);
                Debug.WriteLine($"准备保存设置到目录: {directoryPath}");

                if (!Directory.Exists(directoryPath))
                {
                    Debug.WriteLine("创建设置目录");
                    Directory.CreateDirectory(directoryPath);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    IncludeFields = true // 确保所有字段都被序列化
                };

                string json = JsonSerializer.Serialize(this, options);
                Debug.WriteLine($"准备保存的设置内容: {json}");

                File.WriteAllText(SettingsPath, json);
                Debug.WriteLine("设置保存成功");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存设置失败: {ex.Message}");
                Debug.WriteLine($"异常堆栈: {ex.StackTrace}");
                MessageBox.Show($"保存设置失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string Encrypt(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            try
            {
                // 简单的加密方法，实际使用时建议使用更安全的加密方式
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                return Convert.ToBase64String(bytes);
            }
            catch
            {
                return text;
            }
        }

        private static string Decrypt(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            try
            {
                byte[] bytes = Convert.FromBase64String(text);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return text;
            }
        }
    }
}
