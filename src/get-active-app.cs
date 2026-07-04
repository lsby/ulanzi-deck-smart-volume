using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

public class GetActiveApp {
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError=true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public static void Main() {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;
        uint pid = 0;
        GetWindowThreadProcessId(hwnd, out pid);
        if (pid > 0) {
            try {
                Process proc = Process.GetProcessById((int)pid);
                Console.Write(proc.ProcessName + ".exe");
            } catch {
                Console.Write("");
            }
        }
    }
}
