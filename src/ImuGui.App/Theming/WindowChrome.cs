using System.Runtime.InteropServices;

namespace ImuGui.App.Theming;

/// <summary>Best-effort native window chrome tweaks (dark title bar on Windows 10 1809+).</summary>
internal static class WindowChrome
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    /// <summary>
    /// Requests a dark (or light) title bar for the form. Silently does nothing on
    /// systems without DWM support — purely cosmetic, never load-bearing.
    /// </summary>
    internal static void TrySetDarkTitleBar(Form form, bool dark)
    {
        if (!form.IsHandleCreated)
        {
            form.HandleCreated += (_, _) => TrySetDarkTitleBar(form, dark);
            return;
        }

        try
        {
            int value = dark ? 1 : 0;
            _ = DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref value, sizeof(int));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Pre-DWM or stripped-down system: keep the default title bar.
        }
    }

    // Classic DllImport on purpose: LibraryImport's generator would force
    // AllowUnsafeBlocks on the whole project for one cosmetic call.
    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle, int attribute, ref int value, int valueSize);
}
