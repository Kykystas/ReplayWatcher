using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using NAudio.Wave;

namespace ReplayWatcher
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if (args.Contains("--recording-smoke-test")) return RecordingSmokeTest.Run();
                if (args.Contains("--self-test")) return SelfTests.Run();
                if (args.Contains("--diagnose"))
                {
                    NvidiaSnapshot nv = Nvidia.Discover();
                    var diagnosis = new Dictionary<string, object> { { "overlayFound", nv.OverlayPath != null }, { "overlayPath", nv.OverlayPath }, { "overlayRunning", nv.Running }, { "liveReplay", nv.Replay }, { "hotkey", Nvidia.ShortcutText(nv.Hotkey) }, { "configFound", nv.ConfigPath != null }, { "microphoneMode", nv.MicMode } };
                    if (args.Contains("--microphone"))
                        using (MicrophoneMonitor monitor = new MicrophoneMonitor())
                        {
                            MicSnapshot mic = monitor.Poll("", nv.MicMode);
                            diagnosis["microphone"] = new { device = mic.Device, text = mic.Text, peak = mic.Peak, nvidiaSessionActive = mic.NvidiaActive, nvidiaSessionPeak = mic.NvidiaPeak };
                        }
                    File.WriteAllText(Path.Combine(Storage.Root, "diagnostics.json"), Storage.Json.Serialize(diagnosis)); return 0;
                }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                if (args.Contains("--preview"))
                {
                    using (MainForm form = new MainForm(new Settings(), null, false, true)) form.RenderPreview(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, args.Contains("--microphone") ? "preview-microphone.png" : "preview.png"), args.Contains("--microphone"));
                    using (var dialog = new RecordingTestDialog(true)) dialog.RenderPreview(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "preview-recording.png"));
                    return 0;
                }
                bool created;
                using (Mutex instance = new Mutex(true, @"Local\ReplayWatcher.Application.v2", out created))
                using (EventWaitHandle signal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\ReplayWatcher.ShowWindow.v2"))
                {
                    if (!created) { if (!args.Contains("--background")) signal.Set(); return 0; }
                    Application.Run(new MainForm(Storage.Load(), signal, args.Contains("--background"), false));
                }
                return 0;
            }
            catch (Exception ex)
            {
                Storage.Log("Ошибка запуска: " + ex.Message);
                if (!args.Contains("--background")) MessageBox.Show("Не удалось запустить Replay Watcher.\n" + ex.Message, "Replay Watcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }
    internal static class SelfTests
    {
        private static List<string> passed = new List<string>();
        private static void Assert(bool condition, string test) { if (!condition) throw new InvalidOperationException("FAIL: " + test); passed.Add("PASS: " + test); }
        internal static int Run()
        {
            try
            {
                Assert(Nvidia.Decode(new byte[] { 0, 0, 0, 0 }) == false && Nvidia.Decode(new byte[] { 1, 0, 0, 0 }) == true, "live NVIDIA state decoding");
                Assert(!Nvidia.Decode(null).HasValue && !Nvidia.Decode(new byte[] { 2, 0, 0, 0 }).HasValue && !Nvidia.Decode(new byte[] { 1 }).HasValue, "unknown state never becomes OFF");
                Assert(!Nvidia.ValidHotkey(new[] { 18, 18, 121 }) && !Nvidia.ValidHotkey(new[] { 18, 16 }) && Nvidia.ValidHotkey(new[] { 18, 16, 121 }), "invalid and modifier-only shortcuts rejected");
                string customApp = @"D:\Apps\NVIDIA App\CEF\NVIDIA Overlay.exe";
                Assert(Nvidia.FindOverlay(Nvidia.OverlayCandidates(@"D:\Apps\NVIDIA App"), p => p == customApp) == customApp, "NVIDIA App discovery on a custom drive");
                string legacyApp = @"E:\Программы\GeForce Experience\NVIDIA Share.exe";
                Assert(Nvidia.FindOverlay(Nvidia.OverlayCandidates(@"E:\Программы\GeForce Experience"), p => p == legacyApp) == legacyApp, "GeForce Experience discovery with Unicode installation path");
                Assert(!Nvidia.OverlayCandidates(null).Any() && !Nvidia.OverlayCandidates("relative").Any() && !Nvidia.OverlayCandidates("Z:\\bad\0path").Any(), "invalid installer paths are skipped");
                Assert(Nvidia.FindOverlay(Nvidia.OverlayCandidates(@"D:\Missing"), p => false) == null, "missing overlay is not reported as installed");
                Assert(Marshal.SizeOf(typeof(Keyboard.Input)) == (IntPtr.Size == 8 ? 40 : 28), "Windows input layout");
                RecoveryPolicy policy = new RecoveryPolicy(); DateTime time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                Assert(!policy.Evaluate(null, true, true, true, time).Attempt && !policy.Evaluate(true, true, true, true, time).Attempt, "never toggle ON or unknown state");
                Assert(!policy.Evaluate(false, true, true, true, time).Attempt && policy.Evaluate(false, true, true, true, time.AddSeconds(4)).Attempt, "OFF debounce then recovery");
                policy.Attempted(time.AddSeconds(4));
                Assert(!policy.Evaluate(false, true, true, true, time.AddSeconds(8)).Attempt && policy.Evaluate(false, true, true, true, time.AddSeconds(14)).Attempt, "retry cooldown");
                Assert(!policy.Evaluate(false, false, true, true, time.AddSeconds(20)).Attempt && !policy.Evaluate(false, true, false, true, time.AddSeconds(20)).Attempt && !policy.Evaluate(false, true, true, false, time.AddSeconds(20)).Attempt, "pause / unavailable overlay / unbound shortcut");
                policy.Evaluate(true, true, true, true, time.AddSeconds(21));
                Assert(!policy.Evaluate(false, true, true, true, time.AddSeconds(22)).Attempt, "new OFF transition is debounced again");
                Settings settings = new Settings();
                Assert(!settings.MicrophoneMonitoring, "microphone monitoring defaults OFF");
                settings.MicrophoneId = "test-device"; settings.MicrophoneMonitoring = true;
                Settings roundtrip = Storage.Json.Deserialize<Settings>(Storage.Json.Serialize(settings));
                Assert(roundtrip.MicrophoneId == "test-device" && roundtrip.MicrophoneMonitoring, "settings serialization");
                Assert(CaptureEvidence.ParseManual("active concurrent sessions [MR, IR, ]") == true && CaptureEvidence.ParseManual("active concurrent sessions [MR, IR, ]\nactive concurrent sessions [IR, ]") == false, "manual recording uses latest runtime session state");
                Assert(!CaptureEvidence.ParseManual("no session evidence").HasValue, "unknown manual recording state is not idle");
                Assert(CaptureEvidence.ParseManual("active concurrent sessions [MR, IR, ]\nCCaptureSession::SetCaptureState: dvr[0X1], manual[0], broadcast[0]") == false, "manual stop overrides older active-session notification");
                Assert(CaptureEvidence.ParseManual("SetCaptureState: manual[0]\nSetCaptureState: dvr[0X1], manual[0X1], hl[0]") == true, "manual state transition to recording");
                Assert(CaptureEvidence.FindManualPath("SetOutputLocation :C:\\Clips\\test.mp4, bSplitFiles(0)\nStartManualCapture") == @"C:\Clips\test.mp4", "manual output file matched to recording start");
                Assert(CaptureEvidence.FindManualPath("SetOutputLocation :C:\\Clips\\test.DVR.mp4, bSplitFiles(0)\nStartManualCapture") == null, "instant replay file cannot be selected as manual test recording");
                TestTrayIcons();
                TestAudio();
                File.WriteAllLines(Path.Combine(Storage.Root, "self-test.txt"), passed.ToArray()); return 0;
            }
            catch (Exception ex) { passed.Add(ex.ToString()); File.WriteAllLines(Path.Combine(Storage.Root, "self-test.txt"), passed.ToArray()); return 1; }
        }
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DrawIconEx(IntPtr dc, int x, int y, IntPtr icon, int width, int height, uint step, IntPtr brush, uint flags);
        private static void TestTrayIcons()
        {
            using (var icons = new TrayIcons())
            {
                Icon[] states = { icons.On, icons.Off, icons.Unknown };
                for (int state = 0; state < states.Length; state++)
                    using (var bitmap = new Bitmap(16, 16))
                    {
                        using (var graphics = Graphics.FromImage(bitmap))
                        {
                            graphics.Clear(Color.FromArgb(35, 35, 35));
                            IntPtr dc = graphics.GetHdc();
                            try { if (!DrawIconEx(dc, 0, 0, states[state].Handle, 16, 16, 0, IntPtr.Zero, 3)) throw new InvalidOperationException("Windows could not draw tray icon."); }
                            finally { graphics.ReleaseHdc(dc); }
                        }
                        int colored = 0, corrupt = 0;
                        for (int y = 0; y < 16; y++)
                            for (int x = 0; x < 16; x++)
                            {
                                Color pixel = bitmap.GetPixel(x, y);
                                if (state == 0) { if (pixel.G > pixel.R + 32) colored++; if (pixel.R > pixel.G + 32 || pixel.B > pixel.G + 32) corrupt++; }
                                else if (state == 1) { if (pixel.R > pixel.G + 32) colored++; if (pixel.G > pixel.R + 32 || pixel.B > pixel.R + 32) corrupt++; }
                                else { if (pixel.R > 60) colored++; if (pixel.R != pixel.G || pixel.G != pixel.B) corrupt++; }
                            }
                        Assert(colored > 0 && corrupt == 0, "Windows native tray rendering: " + (state == 0 ? "green" : state == 1 ? "red" : "gray") + " palette without corruption");
                    }
            }
        }
        private static void TestAudio()
        {
            // Synthetic audio only. Tests do not listen to the microphone or toggle NVIDIA.
            string root = Path.Combine(Path.GetTempPath(), "ReplayWatcherTests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                string tone = Path.Combine(root, "tone.wav"), silence = Path.Combine(root, "silence.wav");
                foreach (string file in new[] { tone, silence })
                    using (WaveFileWriter writer = new WaveFileWriter(file, new WaveFormat(48000, 16, 1)))
                        for (int i = 0; i < 48000; i++) writer.WriteSample(file == tone ? (float)(0.25 * Math.Sin(i * 2 * Math.PI * 440 / 48000)) : 0);
                List<AudioTrack> tracks = ClipAnalysis.Read(tone, CancellationToken.None);
                Assert(tracks.Count == 1 && tracks[0].Error == null && tracks[0].HasSignal && tracks[0].Seconds > 0.95, "saved clip with synthetic tone contains audio");
                string playback = Path.Combine(root, "playback.wav");
                ClipAnalysis.Extract(tone, tracks[0], playback, CancellationToken.None);
                using (var audio = new WaveFileReader(playback)) Assert(audio.Length > 90000 && audio.WaveFormat.SampleRate == 48000, "selected saved track decodes to playable WAV");
                File.Delete(playback);
                tracks = ClipAnalysis.Read(silence, CancellationToken.None);
                Assert(tracks.Count == 1 && tracks[0].Error == null && !tracks[0].HasSignal, "silent saved clip is not reported as audio");
                bool cancelled = false; using (CancellationTokenSource stop = new CancellationTokenSource()) { stop.Cancel(); try { ClipAnalysis.Read(tone, stop.Token); } catch (OperationCanceledException) { cancelled = true; } }
                Assert(cancelled, "clip analysis cancellation");
            }
            finally { foreach (string path in new[] { Path.Combine(root, "tone.wav"), Path.Combine(root, "silence.wav") }) if (File.Exists(path)) File.Delete(path); Directory.Delete(root); }
        }
    }
}
