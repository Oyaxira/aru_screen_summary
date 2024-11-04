using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AruScreenSummary
{
    public class SettingsForm : BaseSettingsForm
    {
        private List<Keys> newKeys = new List<Keys>();
        private bool isRecording = false;
        private AppSettings _settings;
        private GlobalKeyboardHook _globalHook;
        private Keys[] _currentHotkey;

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
                Maximum = 4000,
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
            // 创建一个垂直布局的面板
            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Padding = new Padding(5),
            };
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // ListView占用所有剩余空间
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F)); // 状态栏固定高度

            var listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };

            // 添加列
            listView.Columns.Add("时间", 150);
            listView.Columns.Add("内容", 400);
            listView.Columns.Add("宽度", 60);
            listView.Columns.Add("Prompt", 70);
            listView.Columns.Add("Completion", 70);
            listView.Columns.Add("Total", 70);

            // 添加记录
            foreach (var record in TranslationHistory.Records)
            {
                var item = new ListViewItem(record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
                item.SubItems.Add(record.Content);
                item.SubItems.Add(record.Width.ToString());
                item.SubItems.Add(record.PromptTokens.ToString());
                item.SubItems.Add(record.CompletionTokens.ToString());
                item.SubItems.Add(record.TotalTokens.ToString());
                listView.Items.Add(item);
            }

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

            // 恢复双击事件处理
            listView.DoubleClick += (s, e) =>
            {
                if (listView.SelectedItems.Count > 0)
                {
                    var content = listView.SelectedItems[0].SubItems[1].Text;
                    var width = int.Parse(listView.SelectedItems[0].SubItems[2].Text);
                    ToastForm.ShowMessage(content, width);
                }
            };

            // 将控件添加到主面板
            mainPanel.Controls.Add(listView, 0, 0);
            mainPanel.Controls.Add(statusPanel, 0, 1);

            // 将主面板添加到标签页
            tab.Controls.Add(mainPanel);
        }
    }
}
