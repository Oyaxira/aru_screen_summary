using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Win32;

namespace AruScreenSummary
{
    public class SettingsForm : BaseSettingsForm
    {
        private List<Keys> newKeys = new List<Keys>();
        private bool isRecording = false;
        private AppSettings _settings;
        private GlobalKeyboardHook _globalHook;
        private Keys[] _currentHotkey;
        private const string StartupKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string AppName = "AruScreenSummary";

        public SettingsForm(AppSettings settings, Keys[] currentHotkey, GlobalKeyboardHook globalHook)
        {
            _settings = settings;
            _currentHotkey = currentHotkey;
            _globalHook = globalHook;

            // 暂时禁用全局快捷键
            _globalHook?.UninstallCurrentHook();

            this.Text = "设置";
            this.Size = new Size(600, 500);
            this.ShowIcon = true;

            // 设置窗口图标
            try
            {
                using (var bitmap = new Bitmap("bot.png"))
                {
                    IntPtr hIcon = bitmap.GetHicon();
                    this.Icon = Icon.FromHandle(hIcon);
                    this.ShowInTaskbar = true;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"加载设置窗口图标失败: {ex.Message}");
            }

            var tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(12, 4)
            };

            // 创建设置标签页
            var settingsTab = new TabPage("设置");
            InitializeSettingsTab(settingsTab);
            tabControl.TabPages.Add(settingsTab);

            // 创建历史记录标签页
            var historyTab = new TabPage("历史记录");
            InitializeHistoryTab(historyTab);
            tabControl.TabPages.Add(historyTab);

            this.Controls.Add(tabControl);

            // 在窗口关闭时重新启用快捷键
            this.FormClosing += (s, e) =>
            {
                if (_globalHook != null)
                {
                    _globalHook.KeyCombination = _settings.HotKeys;
                    _globalHook.InstallHook();
                }
            };
        }

        private void InitializeSettingsTab(TabPage tab)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(20)
            };

            // 创建一个容器面板来居中内容
            var contentPanel = new TableLayoutPanel
            {
                ColumnCount = 3,
                RowCount = 7,
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(10),
                ColumnStyles = {
                    new ColumnStyle(SizeType.Absolute, 100), // 标签列
                    new ColumnStyle(SizeType.Absolute, 340), // 输入框列
                    new ColumnStyle(SizeType.Absolute, 80)   // 按钮列
                }
            };

            // 快捷键设置
            contentPanel.Controls.Add(new Label { Text = "快捷键设置:", Anchor = AnchorStyles.Left }, 0, 0);
            var hotkeyBox = new TextBox
            {
                ReadOnly = true,
                Width = 330,
                Text = string.Join(" + ", _currentHotkey.Select(k => k.ToString()))
            };
            contentPanel.Controls.Add(hotkeyBox, 1, 0);
            var clearHotkeyButton = new Button { Text = "清除", Width = 70 };
            contentPanel.Controls.Add(clearHotkeyButton, 2, 0);

            // API Key
            contentPanel.Controls.Add(new Label { Text = "API Key:", Anchor = AnchorStyles.Left }, 0, 1);
            var apiKeyBox = new TextBox
            {
                Width = 330,
                UseSystemPasswordChar = true,
                Text = _settings.ApiKey
            };
            contentPanel.Controls.Add(apiKeyBox, 1, 1);
            var showApiKeyButton = new Button
            {
                Text = "👁",
                Width = 30,
                FlatStyle = FlatStyle.Flat
            };
            contentPanel.Controls.Add(showApiKeyButton, 2, 1);

            // Endpoint
            contentPanel.Controls.Add(new Label { Text = "API Endpoint:", Anchor = AnchorStyles.Left }, 0, 2);
            var endpointBox = new TextBox
            {
                Width = 330,
                Text = _settings.Endpoint
            };
            contentPanel.Controls.Add(endpointBox, 1, 2);

            // Model
            contentPanel.Controls.Add(new Label { Text = "模型:", Anchor = AnchorStyles.Left }, 0, 3);
            var modelBox = new TextBox
            {
                Width = 330,
                Text = _settings.Model
            };
            contentPanel.Controls.Add(modelBox, 1, 3);

            // Max Tokens
            contentPanel.Controls.Add(new Label { Text = "最大Token数:", Anchor = AnchorStyles.Left }, 0, 4);
            var tokensBox = new NumericUpDown
            {
                Width = 330,
                Minimum = 100,
                Maximum = 4096,
                Value = _settings.MaxTokens
            };
            contentPanel.Controls.Add(tokensBox, 1, 4);

            // 保存按钮
            var saveButton = new Button
            {
                Text = "保存设置",
                Width = 100,
                Height = 30,
                Margin = new Padding(0, 20, 0, 0)
            };
            var saveButtonCell = new TableLayoutPanel { ColumnCount = 3, Dock = DockStyle.Fill };
            saveButtonCell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            saveButtonCell.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            saveButtonCell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            saveButtonCell.Controls.Add(saveButton, 1, 0);
            contentPanel.Controls.Add(saveButtonCell, 1, 5);

            // 开机启动按钮
            var startupButton = new Button
            {
                Width = 100,
                Height = 30,
                Margin = new Padding(0, 10, 0, 0)
            };

            // 根据当前状态设置按钮文本
            UpdateStartupButtonText(startupButton);

            // 添加到布局
            contentPanel.Controls.Add(new Label { Text = "开机启动:", Anchor = AnchorStyles.Left }, 0, 6);
            contentPanel.Controls.Add(startupButton, 1, 6);

            // 绑定点击事件
            startupButton.Click += (s, e) =>
            {
                bool currentState = IsStartupEnabled();
                ToggleStartup(!currentState);
                UpdateStartupButtonText(startupButton);
            };

            // 绑定事件
            hotkeyBox.KeyDown += (s, e) =>
            {
                if (!isRecording)
                {
                    isRecording = true;
                    newKeys.Clear();
                    hotkeyBox.Text = "";
                }

                if (!newKeys.Contains(e.KeyCode))
                {
                    newKeys.Add(e.KeyCode);
                    hotkeyBox.Text = string.Join(" + ", newKeys.Select(k => k.ToString()));
                }

                e.SuppressKeyPress = true;
            };

            hotkeyBox.KeyUp += (s, e) =>
            {
                if (isRecording && newKeys.Count >= 2)
                {
                    isRecording = false;
                }
            };

            clearHotkeyButton.Click += (s, e) =>
            {
                newKeys.Clear();
                hotkeyBox.Text = "";
                isRecording = false;
            };

            showApiKeyButton.MouseDown += (s, e) => apiKeyBox.UseSystemPasswordChar = false;
            showApiKeyButton.MouseUp += (s, e) => apiKeyBox.UseSystemPasswordChar = true;

            saveButton.Click += (s, e) =>
            {
                try
                {
                    // 保存快捷键设置
                    if (newKeys.Count >= 2)
                    {
                        _settings.HotKeys = newKeys.ToArray();
                        var mainForm = Application.OpenForms.OfType<MainForm>().FirstOrDefault();
                        mainForm?.UpdateHotKey(newKeys.ToArray());
                    }

                    // 保存其他设置
                    _settings.ApiKey = apiKeyBox.Text.Trim();
                    _settings.Endpoint = endpointBox.Text.Trim();
                    _settings.Model = modelBox.Text.Trim();
                    _settings.MaxTokens = (int)tokensBox.Value;
                    _settings.Save();

                    MessageBox.Show("设置已保存", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存设置时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            panel.Controls.Add(contentPanel);
            tab.Controls.Add(panel);
        }

        private void InitializeHistoryTab(TabPage tab)
        {
            const int PAGE_SIZE = 20;
            int currentPage = 0;

            // 创建一个垂直布局的面板
            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,  // 增加一行用于分页控件
                ColumnCount = 1,
                Padding = new Padding(5),
            };
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // ListView占用所有剩余空间
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F)); // 分页控件
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F)); // 状态栏固定高度

            var listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };

            // 添加列（移除宽度列）
            listView.Columns.Add("时间", 150);
            listView.Columns.Add("内容", 460);  // 增加内容列宽度
            listView.Columns.Add("Prompt", 70);
            listView.Columns.Add("Completion", 70);
            listView.Columns.Add("Total", 70);

            // 创建分页面板
            var pagePanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Height = 35,
                BackColor = Color.FromArgb(240, 240, 240)
            };

            var prevButton = new Button
            {
                Text = "上一页",
                Enabled = false,
                Height = 30,
                Width = 80
            };

            var pageLabel = new Label
            {
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                Height = 30
            };

            var nextButton = new Button
            {
                Text = "下一页",
                Height = 30,
                Width = 80
            };

            var pageSizeLabel = new Label
            {
                Text = $"每页 {PAGE_SIZE} 条",
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false,
                Height = 30
            };

            // 添加分页控件
            pagePanel.Controls.Add(prevButton, 0, 0);
            pagePanel.Controls.Add(pageLabel, 1, 0);
            pagePanel.Controls.Add(nextButton, 2, 0);
            pagePanel.Controls.Add(pageSizeLabel, 3, 0);

            // 设置分页面板列宽
            pagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            pagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            // 更新列表视图的函数
            void UpdateListView()
            {
                listView.Items.Clear();
                var pageRecords = TranslationHistory.Records
                    .Skip(currentPage * PAGE_SIZE)
                    .Take(PAGE_SIZE);

                foreach (var record in pageRecords)
                {
                    var item = new ListViewItem(record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
                    item.SubItems.Add(record.Content);
                    item.SubItems.Add(record.PromptTokens.ToString());
                    item.SubItems.Add(record.CompletionTokens.ToString());
                    item.SubItems.Add(record.TotalTokens.ToString());
                    listView.Items.Add(item);
                }

                // 更新页码显示
                int totalPages = (int)Math.Ceiling(TranslationHistory.Records.Count / (double)PAGE_SIZE);
                pageLabel.Text = $"第 {currentPage + 1} 页 / 共 {totalPages} 页";

                // 更新按钮状态
                prevButton.Enabled = currentPage > 0;
                nextButton.Enabled = (currentPage + 1) < totalPages;
            }

            // 绑定分页事件
            prevButton.Click += (s, e) =>
            {
                if (currentPage > 0)
                {
                    currentPage--;
                    UpdateListView();
                }
            };

            nextButton.Click += (s, e) =>
            {
                int totalPages = (int)Math.Ceiling(TranslationHistory.Records.Count / (double)PAGE_SIZE);
                if (currentPage + 1 < totalPages)
                {
                    currentPage++;
                    UpdateListView();
                }
            };

            // 初始化显示第一页
            UpdateListView();

            // 计算总token使用量
            int totalPromptTokens = TranslationHistory.Records.Sum(r => r.PromptTokens);
            int totalCompletionTokens = TranslationHistory.Records.Sum(r => r.CompletionTokens);
            int totalTokens = TranslationHistory.Records.Sum(r => r.TotalTokens);

            // 创建状态栏面板
            var statusPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.FromArgb(240, 240, 240),
                Margin = new Padding(0),
                Padding = new Padding(5)
            };

            // 添加三个标签显示统计信息
            var promptLabel = new Label
            {
                Text = $"总输入Token: {totalPromptTokens:N0}",
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                AutoSize = false
            };

            var completionLabel = new Label
            {
                Text = $"总输出Token: {totalCompletionTokens:N0}",
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                AutoSize = false
            };

            var totalLabel = new Label
            {
                Text = $"总Token用量: {totalTokens:N0}",
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Fill,
                AutoSize = false
            };

            statusPanel.Controls.Add(promptLabel, 0, 0);
            statusPanel.Controls.Add(completionLabel, 1, 0);
            statusPanel.Controls.Add(totalLabel, 2, 0);

            // 设置列宽度比例
            statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));

            // 恢复双击事件处理（注意：现在使用存储的宽度值）
            listView.DoubleClick += (s, e) =>
            {
                if (listView.SelectedItems.Count > 0)
                {
                    var selectedIndex = currentPage * PAGE_SIZE + listView.SelectedItems[0].Index;
                    if (selectedIndex < TranslationHistory.Records.Count)
                    {
                        var record = TranslationHistory.Records[selectedIndex];
                        ToastForm.ShowMessage(record.Content, record.Width);
                    }
                }
            };

            // 将控件添��到主面板
            mainPanel.Controls.Add(listView, 0, 0);
            mainPanel.Controls.Add(pagePanel, 0, 1);
            mainPanel.Controls.Add(statusPanel, 0, 2);

            // 将主面板添加到标签页
            tab.Controls.Add(mainPanel);
        }

        private bool IsStartupEnabled()
        {
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(StartupKey))
            {
                if (key != null)
                {
                    string? value = key.GetValue(AppName) as string;
                    return value != null && value.Equals(Application.ExecutablePath);
                }
                return false;
            }
        }

        private void ToggleStartup(bool enable)
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(StartupKey, true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            key.SetValue(AppName, Application.ExecutablePath);
                        }
                        else
                        {
                            key.DeleteValue(AppName, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"修改开机启动设置失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateStartupButtonText(Button button)
        {
            bool isEnabled = IsStartupEnabled();
            button.Text = isEnabled ? "移除开机启动" : "添加开机启动";
        }
    }
}
