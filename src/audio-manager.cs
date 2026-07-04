using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace VolumeOSD.Audio
{
    internal class AppAudioSession {
        public string AppName;
        public float Volume;
        public bool IsMaster;
        public List<ISimpleAudioVolume> Sessions = new List<ISimpleAudioVolume>();
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumerator { }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr ppDevices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr pNotify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr pNotify);
        [PreserveSig] int GetChannelCount(out int pnChannelCount);
        [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, Guid pguidEventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, Guid pguidEventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
    }

    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(ref Guid AudioSessionGuid, int StreamFlags, out IntPtr SessionControl);
        [PreserveSig] int GetSimpleAudioVolume(ref Guid AudioSessionGuid, int StreamFlags, out ISimpleAudioVolume AudioVolume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator SessionEnum);
        [PreserveSig] int RegisterSessionNotification(IntPtr SessionNotification);
        [PreserveSig] int UnregisterSessionNotification(IntPtr SessionNotification);
        [PreserveSig] int RegisterDuckNotification(string sessionID, IntPtr duckNotification);
        [PreserveSig] int UnregisterDuckNotification(IntPtr duckNotification);
    }

    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int SessionCount);
        [PreserveSig] int GetSession(int SessionCount, out IAudioSessionControl2 Session);
    }

    [Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionControl2
    {
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName(out IntPtr name);
        [PreserveSig] int SetDisplayName(string value, Guid EventContext);
        [PreserveSig] int GetIconPath(out IntPtr path);
        [PreserveSig] int SetIconPath(string value, Guid EventContext);
        [PreserveSig] int GetGroupingParam(out Guid groupingParam);
        [PreserveSig] int SetGroupingParam(Guid Override, Guid EventContext);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr NewNotifications);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr NewNotifications);
        [PreserveSig] int GetSessionIdentifier(out IntPtr retVal);
        [PreserveSig] int GetSessionInstanceIdentifier(out IntPtr retVal);
        [PreserveSig] int GetProcessId(out uint retVal);
        [PreserveSig] int IsSystemSoundsSession();
        [PreserveSig] int SetDuckingPreference(bool optOut);
    }

    [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float fLevel, ref Guid EventContext);
        [PreserveSig] int GetMasterVolume(out float pfLevel);
        [PreserveSig] int SetMute(bool bMute, ref Guid EventContext);
        [PreserveSig] int GetMute(out bool pbMute);
    }

    internal static class AudioManager
    {
        internal static IMMDevice GetDefaultDevice()
        {
            IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            IMMDevice dev;
            enumerator.GetDefaultAudioEndpoint(0, 1, out dev);
            return dev;
        }

        internal static IAudioEndpointVolume GetMasterVolumeObject()
        {
            IMMDevice dev = GetDefaultDevice();
            if (dev == null) return null;
            Guid iid = typeof(IAudioEndpointVolume).GUID;
            object obj;
            dev.Activate(ref iid, 23 /* CLSCTX_ALL */, IntPtr.Zero, out obj);
            return (IAudioEndpointVolume)obj;
        }

        internal static IAudioSessionManager2 GetSessionManager()
        {
            IMMDevice dev = GetDefaultDevice();
            if (dev == null) return null;
            Guid iid = typeof(IAudioSessionManager2).GUID;
            object obj;
            dev.Activate(ref iid, 23, IntPtr.Zero, out obj);
            return (IAudioSessionManager2)obj;
        }

        internal static void ChangeMasterVolume(float step, out float newVolume)
        {
            var endpoint = GetMasterVolumeObject();
            newVolume = 0;
            if (endpoint == null) return;
            float vol;
            endpoint.GetMasterVolumeLevelScalar(out vol);
            vol += step;
            if (vol < 0) vol = 0;
            if (vol > 1) vol = 1;
            Guid emptyGuid = Guid.Empty;
            endpoint.SetMasterVolumeLevelScalar(vol, emptyGuid);
            newVolume = vol;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public static void Log(string msg) {
            // 调试通过后关闭日志，避免频繁的 UI 线程磁盘 I/O 导致卡顿
            return;
        }

        private static Dictionary<uint, string> _pidNameCache = new Dictionary<uint, string>();
        private static DateTime _lastCacheClear = DateTime.Now;

        private static string GetProcessNameCached(uint pid) {
            if (pid == 0) return "";
            if (DateTime.Now.Subtract(_lastCacheClear).TotalSeconds > 2) {
                _pidNameCache.Clear();
                _lastCacheClear = DateTime.Now;
            }
            if (_pidNameCache.ContainsKey(pid)) return _pidNameCache[pid];

            try {
                using (Process proc = Process.GetProcessById((int)pid)) {
                    string name = proc.ProcessName;
                    _pidNameCache[pid] = name;
                    return name;
                }
            } catch {
                _pidNameCache[pid] = "";
                return "";
            }
        }

        internal static bool ChangeForegroundVolume(float step, out string appName, out float newVolume)
        {
            appName = "";
            newVolume = 0;
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            if (pid == 0) return false;

            appName = GetProcessNameCached(pid);

            try {
                var mgr = GetSessionManager();
                if (mgr == null) return false;

                IAudioSessionEnumerator sessionEnum;
                mgr.GetSessionEnumerator(out sessionEnum);
                if (sessionEnum == null) return false;

                int count;
                sessionEnum.GetCount(out count);
                bool found = false;

                for (int i = 0; i < count; i++)
                {
                    IAudioSessionControl2 ctl;
                    sessionEnum.GetSession(i, out ctl);
                    if (ctl != null)
                    {
                        uint cPid;
                        ctl.GetProcessId(out cPid);
                        bool match = false;
                        if (cPid == pid) {
                            match = true;
                        } else if (!string.IsNullOrEmpty(appName)) {
                            string cName = GetProcessNameCached(cPid);
                            if (cName.Equals(appName, StringComparison.OrdinalIgnoreCase)) {
                                match = true;
                            }
                        }

                        if (match)
                        {
                            var sav = ctl as ISimpleAudioVolume;
                            if (sav != null)
                            {
                                float vol;
                                sav.GetMasterVolume(out vol);
                                vol += step;
                                if (vol < 0) vol = 0;
                                if (vol > 1) vol = 1;
                                Guid emptyGuid = Guid.Empty;
                                sav.SetMasterVolume(vol, ref emptyGuid);
                                newVolume = vol;
                                found = true;
                            }
                        }
                    }
                }
                return found;
            } catch (Exception) {
                return false;
            }
        }

        internal static List<AppAudioSession> GetActiveSessions()
        {
            var list = new List<AppAudioSession>();
            try {
                var endpoint = GetMasterVolumeObject();
                if (endpoint != null) {
                    float masterVol;
                    endpoint.GetMasterVolumeLevelScalar(out masterVol);
                    list.Add(new AppAudioSession {
                        AppName = "系统主音量",
                        Volume = masterVol,
                        IsMaster = true
                    });
                }

                var dict = new Dictionary<string, AppAudioSession>(StringComparer.OrdinalIgnoreCase);
                var mgr = GetSessionManager();
                if (mgr != null) {
                    IAudioSessionEnumerator sessionEnum;
                    mgr.GetSessionEnumerator(out sessionEnum);
                    if (sessionEnum != null) {
                        int count;
                        sessionEnum.GetCount(out count);

                        for (int i = 0; i < count; i++)
                        {
                            IAudioSessionControl2 ctl;
                            sessionEnum.GetSession(i, out ctl);
                            if (ctl != null)
                            {
                                uint cPid;
                                ctl.GetProcessId(out cPid);
                                if (cPid == 0) continue;

                                string name = GetProcessNameCached(cPid);
                                if (string.IsNullOrEmpty(name) || name == "Idle") continue;

                                var sav = ctl as ISimpleAudioVolume;
                                if (sav != null)
                                {
                                    float vol;
                                    sav.GetMasterVolume(out vol);
                                    
                                    if (!dict.ContainsKey(name)) {
                                        dict[name] = new AppAudioSession { AppName = name, Volume = vol };
                                    }
                                    dict[name].Sessions.Add(sav);
                                }
                            }
                        }
                    }
                }
                list.AddRange(dict.Values);
            } catch { }
            return list;
        }

        internal static void ChangeAppVolume(AppAudioSession session, float step) {
            if (session.IsMaster) {
                float newVol;
                ChangeMasterVolume(step, out newVol);
                session.Volume = newVol;
                return;
            }

            session.Volume += step;
            if (session.Volume < 0) session.Volume = 0;
            if (session.Volume > 1) session.Volume = 1;
            
            Guid emptyGuid = Guid.Empty;
            foreach (var sav in session.Sessions) {
                try {
                    sav.SetMasterVolume(session.Volume, ref emptyGuid);
                } catch { }
            }
        }
    }
}
