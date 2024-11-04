using System;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Text.Json;
using System.Net.Http;
using System.Text;

namespace AruScreenSummary
{
    public partial class MainForm : Form
    {
        private GlobalKeyboardHook _globalHook;
        private bool _isCapturing = false;
        private Point _startPoint;
        private Form _overlay;
        private AppSettings _settings;
        private NotifyIcon notifyIcon;
        private Keys[] _currentHotkey = new Keys[] { Keys.ControlKey, Keys.Menu, Keys.P }; // 修改默认快捷键，使用通用的Control和Alt键
        private bool _disposed = false;

        private Rectangle _selectionRect;
        private bool _isDrawing;
        private Button _applyButton;
        private Button _cancelButton;

        private List<Form> _overlayForms;

        private Form _activeOverlay;
        private Point _globalStartPoint;
        private Rectangle _globalSelectionRect;

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(PROCESS_DPI_AWARENESS awareness);

        private enum PROCESS_DPI_AWARENESS
        {
            Process_DPI_Unaware = 0,
            Process_System_DPI_Aware = 1,
            Process_Per_Monitor_DPI_Aware = 2
        }

        private class DoubleBufferedForm : Form
        {
            public DoubleBufferedForm()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint |
                        ControlStyles.UserPaint |
                        ControlStyles.DoubleBuffer, true);
            }
        }

        public MainForm()
        {
            try
            {
                // 使用更现代的DPI感知方式
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    SetProcessDpiAwareness(PROCESS_DPI_AWARENESS.Process_Per_Monitor_DPI_Aware);
                }
                else
                {
                    SetProcessDPIAware();
                }

                InitializeComponent();

                // 加载设置和历史记录
                _settings = AppSettings.Load() ?? new AppSettings();  // 确保不为 null
                TranslationHistory.Load();

                // 使用保存的快捷键或默认快捷键
                _currentHotkey = _settings.HotKeys?.Length > 0 ? _settings.HotKeys : new Keys[] { Keys.ControlKey, Keys.Menu, Keys.P };

                // 先初始化托盘图标，确保在隐藏窗口前托盘图标已经显示
                InitializeNotifyIcon();
                InitializeHotKey();


                // 确保托盘图标可见后再隐藏主窗体
                this.ShowInTaskbar = false;
                this.WindowState = FormWindowState.Minimized;
                this.Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeHotKey()
        {
            try
            {
                if (_globalHook != null)
                {
                    _globalHook.Dispose();
                    _globalHook = null;
                }

                _globalHook = new GlobalKeyboardHook();
                _globalHook.KeyCombination = _currentHotkey;
                _globalHook.KeyPressed += () =>
                {
                    if (!_isCapturing)
                    {
                        if (InvokeRequired)
                        {
                            Invoke(new Action(StartCapture));
                        }
                        else
                        {
                            StartCapture();
                        }
                    }
                    else
                    {
                    }
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化快捷键时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void StartCapture()
        {
            if (_isCapturing)
            {
                return;
            }
            _isCapturing = true;

            try
            {
                var screens = Screen.AllScreens;
                Rectangle totalBounds = GetTotalScreenBounds();

                // 先创建遮罩窗口
                _overlayForms = new List<Form>();
                foreach (Screen screen in screens)
                {
                    Form darkOverlay = new Form
                    {
                        StartPosition = FormStartPosition.Manual,
                        FormBorderStyle = FormBorderStyle.None,
                        BackColor = Color.Black,
                        Opacity = 0.3,
                        ShowInTaskbar = false,
                        TopMost = true,
                        Location = screen.Bounds.Location,
                        Size = screen.Bounds.Size
                    };
                    _overlayForms.Add(darkOverlay);
                    darkOverlay.Show();
                }

                // 等待遮罩显示
                Application.DoEvents();

                // 创建组合位图
                using (var compositeBitmap = new Bitmap(totalBounds.Width, totalBounds.Height))
                {
                    using (Graphics compositeGraphics = Graphics.FromImage(compositeBitmap))
                    {
                        compositeGraphics.Clear(Color.Transparent);
                        compositeGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        compositeGraphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        compositeGraphics.SmoothingMode = SmoothingMode.HighQuality;

                        // 为每个屏幕捕获内容
                        foreach (Screen screen in screens)
                        {
                            using (var screenBitmap = new Bitmap(screen.Bounds.Width, screen.Bounds.Height))
                            {
                                using (Graphics g = Graphics.FromImage(screenBitmap))
                                {
                                    g.CopyFromScreen(
                                        screen.Bounds.X,
                                        screen.Bounds.Y,
                                        0,
                                        0,
                                        screen.Bounds.Size,
                                        CopyPixelOperation.SourceCopy
                                    );
                                }

                                compositeGraphics.DrawImage(
                                    screenBitmap,
                                    screen.Bounds.X - totalBounds.X,
                                    screen.Bounds.Y - totalBounds.Y,
                                    screen.Bounds.Width,
                                    screen.Bounds.Height
                                );
                            }
                        }
                    }

                    // 清理遮罩窗口
                    foreach (var overlay in _overlayForms)
                    {
                        if (!overlay.IsDisposed)
                        {
                            overlay.Close();
                            overlay.Dispose();
                        }
                    }
                    _overlayForms.Clear();

                    // 建选择窗口
                    CreateScreenOverlays(new Bitmap(compositeBitmap));
                }
            }
            catch (Exception ex)
            {
                _isCapturing = false;

                // 确保清理所有窗口
                if (_overlayForms != null)
                {
                    foreach (var overlay in _overlayForms)
                    {
                        if (!overlay.IsDisposed)
                        {
                            overlay.Close();
                            overlay.Dispose();
                        }
                    }
                    _overlayForms.Clear();
                }

                MessageBox.Show($"截图失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private Rectangle GetTotalScreenBounds()
        {
            // 获取所有屏幕的总边界
            int left = Screen.AllScreens.Min(s => s.Bounds.Left);
            int top = Screen.AllScreens.Min(s => s.Bounds.Top);
            int right = Screen.AllScreens.Max(s => s.Bounds.Right);
            int bottom = Screen.AllScreens.Max(s => s.Bounds.Bottom);

            Rectangle totalBounds = new Rectangle(left, top, right - left, bottom - top);
            return totalBounds;
        }

        private void CreateScreenOverlays(Bitmap compositeBitmap)
        {
            var screens = Screen.AllScreens;
            _overlayForms = new List<Form>();
            Rectangle totalBounds = GetTotalScreenBounds();

            foreach (Screen screen in screens)
            {
                // 获取真实的屏幕分辨率
                var screenBounds = screen.Bounds;

                var screenOverlay = new DoubleBufferedForm
                {
                    StartPosition = FormStartPosition.Manual,
                    FormBorderStyle = FormBorderStyle.None,
                    ShowInTaskbar = false,
                    TopMost = true,
                    Location = screenBounds.Location,
                    Size = screenBounds.Size,
                    AutoScaleMode = AutoScaleMode.None
                };

                // 确保窗口不会被DPI缩放影响
                screenOverlay.HandleCreated += (s, e) =>
                {
                    var form = (Form)s;
                    var currentScreen = Screen.FromHandle(form.Handle);
                    if (currentScreen.Bounds != form.Bounds)
                    {
                        form.Size = currentScreen.Bounds.Size;
                        form.Location = currentScreen.Bounds.Location;
                    }
                };

                // 为了确保正确的大小，添加日志

                // 为每个屏幕创建精确的背景图
                screenOverlay.BackgroundImage = new Bitmap(screen.Bounds.Width, screen.Bounds.Height);
                using (var g = Graphics.FromImage(screenOverlay.BackgroundImage))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.SmoothingMode = SmoothingMode.HighQuality;

                    // 计算源矩形
                    Rectangle sourceRect = new Rectangle(
                        screen.Bounds.X - totalBounds.X,
                        screen.Bounds.Y - totalBounds.Y,
                        screen.Bounds.Width,
                        screen.Bounds.Height
                    );

                    g.DrawImage(
                        compositeBitmap,
                        new Rectangle(0, 0, screen.Bounds.Width, screen.Bounds.Height),
                        sourceRect,
                        GraphicsUnit.Pixel
                    );
                }

                // 添加事件处理
                screenOverlay.MouseDown += Overlay_MouseDown;
                screenOverlay.MouseMove += Overlay_MouseMove;
                screenOverlay.MouseUp += Overlay_MouseUp;
                screenOverlay.Paint += Overlay_Paint;
                screenOverlay.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Escape)
                    {
                        CancelCapture();
                    }
                };

                screenOverlay.Cursor = Cursors.Cross;
                _overlayForms.Add(screenOverlay);
                screenOverlay.Show();
            }
        }

        private void Overlay_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _activeOverlay = sender as Form;
                if (_activeOverlay == null) return;

                _isDrawing = true;
                // 记录起始点的全局坐标
                _startPoint = e.Location;
                _globalStartPoint = new Point(
                    _activeOverlay.Left + e.X,
                    _activeOverlay.Top + e.Y
                );
            }
        }

        private void Overlay_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDrawing || _activeOverlay == null) return;

            // 计算当前鼠标的全局坐标
            Point currentGlobalPoint = new Point(
                ((Form)sender).Left + e.X,
                ((Form)sender).Top + e.Y
            );

            // 计算全局选区
            _globalSelectionRect = new Rectangle(
                Math.Min(_globalStartPoint.X, currentGlobalPoint.X),
                Math.Min(_globalStartPoint.Y, currentGlobalPoint.Y),
                Math.Abs(currentGlobalPoint.X - _globalStartPoint.X),
                Math.Abs(currentGlobalPoint.Y - _globalStartPoint.Y)
            );

            // 强制所有遮罩窗口重绘
            foreach (var overlay in _overlayForms)
            {
                overlay.Invalidate();
            }
        }

        private void Overlay_Paint(object sender, PaintEventArgs e)
        {
            var overlay = sender as Form;
            if (overlay == null) return;

            e.Graphics.CompositingMode = CompositingMode.SourceOver;
            e.Graphics.CompositingQuality = CompositingQuality.HighSpeed;
            e.Graphics.InterpolationMode = InterpolationMode.Low;
            e.Graphics.SmoothingMode = SmoothingMode.HighSpeed;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;

            if (_globalSelectionRect.Width > 0 && _globalSelectionRect.Height > 0)
            {
                // 计算当前窗口上的选区
                Rectangle localRect = new Rectangle(
                    _globalSelectionRect.X - overlay.Left,
                    _globalSelectionRect.Y - overlay.Top,
                    _globalSelectionRect.Width,
                    _globalSelectionRect.Height
                );

                // 创建透明遮罩
                using (SolidBrush darkBrush = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                {
                    // 绘制四个区域来创建遮罩效果
                    Rectangle[] regions = {
                        new Rectangle(0, 0, overlay.Width, localRect.Top),  // 上方
                        new Rectangle(0, localRect.Bottom, overlay.Width, overlay.Height - localRect.Bottom),  // 下方
                        new Rectangle(0, localRect.Top, localRect.Left, localRect.Height),  // 左方
                        new Rectangle(localRect.Right, localRect.Top, overlay.Width - localRect.Right, localRect.Height)  // 右方
                    };

                    foreach (var region in regions)
                    {
                        e.Graphics.FillRectangle(darkBrush, region);
                    }
                }

                // 绘制选区边框
                using (Pen pen = new Pen(Color.DodgerBlue, 2))
                {
                    e.Graphics.DrawRectangle(pen, localRect);

                    // 在四个角绘制小方块
                    int squareSize = 6;
                    using (SolidBrush squareBrush = new SolidBrush(Color.DodgerBlue))
                    {
                        e.Graphics.FillRectangle(squareBrush, localRect.X - squareSize/2, localRect.Y - squareSize/2, squareSize, squareSize);
                        e.Graphics.FillRectangle(squareBrush, localRect.Right - squareSize/2, localRect.Y - squareSize/2, squareSize, squareSize);
                        e.Graphics.FillRectangle(squareBrush, localRect.X - squareSize/2, localRect.Bottom - squareSize/2, squareSize, squareSize);
                        e.Graphics.FillRectangle(squareBrush, localRect.Right - squareSize/2, localRect.Bottom - squareSize/2, squareSize, squareSize);
                    }
                }
            }
        }

        private void Overlay_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isDrawing && _globalSelectionRect.Width > 0 && _globalSelectionRect.Height > 0)
            {
                _isDrawing = false;

                // 如果按钮已经存在，只更新位置
                if (_applyButton != null && _cancelButton != null)
                {
                    UpdateButtonsPosition();
                }
                else
                {
                    // 只在按钮不存在时创建
                    CreateSelectionButtons();
                }
            }
        }

        private async Task SendToGPT(Bitmap image)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    image.Save(ms, ImageFormat.Png);
                    string base64Image = Convert.ToBase64String(ms.ToArray());

                    using (HttpClient client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiKey}");

                        // 明确定义消息类型
                        var messages = new object[]
                        {
                            new { role = "system", content = "Parse and summarize the image content,Reply in the user's language." },
                            new { role = "user", content = new object[]
                                {
                                    new { type = "text", text = "解释" },
                                    new { type = "image_url", image_url = new { url = $"data:image/png;base64,{base64Image}" } }
                                }
                            }
                        };

                        var requestData = new
                        {
                            model = _settings.Model,
                            messages = messages,
                            max_tokens = _settings.MaxTokens
                        };

                        var jsonContent = JsonSerializer.Serialize(requestData);
                        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                        var response = await client.PostAsync(_settings.Endpoint, content);

                        if (response.IsSuccessStatusCode)
                        {
                            var result = await response.Content.ReadFromJsonAsync<GPTResponse>();
                            var responseContent = result.choices[0].message.content;

                            // 添加记录时包含 token 使用信息
                            TranslationHistory.AddRecord(
                                responseContent,
                                _globalSelectionRect.Width,
                                result.usage  // 传入 usage 信息
                            );

                            this.Invoke((MethodInvoker)delegate
                            {
                                ToastForm.ShowMessage(responseContent, _globalSelectionRect.Width);
                            });
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            this.Invoke((MethodInvoker)delegate
                            {
                                ToastForm.ShowMessage($"API 请求失败: {response.StatusCode}\n{errorContent}");
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    ToastForm.ShowMessage(ex.Message, 300);  // 错误消息使用默认宽度
                });
            }
        }

        private void InitializeNotifyIcon()
        {
            try
            {
                notifyIcon = new NotifyIcon
                {
                    Visible = true,
                    Text = "Aru Screen Summary"
                };

                // 尝试加载自定义图标
                try
                {
                    // 优先使用 ICO 文件
                    if (File.Exists("bot.ico"))
                    {
                        notifyIcon.Icon = new Icon("bot.ico");
                    }
                    else
                    {
                        using (var bitmap = new Bitmap("bot.png"))
                        {
                            IntPtr hIcon = bitmap.GetHicon();
                            notifyIcon.Icon = Icon.FromHandle(hIcon);
                        }
                    }
                }
                catch (Exception ex)
                {
                    notifyIcon.Icon = SystemIcons.Application;  // 使用默认图标
                }

                // 右键菜单
                var contextMenu = new ContextMenuStrip();

                // 添加设置选项
                contextMenu.Items.Add("设置", null, (s, e) => ShowSettings());
                contextMenu.Items.Add("退出", null, (s, e) =>
                {
                    notifyIcon.Visible = false;
                    notifyIcon.Dispose();
                    Application.Exit();
                });

                notifyIcon.ContextMenuStrip = contextMenu;

                // 添加双击事件
                notifyIcon.DoubleClick += (s, e) => ShowSettings();

                // 显示气泡提示
                string hotkeyText = string.Join("+", _currentHotkey.Select(k =>
                {
                    switch (k)
                    {
                        case Keys.ControlKey:
                            return "Ctrl";
                        case Keys.Menu:
                            return "Alt";
                        case Keys.ShiftKey:
                            return "Shift";
                        default:
                            return k.ToString();
                    }
                }));

                notifyIcon.ShowBalloonTip(
                    3000,  // 显示时间（毫秒）
                    "阿露助手",  // 标题
                    $"阿露助手已经就绪，解析快捷键：{hotkeyText}",  // 内容
                    ToolTipIcon.Info  // 图标类型
                );
            }
            catch (Exception ex)
            {
                throw; // 重新抛出异常，因为没有托盘图标程序就无法正常使用
            }
        }

        private void ShowSettings()
        {
            var settingsForm = new SettingsForm(_settings, _currentHotkey, _globalHook);

            // 先隐藏窗口
            settingsForm.Visible = false;

            // 设置位置和激活
            settingsForm.StartPosition = FormStartPosition.CenterScreen;
            settingsForm.Show();
            settingsForm.BringToFront();
            settingsForm.Activate();
            settingsForm.Focus();

            // 隐藏后再显示为模态对话框
            settingsForm.Hide();
            settingsForm.ShowDialog();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;  // 取消关闭操作
                this.Hide();  // 隐藏窗口
                return;
            }

            // 确保清理资源
            if (!_disposed)
            {
                if (notifyIcon != null)
                {
                    notifyIcon.Visible = false;
                    notifyIcon.Dispose();
                }

                if (_globalHook != null)
                {
                    _globalHook.Dispose();
                }

                _disposed = true;
            }

            base.OnFormClosing(e);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        public void UpdateHotKey(Keys[] newHotKey)
        {
            try
            {
                _currentHotkey = newHotKey;

                if (_globalHook != null)
                {
                    // 直接更新组合键，内部会重新注册钩子
                    _globalHook.KeyCombination = newHotKey;
                }
                else
                {
                    InitializeHotKey();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新快捷键时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void ApplyButton_Click(object sender, EventArgs e)
        {
            if (_globalSelectionRect.Width > 0 && _globalSelectionRect.Height > 0)
            {
                foreach (var overlay in _overlayForms)
                {
                    overlay.Hide();
                }
                await Task.Delay(100); // 等待overlay完全隐藏

                try
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        ToastForm.ShowLoading(_globalSelectionRect.Width);
                    });

                    using (Bitmap bitmap = new Bitmap(_globalSelectionRect.Width, _globalSelectionRect.Height))
                    {
                        using (Graphics g = Graphics.FromImage(bitmap))
                        {
                            g.CopyFromScreen(
                                _globalSelectionRect.X,
                                _globalSelectionRect.Y,
                                0,
                                0,
                                new Size(_globalSelectionRect.Width, _globalSelectionRect.Height)
                            );
                        }
                        await SendToGPT(bitmap);
                    }
                }
                catch (Exception ex)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        ToastForm.ShowMessage($"截图错误: {ex.Message}");
                    });
                }
            }

            CleanupCapture();
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            CancelCapture();
        }

        private void CancelCapture()
        {
            // 清除所有选区相关的状态
            _selectionRect = Rectangle.Empty;
            _globalSelectionRect = Rectangle.Empty;
            _isCapturing = false;
            _isDrawing = false;
            _activeOverlay = null;
            _globalStartPoint = Point.Empty;

            // 正确清理按钮
            if (_applyButton != null)
            {
                if (_applyButton.Parent != null)
                {
                    _applyButton.Parent.Controls.Remove(_applyButton);
                }
                _applyButton.Dispose();
                _applyButton = null;
            }
            if (_cancelButton != null)
            {
                if (_cancelButton.Parent != null)
                {
                    _cancelButton.Parent.Controls.Remove(_cancelButton);
                }
                _cancelButton.Dispose();
                _cancelButton = null;
            }

            if (_overlayForms != null)
            {
                foreach (var overlay in _overlayForms)
                {
                    if (!overlay.IsDisposed)
                    {
                        if (overlay.BackgroundImage != null)
                        {
                            overlay.BackgroundImage.Dispose();
                        }
                        overlay.Close();
                        overlay.Dispose();
                    }
                }
                _overlayForms.Clear();
            }
        }

        private void CleanupCapture()
        {
            // 清除所有选区相关的状态
            _selectionRect = Rectangle.Empty;
            _globalSelectionRect = Rectangle.Empty;
            _isCapturing = false;
            _isDrawing = false;
            _activeOverlay = null;
            _globalStartPoint = Point.Empty;

            if (_applyButton != null) _applyButton.Visible = false;
            if (_cancelButton != null) _cancelButton.Visible = false;

            if (_overlayForms != null)
            {
                foreach (var overlay in _overlayForms)
                {
                    if (!overlay.IsDisposed)
                    {
                        if (overlay.BackgroundImage != null)
                        {
                            overlay.BackgroundImage.Dispose();
                        }
                        overlay.Close();
                        overlay.Dispose();
                    }
                }
                _overlayForms.Clear();
            }
        }

        private void CreateSelectionButtons()
        {
            // 如果按钮已存在，先清理掉
            if (_applyButton != null)
            {
                _applyButton.Dispose();
                _applyButton = null;
            }
            if (_cancelButton != null)
            {
                _cancelButton.Dispose();
                _cancelButton = null;
            }

            // 创建按钮
            _applyButton = new Button
            {
                Text = "解析",
                Size = new Size(60, 25),
                Visible = true,
                BackColor = Color.White,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Standard,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            _applyButton.Click += ApplyButton_Click;

            _cancelButton = new Button
            {
                Text = "取消",
                Size = new Size(60, 25),
                Visible = true,
                BackColor = Color.White,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Standard,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            _cancelButton.Click += CancelButton_Click;

            UpdateButtonsPosition();
        }

        private void UpdateButtonsPosition()
        {
            // 找到包含选区右下角的窗口
            var targetOverlay = _overlayForms.FirstOrDefault(f =>
                f.Bounds.Contains(new Point(
                    _globalSelectionRect.Right,
                    _globalSelectionRect.Bottom
                ))
            );

            if (targetOverlay != null)
            {
                // 如果按钮已经在其他窗口上，先移除
                if (_applyButton.Parent != null)
                {
                    _applyButton.Parent.Controls.Remove(_applyButton);
                }
                if (_cancelButton.Parent != null)
                {
                    _cancelButton.Parent.Controls.Remove(_cancelButton);
                }

                // 计算按钮在目标窗口上的位置
                _cancelButton.Location = new Point(
                    _globalSelectionRect.Right - targetOverlay.Left - _applyButton.Width - _cancelButton.Width - 5,
                    _globalSelectionRect.Bottom - targetOverlay.Top + 5
                );
                _applyButton.Location = new Point(
                    _globalSelectionRect.Right - targetOverlay.Left - _applyButton.Width,
                    _globalSelectionRect.Bottom - targetOverlay.Top + 5
                );

                targetOverlay.Controls.Add(_cancelButton);
                targetOverlay.Controls.Add(_applyButton);

                _cancelButton.BringToFront();
                _applyButton.BringToFront();
            }
        }

    }

}
