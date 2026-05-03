using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Ember_Config_Tool.Services;
using Ember_Config_Tool.ViewModels;

namespace Ember_Config_Tool;

public partial class MainWindow : Window
{
    public MainWindow(AppOptions options)
    {
        InitializeComponent();
        DataContext = new MainViewModel(options);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyDarkChrome();
    }

    private void ApplyDarkChrome()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var darkMode = 1;
            _ = DwmSetWindowAttribute(hwnd, 20, ref darkMode, Marshal.SizeOf<int>());
            _ = DwmSetWindowAttribute(hwnd, 19, ref darkMode, Marshal.SizeOf<int>());

            var caption = ColorRef(0x15, 0x17, 0x19);
            var border = ColorRef(0x3A, 0x41, 0x45);
            var text = ColorRef(0xF4, 0xF1, 0xEA);
            _ = DwmSetWindowAttribute(hwnd, 35, ref caption, Marshal.SizeOf<uint>());
            _ = DwmSetWindowAttribute(hwnd, 34, ref border, Marshal.SizeOf<uint>());
            _ = DwmSetWindowAttribute(hwnd, 36, ref text, Marshal.SizeOf<uint>());
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    private static uint ColorRef(byte red, byte green, byte blue)
    {
        return (uint)(red | (green << 8) | (blue << 16));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref uint attributeValue, int attributeSize);
}
