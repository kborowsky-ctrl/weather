using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace WeatherWizard.Services;

/// <summary>
/// Shell notification-area ("system tray") icon with a simple context menu.
/// Hides the main window from the taskbar while the app is running.
/// </summary>
public sealed class TaskTrayIcon : IDisposable
{
    private const int WmCommand = 0x0111;
    private const uint WmAppTray = 0x8000 + 88;
    private const int WmLButtonDblClk = 0x0203;
    private const int WmRButtonUp = 0x0205;

    private const int NimAdd = 0;
    private const int NimModify = 1;
    private const int NimDelete = 2;

    private const int NifMessage = 0x00000001;
    private const int NifIcon = 0x00000002;
    private const int NifTip = 0x00000004;

    private const int MfString = 0x00000000;
    private const int MfSeparator = 0x00000800;

    private const int MenuOpenId = 1001;
    private const int MenuExitId = 1002;

    private const int GwlExstyle = -20;
    private const uint WsExToolwindow = 0x00000080;
    private const uint WsExAppwindow = 0x00040000;

    private const uint TpmLeftalign = 0x0000;
    private const uint TpmRightbutton = 0x0002;
    private const uint TpmBottomalign = 0x0020;
    private const uint TpmReturncmd = 0x0100;

    private readonly Window _window;
    private readonly nint _hwnd;
    private readonly SubclassProc _subclassProc;
    private readonly GCHandle _selfHandle;
    private Bitmap? _iconBitmap;
    private nint _iconHicon;
    private bool _iconAdded;

    public TaskTrayIcon(Window window)
    {
        _window = window;
        _hwnd = WindowNative.GetWindowHandle(window);
        _subclassProc = OnSubclassMessage;
        _selfHandle = GCHandle.Alloc(this);

        if (!SetWindowSubclass(_hwnd, _subclassProc, SubclassId, (nuint)(nint)GCHandle.ToIntPtr(_selfHandle)))
            throw new InvalidOperationException("SetWindowSubclass failed.");

        _iconBitmap = TrayWeatherIconFactory.CreateBitmap(-1);
        _iconHicon = _iconBitmap.GetHicon();

        var data = new NotifyIconDataW
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconDataW>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NifMessage | NifIcon | NifTip,
            uCallbackMessage = WmAppTray,
            hIcon = _iconHicon,
            szTip = "WeatherWizard",
        };

        if (!Shell_NotifyIconW(NimAdd, ref data))
        {
            _ = RemoveWindowSubclass(_hwnd, _subclassProc, SubclassId);
            if (_selfHandle.IsAllocated)
                _selfHandle.Free();
            CleanupIconBitmap();
            throw new InvalidOperationException("Could not add notification area icon (Shell_NotifyIcon).");
        }

        _iconAdded = true;
    }

    private const nuint SubclassId = 0x5757;

    /// <summary>Updates the tray glyph from weather code, day/night, and optional alert badge.</summary>
    public void SetWeatherIcon(int weatherCode, bool hasActiveAlert = false, bool isNight = false, DateTimeOffset at = default)
    {
        if (!_iconAdded)
            return;

        Bitmap nextBmp;
        try
        {
            nextBmp = TrayWeatherIconFactory.CreateBitmap(weatherCode, isNight, at, hasActiveAlert);
        }
        catch
        {
            return;
        }

        nint nextHicon;
        try
        {
            nextHicon = nextBmp.GetHicon();
        }
        catch
        {
            nextBmp.Dispose();
            return;
        }

        var prevBmp = _iconBitmap;
        var prevH = _iconHicon;
        _iconBitmap = nextBmp;
        _iconHicon = nextHicon;

        var data = new NotifyIconDataW
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconDataW>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NifMessage | NifIcon | NifTip,
            uCallbackMessage = WmAppTray,
            hIcon = _iconHicon,
            szTip = hasActiveAlert ? "WeatherWizard — active alert" : "WeatherWizard",
        };

        if (!Shell_NotifyIconW(NimModify, ref data))
        {
            _iconBitmap = prevBmp;
            _iconHicon = prevH;
            _ = DestroyIcon(nextHicon);
            nextBmp.Dispose();
            return;
        }

        if (prevH != nint.Zero)
            _ = DestroyIcon(prevH);
        prevBmp?.Dispose();
    }

    private void CleanupIconBitmap()
    {
        if (_iconHicon != nint.Zero)
        {
            _ = DestroyIcon(_iconHicon);
            _iconHicon = nint.Zero;
        }

        _iconBitmap?.Dispose();
        _iconBitmap = null;
    }

    /// <summary>
    /// Prefer WinAppSDK API; fall back to extended style so the window does not get a taskbar button.
    /// </summary>
    public static void ApplyNoTaskbarButton(Window window)
    {
        try
        {
            window.AppWindow.IsShownInSwitchers = false;
        }
        catch
        {
            // Older runtimes: rely on native style only.
        }

        var hwnd = WindowNative.GetWindowHandle(window);
        var ex = (uint)GetWindowLongPtrW(hwnd, GwlExstyle);
        ex = (ex & ~WsExAppwindow) | WsExToolwindow;
        _ = SetWindowLongPtrW(hwnd, GwlExstyle, (nint)ex);
    }

    private static nint OnSubclassMessage(nint hWnd, uint msg, nint wParam, nint lParam, nuint uIdSubclass, nuint dwRefData)
    {
        var self = (TaskTrayIcon)GCHandle.FromIntPtr((nint)dwRefData).Target!;

        if (msg == WmAppTray && self._iconAdded)
        {
            var mouse = (uint)(nint)lParam;
            if (mouse == WmLButtonDblClk)
            {
                self._window.DispatcherQueue.TryEnqueue(self.ActivateMainWindow);
                return nint.Zero;
            }

            if (mouse == WmRButtonUp)
            {
                self._window.DispatcherQueue.TryEnqueue(self.ShowContextMenu);
                return nint.Zero;
            }
        }

        if (msg == WmCommand)
        {
            var id = (int)(uint)(nint)wParam & 0xFFFF;
            if (id is MenuOpenId or MenuExitId)
            {
                if (self._window.DispatcherQueue.HasThreadAccess)
                {
                    if (id == MenuOpenId)
                        self.ActivateMainWindow();
                    else
                        Microsoft.UI.Xaml.Application.Current.Exit();
                }

                return nint.Zero;
            }
        }

        return DefSubclassProc(hWnd, msg, wParam, lParam, uIdSubclass, dwRefData);
    }

    private void ActivateMainWindow()
    {
        if (_window.AppWindow.Presenter is OverlappedPresenter op)
        {
            if (op.State == OverlappedPresenterState.Minimized)
                op.Restore();
        }

        _window.AppWindow.Show();
        _window.Activate();
    }

    private void ShowContextMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == nint.Zero)
            return;

        try
        {
            _ = AppendMenuW(menu, MfString, new nint(MenuOpenId), "Open WeatherWizard");
            _ = AppendMenuW(menu, MfSeparator, nint.Zero, null);
            _ = AppendMenuW(menu, MfString, new nint(MenuExitId), "Exit");

            _ = GetCursorPos(out var pt);
            _ = SetForegroundWindow(_hwnd);
            var cmd = TrackPopupMenuEx(menu, TpmLeftalign | TpmRightbutton | TpmBottomalign | TpmReturncmd, pt.X, pt.Y, _hwnd, nint.Zero);
            if (cmd != 0)
                _ = PostMessageW(_hwnd, WmCommand, new nint((int)cmd), nint.Zero);
        }
        finally
        {
            _ = DestroyMenu(menu);
        }
    }

    public void Dispose()
    {
        if (_iconAdded)
        {
            var data = new NotifyIconDataW
            {
                cbSize = (uint)Marshal.SizeOf<NotifyIconDataW>(),
                hWnd = _hwnd,
                uID = 1,
            };
            _ = Shell_NotifyIconW(NimDelete, ref data);
            _iconAdded = false;
        }

        _ = RemoveWindowSubclass(_hwnd, _subclassProc, SubclassId);

        CleanupIconBitmap();

        if (_selfHandle.IsAllocated)
            _selfHandle.Free();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconDataW
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint SubclassProc(nint hWnd, uint msg, nint wParam, nint lParam, nuint uIdSubclass, nuint dwRefData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern bool Shell_NotifyIconW(int dwMessage, ref NotifyIconDataW lpData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(nint hWnd, SubclassProc pfnSubclass, nuint uIdSubclass, nuint dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(nint hWnd, SubclassProc pfnSubclass, nuint uIdSubclass);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern nint DefSubclassProc(nint hWnd, uint msg, nint wParam, nint lParam, nuint uIdSubclass, nuint dwRefData);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtrW(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtrW(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(nint hIcon);

    [DllImport("user32.dll")]
    private static extern nint CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern bool AppendMenuW(nint hMenu, uint uFlags, nint uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyMenu(nint hMenu);

    [DllImport("user32.dll")]
    private static extern nint TrackPopupMenuEx(nint hMenu, uint fuFlags, int x, int y, nint hwnd, nint lptpm);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point pt);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern bool PostMessageW(nint hWnd, uint msg, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }
}
