using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.ComponentModel;
using System.Linq;

namespace ScreenshotGPT
{
    public class GlobalKeyboardHook : IDisposable
    {
        private IntPtr _hookHandle = IntPtr.Zero;
        private HookProc _hookProc;
        private Keys[] _keyCombination;
        private HashSet<Keys> _pressedKeys = new HashSet<Keys>();
        private bool _disposed;

        public event Action KeyPressed;

        public Keys[] KeyCombination
        {
            get => _keyCombination;
            set
            {
                _keyCombination = value;
                // 重新注册钩子
                UninstallHook();
                InstallHook();
            }
        }

        public GlobalKeyboardHook()
        {
            _hookProc = HookCallback;
            InstallHook();
        }

        public void InstallHook()
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                _hookHandle = SetWindowsHookEx(
                    WH_KEYBOARD_LL,
                    _hookProc,
                    GetModuleHandle(curModule.ModuleName),
                    0);
            }

            if (_hookHandle == IntPtr.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                throw new Win32Exception(errorCode, $"Failed to install hook. Error {errorCode}");
            }
        }

        private void UninstallHook()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && KeyCombination != null)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Keys key = (Keys)vkCode;
                bool keyDown = (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN);
                bool keyUp = (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP);

                if (keyDown)
                {
                    Trace.WriteLine($"按下按键: {key}");
                    if (!_pressedKeys.Contains(key))
                    {
                        _pressedKeys.Add(key);
                        Trace.WriteLine($"当前按下的按键: {string.Join(" + ", _pressedKeys)}");
                        CheckHotkey();
                    }
                }
                else if (keyUp)
                {
                    Trace.WriteLine($"释放按键: {key}");
                    _pressedKeys.Remove(key);
                }
            }

            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        private void CheckHotkey()
        {
            if (_keyCombination == null) return;

            bool allKeysPressed = _keyCombination.All(k =>
            {
                if (k == Keys.ControlKey)
                    return _pressedKeys.Contains(Keys.LControlKey) || _pressedKeys.Contains(Keys.RControlKey);
                if (k == Keys.Menu)
                    return _pressedKeys.Contains(Keys.LMenu) || _pressedKeys.Contains(Keys.RMenu);
                if (k == Keys.ShiftKey)
                    return _pressedKeys.Contains(Keys.LShiftKey) || _pressedKeys.Contains(Keys.RShiftKey);

                return _pressedKeys.Contains(k);
            });

            bool onlyHotkeyKeysPressed = _pressedKeys.Count == _keyCombination.Length;

            Trace.WriteLine($"检查快捷键 - 所有按键已按下: {allKeysPressed}, 只按下了快捷键按键: {onlyHotkeyKeysPressed}");
            Trace.WriteLine($"期望的按键组合: {string.Join(" + ", _keyCombination)}");
            Trace.WriteLine($"当前按下的按键: {string.Join(" + ", _pressedKeys)}");

            if (allKeysPressed && onlyHotkeyKeysPressed)
            {
                Trace.WriteLine("触发快捷键事件");
                KeyPressed?.Invoke();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 释放托管资源
                }

                // 释放非托管资源
                UninstallHook();
                _disposed = true;
            }
        }

        ~GlobalKeyboardHook()
        {
            Dispose(false);
        }

        // 添加公共方法来安全地卸载钩子
        public void UninstallCurrentHook()
        {
            UninstallHook();
        }

        #region Native Methods
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        #endregion
    }
}

