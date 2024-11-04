using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace AruScreenSummary
{
    public class BaseSettingsForm : Form
    {
        private static BaseSettingsForm _currentInstance;

        public BaseSettingsForm()
        {
            // 如果有已打开的窗口，先关闭它
            if (_currentInstance != null && !_currentInstance.IsDisposed)
            {
                var oldInstance = _currentInstance;
                _currentInstance = null;  // 先清除引用，避免关闭事件中的冲突
                oldInstance.Close();
                oldInstance.Dispose();
            }
            _currentInstance = this;

            // 设置基本属性
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;
            this.KeyPreview = true;

            // 设置 ESC 关闭功能
            this.KeyDown += BaseSettingsForm_KeyDown;
        }

        private void BaseSettingsForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Debug.WriteLine("ESC key pressed, closing form");
                this.Close();
                e.Handled = true;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (this == _currentInstance)
            {
                _currentInstance = null;
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && this == _currentInstance)
            {
                _currentInstance = null;
            }
            base.Dispose(disposing);
        }

        public static bool IsAnyFormOpen()
        {
            return _currentInstance != null && !_currentInstance.IsDisposed;
        }
    }
}
