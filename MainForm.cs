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

namespace ScreenshotGPT
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
                Trace.WriteLine("MainForm 构造函数开始");
                InitializeComponent();

                // 加载置和历史记录
                _settings = AppSettings.Load();
                TranslationHistory.Load();
                Trace.WriteLine($"加载的设置 - API Key: {_settings.ApiKey?.Length > 0}, Endpoint: {_settings.Endpoint}");

                // 使用保存的快捷键或默认快捷键
                _currentHotkey = _settings.HotKeys?.Length > 0 ? _settings.HotKeys : new Keys[] { Keys.ControlKey, Keys.Menu, Keys.P };
                Trace.WriteLine($"使用快捷键: {string.Join(" + ", _currentHotkey)}");

                // 先初始化托盘图标，确保在隐藏窗口前托盘图标已经显示
                InitializeNotifyIcon();
                InitializeHotKey();

                Trace.WriteLine("初始化完成");

                // 确保托盘图标可见后再隐藏主窗体
                this.ShowInTaskbar = false;
                this.WindowState = FormWindowState.Minimized;
                this.Hide();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MainForm 初始化错误: {ex}");
                MessageBox.Show($"初始化错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeHotKey()
        {
            try
            {
                if (_globalHook != null)
                {
                    Trace.WriteLine("正在释放旧的钩子");
                    _globalHook.Dispose();
                    _globalHook = null;
                }

                Trace.WriteLine("开始初始化全局钩子");
                _globalHook = new GlobalKeyboardHook();
                _globalHook.KeyCombination = _currentHotkey;
                _globalHook.KeyPressed += () =>
                {
                    Trace.WriteLine("快捷键事件被触发");
                    if (!_isCapturing)
                    {
                        Trace.WriteLine("准备开始截图");
                        if (InvokeRequired)
                        {
                            Trace.WriteLine("在UI线程上执行截图");
                            Invoke(new Action(StartCapture));
                        }
                        else
                        {
                            StartCapture();
                        }
                    }
                    else
                    {
                        Trace.WriteLine("已经在截图中，忽略本次触发");
                    }
                };
                Trace.WriteLine($"已设置快捷键: {string.Join(" + ", _currentHotkey)}");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"初始化快捷键时出错: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"初始化快捷键时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void StartCapture()
        {
            Trace.WriteLine("开始截图");
            if (_isCapturing)
            {
                Trace.WriteLine("已经在截图中，忽略本次触发");
                return;
            }
            _isCapturing = true;

            try
            {
                // 获取所有屏幕的总边界
                Rectangle virtualScreen = SystemInformation.VirtualScreen;

                // 创建一个全屏遮罩
                Form darkOverlay = new Form
                {
                    StartPosition = FormStartPosition.Manual,
                    FormBorderStyle = FormBorderStyle.None,
                    BackColor = Color.Black,
                    Opacity = 0.3,
                    ShowInTaskbar = false,
                    TopMost = true,
                    Location = new Point(virtualScreen.X, virtualScreen.Y),
                    Size = new Size(virtualScreen.Width, virtualScreen.Height)
                };
                darkOverlay.Show();

                // 等待一下确保遮罩显示
                Application.DoEvents();

                // 捕获整个虚拟屏幕
                using (var fullScreenshot = new Bitmap(virtualScreen.Width, virtualScreen.Height))
                {
                    using (Graphics g = Graphics.FromImage(fullScreenshot))
                    {
                        g.CopyFromScreen(virtualScreen.X, virtualScreen.Y, 0, 0, virtualScreen.Size);
                    }

                    // 关闭遮罩
                    darkOverlay.Close();
                    darkOverlay.Dispose();

                    // 创建一个新的 Bitmap 副本
                    var screenshotCopy = new Bitmap(fullScreenshot);
                    CreateOverlay(screenshotCopy);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"截图时出错: {ex.Message}\n{ex.StackTrace}");
                _isCapturing = false;
            }
        }

        private void CreateOverlay(Image screenshot)
        {
            Trace.WriteLine("开始创建遮罩窗口");
            try
            {
                // 获取所有屏幕的总边界
                Rectangle virtualScreen = SystemInformation.VirtualScreen;

                // 创建一个新的 Bitmap 来存储截图
                Bitmap backgroundImage = new Bitmap(screenshot);

                _overlay = new DoubleBufferedForm
                {
                    StartPosition = FormStartPosition.Manual,
                    FormBorderStyle = FormBorderStyle.None,
                    BackgroundImage = backgroundImage,
                    BackgroundImageLayout = ImageLayout.None,
                    ShowInTaskbar = false,
                    TopMost = true,
                    Location = new Point(virtualScreen.X, virtualScreen.Y),
                    Size = new Size(virtualScreen.Width, virtualScreen.Height)
                };

                // 确保在窗口关闭时释放资源
                _overlay.FormClosing += (s, e) =>
                {
                    if (_overlay.BackgroundImage != null)
                    {
                        _overlay.BackgroundImage.Dispose();
                        _overlay.BackgroundImage = null;
                    }
                };

                // 创建按钮但初始时不显示
                _cancelButton = new Button
                {
                    Text = "取消",
                    Size = new Size(60, 25),
                    Visible = false,
                    BackColor = Color.White,
                    ForeColor = Color.Black,
                    FlatStyle = FlatStyle.Standard,
                    Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
                    Cursor = Cursors.Hand
                };
                _cancelButton.Click += CancelButton_Click;

                _applyButton = new Button
                {
                    Text = "应用",
                    Size = new Size(60, 25),
                    Visible = false,
                    BackColor = Color.White,
                    ForeColor = Color.Black,
                    FlatStyle = FlatStyle.Standard,
                    Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
                    Cursor = Cursors.Hand
                };
                _applyButton.Click += ApplyButton_Click;

                _overlay.Controls.Add(_cancelButton);
                _overlay.Controls.Add(_applyButton);

                _overlay.MouseDown += Overlay_MouseDown;
                _overlay.MouseMove += Overlay_MouseMove;
                _overlay.MouseUp += Overlay_MouseUp;
                _overlay.Paint += Overlay_Paint;
                _overlay.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Escape)
                    {
                        CancelCapture();
                    }
                };

                // 设置鼠标样式
                _overlay.Cursor = Cursors.Cross;

                Trace.WriteLine("显示遮罩窗口");
                _overlay.Show();
                _overlay.Activate();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"创建遮罩窗口时出错: {ex.Message}\n{ex.StackTrace}");
                _isCapturing = false;
            }
        }

        private void UpdateButtonsPosition()
        {
            if (_selectionRect.Width > 0 && _selectionRect.Height > 0)
            {
                // 交换按钮位置，应用按钮在右边
                _cancelButton.Location = new Point(
                    _selectionRect.Right - _applyButton.Width - _cancelButton.Width - 5,
                    _selectionRect.Bottom + 5
                );
                _applyButton.Location = new Point(
                    _selectionRect.Right - _applyButton.Width,
                    _selectionRect.Bottom + 5
                );

                // 设置按钮样式
                foreach (Button btn in new[] { _applyButton, _cancelButton })
                {
                    btn.Visible = true;
                    btn.Parent = _overlay;
                    btn.BringToFront();
                }

                // 设置按钮的层级
                _overlay.Controls.SetChildIndex(_applyButton, 0);
                _overlay.Controls.SetChildIndex(_cancelButton, 0);
            }
        }

        private void Overlay_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _startPoint = e.Location;
                _isDrawing = true;
            }
        }

        private void Overlay_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDrawing)
            {
                _selectionRect = new Rectangle(
                    Math.Min(_startPoint.X, e.X),
                    Math.Min(_startPoint.Y, e.Y),
                    Math.Abs(e.X - _startPoint.X),
                    Math.Abs(e.Y - _startPoint.Y)
                );
                _overlay.Invalidate(true);  // 强制立即重绘
            }
        }

        private void Overlay_Paint(object sender, PaintEventArgs e)
        {
            if (_selectionRect.Width > 0 && _selectionRect.Height > 0)
            {
                e.Graphics.CompositingMode = CompositingMode.SourceOver;
                e.Graphics.CompositingQuality = CompositingQuality.HighSpeed;
                e.Graphics.InterpolationMode = InterpolationMode.Low;
                e.Graphics.SmoothingMode = SmoothingMode.HighSpeed;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;

                // 创建半透明遮罩
                using (SolidBrush darkBrush = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                {
                    // 绘制四个区域来创建遮罩效果
                    Rectangle[] regions = {
                        new Rectangle(0, 0, _overlay.Width, _selectionRect.Top),  // 上方
                        new Rectangle(0, _selectionRect.Bottom, _overlay.Width, _overlay.Height - _selectionRect.Bottom),  // 下方
                        new Rectangle(0, _selectionRect.Top, _selectionRect.Left, _selectionRect.Height),  // 左方
                        new Rectangle(_selectionRect.Right, _selectionRect.Top, _overlay.Width - _selectionRect.Right, _selectionRect.Height)  // 右方
                    };

                    foreach (var region in regions)
                    {
                        e.Graphics.FillRectangle(darkBrush, region);
                    }
                }

                // 绘制选区边框
                using (Pen pen = new Pen(Color.DodgerBlue, 2))
                {
                    e.Graphics.DrawRectangle(pen, _selectionRect);

                    // 在四个角绘制小方块
                    int squareSize = 6;
                    using (SolidBrush squareBrush = new SolidBrush(Color.DodgerBlue))
                    {
                        e.Graphics.FillRectangle(squareBrush, _selectionRect.X - squareSize/2, _selectionRect.Y - squareSize/2, squareSize, squareSize);
                        e.Graphics.FillRectangle(squareBrush, _selectionRect.Right - squareSize/2, _selectionRect.Y - squareSize/2, squareSize, squareSize);
                        e.Graphics.FillRectangle(squareBrush, _selectionRect.X - squareSize/2, _selectionRect.Bottom - squareSize/2, squareSize, squareSize);
                        e.Graphics.FillRectangle(squareBrush, _selectionRect.Right - squareSize/2, _selectionRect.Bottom - squareSize/2, squareSize, squareSize);
                    }
                }
            }
        }

        private async void Overlay_MouseUp(object sender, MouseEventArgs e)
        {
            if (_selectionRect.Width > 0 && _selectionRect.Height > 0)
            {
                _isDrawing = false;
                UpdateButtonsPosition();
                _overlay.Invalidate();
            }
            else
            {
                CancelCapture();
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

                        var requestData = new
                        {
                            model = _settings.Model,
                            messages = new[]
                            {
                                new
                                {
                                    role = "user",
                                    content = new object[]
                                    {
                                        new { type = "text", text = "请描述并解析这张图片的内容" },
                                        new
                                        {
                                            type = "image_url",
                                            image_url = new
                                            {
                                                url = $"data:image/png;base64,{base64Image}"
                                            }
                                        }
                                    }
                                }
                            },
                            max_tokens = _settings.MaxTokens
                        };

                        var jsonContent = JsonSerializer.Serialize(requestData);
                        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                        var response = await client.PostAsync(_settings.Endpoint, content);

                        if (response.IsSuccessStatusCode)
                        {
                            var result = await response.Content.ReadFromJsonAsync<GPTResponse>();
                            var responseContent = result.choices[0].message.content;

                            // 添加到历史记录时包含宽度
                            TranslationHistory.AddRecord(responseContent, _selectionRect.Width);

                            this.Invoke((MethodInvoker)delegate
                            {
                                ToastForm.ShowMessage(responseContent, _selectionRect.Width);
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
                    using (var bitmap = new Bitmap("bot.png"))
                    {
                        IntPtr hIcon = bitmap.GetHicon();
                        notifyIcon.Icon = Icon.FromHandle(hIcon);
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"加载自定义图标失: {ex.Message}");
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

                Trace.WriteLine("托盘图标初始化完成");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"初始化托盘图标失败: {ex.Message}");
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
                Trace.WriteLine($"更新快捷键: {string.Join(" + ", newHotKey)}");

                if (_globalHook != null)
                {
                    // 直接更新组合键，内部会重新注册钩子
                    _globalHook.KeyCombination = newHotKey;
                    Trace.WriteLine("快捷键更新完成");
                }
                else
                {
                    InitializeHotKey();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"更新快捷键时出错: {ex.Message}");
                MessageBox.Show($"更新快捷键时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void ApplyButton_Click(object sender, EventArgs e)
        {
            if (_selectionRect.Width > 0 && _selectionRect.Height > 0)
            {
                _overlay.Hide();
                await Task.Delay(100); // 等待overlay完全隐藏

                try
                {
                    // 立即显示加载提示
                    this.Invoke((MethodInvoker)delegate
                    {
                        ToastForm.ShowLoading(_selectionRect.Width);
                    });

                    // 获取所有屏幕的总边界
                    Rectangle virtualScreen = SystemInformation.VirtualScreen;

                    using (Bitmap bitmap = new Bitmap(_selectionRect.Width, _selectionRect.Height))
                    {
                        using (Graphics g = Graphics.FromImage(bitmap))
                        {
                            // 使用相对于虚拟屏幕的坐标
                            g.CopyFromScreen(
                                virtualScreen.X + _selectionRect.Left,
                                virtualScreen.Y + _selectionRect.Top,
                                0,
                                0,
                                new Size(_selectionRect.Width, _selectionRect.Height)
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
            // 清除选区
            _selectionRect = Rectangle.Empty;
            _isCapturing = false;
            _isDrawing = false;

            // 隐藏按钮
            if (_applyButton != null) _applyButton.Visible = false;
            if (_cancelButton != null) _cancelButton.Visible = false;

            if (_overlay != null && !_overlay.IsDisposed)
            {
                // 确保清理背景图像
                if (_overlay.BackgroundImage != null)
                {
                    _overlay.BackgroundImage.Dispose();
                    _overlay.BackgroundImage = null;
                }
                _overlay.Close();
                _overlay.Dispose();
                _overlay = null;
            }
        }

        private void CleanupCapture()
        {
            // 清除选区
            _selectionRect = Rectangle.Empty;
            _isCapturing = false;
            _isDrawing = false;

            // 隐藏按钮
            if (_applyButton != null) _applyButton.Visible = false;
            if (_cancelButton != null) _cancelButton.Visible = false;

            if (_overlay != null && !_overlay.IsDisposed)
            {
                // 确保清理背景图像
                if (_overlay.BackgroundImage != null)
                {
                    _overlay.BackgroundImage.Dispose();
                    _overlay.BackgroundImage = null;
                }
                _overlay.Close();
                _overlay.Dispose();
                _overlay = null;
            }
        }

    }

}
