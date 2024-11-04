using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing.Imaging;
using Markdig;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

public class ToastForm : Form
{
    private static ToastForm _instance;
    private string _message;
    private Button _closeButton;
    private const int PADDING = 20;
    private const int CLOSE_BUTTON_SIZE = 24;
    private const int AVATAR_SIZE = 256;
    private const int AVATAR_MARGIN = 20;
    private Image _avatarImage;
    private Rectangle _textBounds;
    private Rectangle _avatarBounds;
    private bool _isLoading = false;
    private System.Windows.Forms.Timer _loadingTimer;
    private int _loadingDots = 0;
    private const string LOADING_TEXT = "正在分析中";
    private WebView2 _webView;

    public static void ShowLoading(int width = 400)
    {
        if (_instance == null || _instance.IsDisposed)
        {
            _instance = new ToastForm(LOADING_TEXT, width, true);
            _instance.Show();
        }
        else
        {
            _instance.UpdateMessage(LOADING_TEXT, width, true);
        }
    }

    public static void ShowMessage(string message, int width = 400)
    {
        if (_instance == null || _instance.IsDisposed)
        {
            _instance = new ToastForm(message, width);
            _instance.Show();
        }
        else
        {
            _instance.UpdateMessage(message, width);
        }
    }

    private ToastForm(string message, int width = 400, bool isLoading = false)
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor |
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
        _message = message;
        _isLoading = isLoading;

        if (_isLoading)
        {
            _loadingTimer = new System.Windows.Forms.Timer();
            _loadingTimer.Interval = 500; // 每500ms更新一次
            _loadingTimer.Tick += (s, e) =>
            {
                _loadingDots = (_loadingDots + 1) % 4;
                this.Invalidate();
            };
            _loadingTimer.Start();
        }

        try
        {
            using (var originalImage = Image.FromFile("bot.png"))
            {
                _avatarImage = new Bitmap(AVATAR_SIZE, AVATAR_SIZE, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(_avatarImage))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.CompositingMode = CompositingMode.SourceOver;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.DrawImage(originalImage, 0, 0, AVATAR_SIZE, AVATAR_SIZE);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"加载头像失败: {ex.Message}");
            _avatarImage = null;
        }

        // 添加键盘事件处理
        this.KeyPreview = true;  // 确保窗体可以接收键盘事件
        this.KeyDown += ToastForm_KeyDown;

        InitializeForm(width);
    }

    private void ToastForm_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            this.Close();
            e.Handled = true;
        }
    }

    private void InitializeForm(int preferredWidth)
    {
        this.FormBorderStyle = FormBorderStyle.None;
        this.ShowInTaskbar = false;
        this.TopMost = true;
        this.BackColor = Color.Fuchsia;
        this.TransparencyKey = Color.Fuchsia;
        this.ForeColor = Color.White;
        this.Font = new Font("微软雅黑", 10F);

        // 先计算布局
        int effectiveWidth = Math.Max(Math.Min(preferredWidth, Screen.PrimaryScreen.WorkingArea.Width - 100), 400);
        using (Graphics g = CreateGraphics())
        {
            SizeF textSize = g.MeasureString(_message, this.Font, effectiveWidth - PADDING * 2);

            // 文本区域
            _textBounds = new Rectangle(
                AVATAR_SIZE + AVATAR_MARGIN,
                0,
                effectiveWidth,
                (int)Math.Max(textSize.Height + PADDING * 2, AVATAR_SIZE)
            );

            // 头像区域
            _avatarBounds = new Rectangle(
                0,
                _textBounds.Height / 2 - AVATAR_SIZE / 2,
                AVATAR_SIZE,
                AVATAR_SIZE
            );

            // 设置窗口大小
            this.Size = new Size(
                _textBounds.Right,
                _textBounds.Height
            );
        }

        // 创建关闭按钮
        _closeButton = new Button
        {
            Size = new Size(CLOSE_BUTTON_SIZE, CLOSE_BUTTON_SIZE),
            Text = "×",
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(64, 64, 64),
            Cursor = Cursors.Hand,
            Font = new Font("Arial", 12, FontStyle.Bold),
            Location = new Point(_textBounds.Right - CLOSE_BUTTON_SIZE - 5, 5)
        };
        _closeButton.FlatAppearance.BorderSize = 0;
        _closeButton.Click += (s, e) => this.Close();
        this.Controls.Add(_closeButton);

        InitializeWebView();

        // 设置窗体位置
        Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
        this.Location = new Point(
            workingArea.Right - this.Width - 20,
            workingArea.Bottom - this.Height - 20
        );

        this.Paint += ToastForm_Paint;

        // 添加淡入效果
        this.Opacity = 0;
        System.Windows.Forms.Timer fadeTimer = new System.Windows.Forms.Timer();
        fadeTimer.Interval = 10;
        fadeTimer.Tick += (s, e) =>
        {
            if (this.Opacity < 1)
                this.Opacity += 0.1;
            else
                fadeTimer.Stop();
        };
        fadeTimer.Start();

        // 添加鼠标事件以支持拖动
        this.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                NativeMethods.ReleaseCapture();
                NativeMethods.SendMessage(Handle, 0xA1, 0x2, 0);
            }
        };

        // 确保窗体可以获得焦点
        this.Activated += (s, e) => this.Focus();

        // 确保窗和 WebView2 都能接收键盘事件
        this.KeyPreview = true;
        this.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
                e.Handled = true;
            }
        };
    }

    private async void InitializeWebView()
    {
        _webView = new WebView2
        {
            Location = new Point(_textBounds.X + PADDING, _textBounds.Y + PADDING),
            Size = new Size(_textBounds.Width - PADDING * 2, _textBounds.Height - PADDING * 2),
            DefaultBackgroundColor = Color.FromArgb(64, 64, 64)
        };

        await _webView.EnsureCoreWebView2Async();

        // 添加键盘事件处理
        _webView.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
                e.Handled = true;
            }
        };

        // 使用替代方案：将 HTML 模板分成几部分
        string htmlStart = @"
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    body {
                        color: white;
                        font-family: '微软雅黑';
                        font-size: 10pt;
                        margin: 0;
                        padding: 0;
                        background-color: rgb(64, 64, 64);
                    }
                    code {
                        background-color: #444;
                        padding: 2px 4px;
                        border-radius: 3px;
                        font-family: Consolas, monospace;
                    }
                    pre {
                        background-color: #444;
                        padding: 10px;
                        border-radius: 5px;
                        overflow-x: auto;
                    }
                    blockquote {
                        border-left: 4px solid #666;
                        margin: 0;
                        padding-left: 10px;
                    }
                    a { color: #3498db; }
                    p { margin: 0 0 10px 0; }
                </style>
            </head>
            <body>";

        string htmlEnd = @"
            </body>
            </html>";

        string content = ConvertMarkdownToHtml(_message);
        string html = htmlStart + content + htmlEnd;

        _webView.NavigateToString(html);
        this.Controls.Add(_webView);
    }

    private void UpdateMessage(string message, int width, bool isLoading = false)
    {
        _message = message;
        _isLoading = isLoading;

        if (_isLoading && _loadingTimer == null)
        {
            _loadingTimer = new System.Windows.Forms.Timer();
            _loadingTimer.Interval = 500;
            _loadingTimer.Tick += (s, e) =>
            {
                _loadingDots = (_loadingDots + 1) % 4;
                this.Invalidate();
            };
            _loadingTimer.Start();
        }
        else if (!_isLoading && _loadingTimer != null)
        {
            _loadingTimer.Stop();
            _loadingTimer.Dispose();
            _loadingTimer = null;
        }

        // 计算新的布局，但不重新创建控件
        int effectiveWidth = Math.Max(Math.Min(width, Screen.PrimaryScreen.WorkingArea.Width - 100), 400);
        using (Graphics g = CreateGraphics())
        {
            SizeF textSize = g.MeasureString(_message, this.Font, effectiveWidth - PADDING * 2);

            // 文本区域
            _textBounds = new Rectangle(
                AVATAR_SIZE + AVATAR_MARGIN,
                0,
                effectiveWidth,
                (int)Math.Max(textSize.Height + PADDING * 2, AVATAR_SIZE)
            );

            // 头像区域
            _avatarBounds = new Rectangle(
                0,
                _textBounds.Height / 2 - AVATAR_SIZE / 2,
                AVATAR_SIZE,
                AVATAR_SIZE
            );

            // 更新窗口大小
            this.Size = new Size(
                _textBounds.Right,
                _textBounds.Height
            );
        }

        // 更新关闭按钮位置
        _closeButton.Location = new Point(
            _textBounds.Right - CLOSE_BUTTON_SIZE - 5,
            5
        );

        // 更新窗口位置
        Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
        this.Location = new Point(
            workingArea.Right - this.Width - 20,
            workingArea.Bottom - this.Height - 20
        );

        // 更新 WebView 内容
        string displayText = _message;
        if (_isLoading)
        {
            displayText = $"{_message}{new string('.', _loadingDots)}";
        }

        if (_webView != null)
        {
            // 使用相同的 HTML 模板
            string htmlStart = @"
                <!DOCTYPE html>
                <html>
                <head>
                    <style>
                        body {
                            color: white;
                            font-family: '微软雅黑';
                            font-size: 10pt;
                            margin: 0;
                            padding: 0;
                            background-color: rgb(64, 64, 64);
                        }
                        code {
                            background-color: #444;
                            padding: 2px 4px;
                            border-radius: 3px;
                            font-family: Consolas, monospace;
                        }
                        pre {
                            background-color: #444;
                            padding: 10px;
                            border-radius: 5px;
                            overflow-x: auto;
                        }
                        blockquote {
                            border-left: 4px solid #666;
                            margin: 0;
                            padding-left: 10px;
                        }
                        a { color: #3498db; }
                        p { margin: 0 0 10px 0; }
                    </style>
                </head>
                <body>";

            string htmlEnd = @"
                </body>
                </html>";

            string content = ConvertMarkdownToHtml(displayText);
            string html = htmlStart + content + htmlEnd;

            _webView.NavigateToString(html);
            _webView.Location = new Point(_textBounds.X + PADDING, _textBounds.Y + PADDING);
            _webView.Size = new Size(_textBounds.Width - PADDING * 2, _textBounds.Height - PADDING * 2);
        }

        // 重绘窗口
        this.Invalidate();
    }

    private string ConvertMarkdownToHtml(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
        return Markdig.Markdown.ToHtml(markdown, pipeline);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        this.UpdateStyles();
    }

    private void ToastForm_Paint(object sender, PaintEventArgs e)
    {
        e.Graphics.Clear(this.BackColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.CompositingMode = CompositingMode.SourceOver;
        e.Graphics.CompositingQuality = CompositingQuality.HighQuality;

        // 绘制文本区域背景
        using (GraphicsPath path = new GraphicsPath())
        {
            path.AddRoundedRectangle(_textBounds, 10);
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(64, 64, 64)))
            {
                e.Graphics.FillPath(brush, path);
            }
        }

        // 绘制头像（使用圆形裁剪）
        if (_avatarImage != null)
        {
            using (GraphicsPath avatarPath = new GraphicsPath())
            {
                avatarPath.AddEllipse(_avatarBounds);
                e.Graphics.SetClip(avatarPath);
                using (ImageAttributes imageAttr = new ImageAttributes())
                {
                    imageAttr.SetWrapMode(WrapMode.TileFlipXY);
                    e.Graphics.DrawImage(_avatarImage, _avatarBounds,
                        0, 0, _avatarImage.Width, _avatarImage.Height,
                        GraphicsUnit.Pixel, imageAttr);
                }
                e.Graphics.ResetClip();
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _loadingTimer?.Stop();
            _loadingTimer?.Dispose();
            _avatarImage?.Dispose();
            _webView?.Dispose();
            if (this == _instance)
            {
                _instance = null;
            }
        }
        base.Dispose(disposing);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x80000; // WS_EX_LAYERED
            return cp;
        }
    }
}

// 扩展方法用于创建圆角矩形路径
public static class GraphicsExtensions
{
    public static void AddRoundedRectangle(this GraphicsPath path, Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        Rectangle arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        // 左上角
        path.AddArc(arc, 180, 90);

        // 右上角
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);

        // 右下角
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);

        // 左下角
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);

        path.CloseFigure();
    }
}

// 用于窗口拖动的本地方法
internal static class NativeMethods
{
    [DllImport("user32.dll")]
    public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
    [DllImport("user32.dll")]
    public static extern bool ReleaseCapture();
}
