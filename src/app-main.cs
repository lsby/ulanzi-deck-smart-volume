using System;
using System.IO.Pipes;
using System.IO;
using System.Threading;
using System.Windows;
using System.Diagnostics;

namespace VolumeOSD {
    public class App : Application {
        private static Mutex mutex = null;

        [STAThread]
        public static void Main(string[] args) {
            string msg = null;
            int parentPid = -1;
            if (args.Length == 2) {
                msg = args[0] + "|" + args[1];
            } else if (args.Length >= 3 && args[0] == "CMD") {
                msg = args[0] + "|" + args[1] + "|" + args[2];
                if (args.Length >= 4) {
                    int.TryParse(args[3], out parentPid);
                }
            }

            bool createdNew;
            mutex = new Mutex(true, "UlanziSmartVolumeOSDMutex", out createdNew);

            if (!createdNew) {
                // 另一个实例已经在运行，作为客户端发送数据
                if (msg != null && !msg.StartsWith("CMD|Preload")) {
                    try {
                        using (var client = new NamedPipeClientStream(".", "UlanziSmartVolumeOSD", PipeDirection.Out, PipeOptions.None)) {
                            client.Connect(200);
                            using (var writer = new StreamWriter(client)) {
                                writer.WriteLine(msg);
                                writer.Flush();
                            }
                        }
                    } catch { }
                }
                return;
            }

            if (parentPid != -1) {
                Thread monitorThread = new Thread(() => {
                    try {
                        var p = Process.GetProcessById(parentPid);
                        p.WaitForExit();
                    } catch { }
                    Environment.Exit(0);
                });
                monitorThread.IsBackground = true;
                monitorThread.Start();
            }

            // 我们是第一个实例，作为服务端运行
            var app = new App();
                app.Startup += (s, e) => {
                var window = new OsdWindow();
                window.Opacity = 0;
                window.Show();
                window.Visibility = Visibility.Hidden;
                
                Action<string> processMessage = (line) => {
                    if (line == null) return;
                    var parts = line.Split('|');
                    if (parts.Length >= 3 && parts[0] == "CMD") {
                        string target = parts[1];
                        if (target == "Preload") return; // 仅预热，不显示
                        float step = float.Parse(parts[2]) / 100f;
                        string context = parts.Length >= 4 ? parts[3] : "default";
                        
                        if (target == "HoldStart") {
                            window.StartHoldProgress();
                            return;
                        }
                        if (target == "HoldCancel") {
                            window.CancelHoldProgress();
                            return;
                        }
                        if (target == "SetDefaultApp") {
                            window.SetDefaultApp(context);
                            return;
                        }
                        if (target == "ReloadConfig") {
                            window.ReloadConfig();
                            return;
                        }
                        if (target == "ToggleListMode") {
                            window.ToggleListMode(context);
                            return;
                        }
                        if (target == "SingleClick") {
                            window.ToggleItemLock();
                            return;
                        }

                        if (window.IsInListMode) {
                            if (window.ListModeContext == context) {
                                if (target == "Master") {
                                    if (window.IsItemLocked) {
                                        window.ChangeSelectedVolume(step);
                                    } else {
                                        window.ListScroll(step > 0 ? 1 : -1);
                                    }
                                } else if (target == "Foreground") {
                                    window.ChangeSelectedVolume(step);
                                }
                            }
                        } else {
                            if (target == "Master") {
                                string defaultApp = window.GetDefaultApp(context);
                                if (!string.IsNullOrEmpty(defaultApp) && defaultApp != "系统主音量") {
                                    string outAppName;
                                    float vol;
                                    bool ok = VolumeOSD.Audio.AudioManager.ChangeAppVolumeByName(defaultApp, step, out outAppName, out vol);
                                    if (ok) {
                                        window.UpdateVolume(outAppName, Math.Round(vol * 100));
                                        return;
                                    }
                                }
                                float mVol;
                                VolumeOSD.Audio.AudioManager.ChangeMasterVolume(step, out mVol);
                                window.UpdateVolume("系统主音量", Math.Round(mVol * 100));
                            } else if (target == "Foreground") {
                                string appName;
                                float vol;
                                bool ok = VolumeOSD.Audio.AudioManager.ChangeForegroundVolume(step, out appName, out vol);
                                if (ok) {
                                    window.UpdateVolume(appName, Math.Round(vol * 100));
                                } else {
                                    window.UpdateVolume("未找到音频", 0);
                                }
                            }
                        }
                    } else if (parts.Length == 2) {
                        window.UpdateVolume(parts[0], double.Parse(parts[1]));
                    }
                };

                if (msg != null) {
                    processMessage(msg);
                }

                Thread serverThread = new Thread(() => {
                    while (true) {
                        try {
                            using (var server = new NamedPipeServerStream("UlanziSmartVolumeOSD", PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous)) {
                                server.WaitForConnection();
                                using (var reader = new StreamReader(server)) {
                                    string line = reader.ReadLine();
                                    app.Dispatcher.Invoke(() => {
                                        processMessage(line);
                                    });
                                }
                            }
                        } catch { }
                    }
                });
                serverThread.IsBackground = true;
                serverThread.Start();
            };
            
            app.Exit += (s, e) => {
                if (mutex != null) {
                    mutex.ReleaseMutex();
                }
            };
            
            app.Run();
        }
    }
}
