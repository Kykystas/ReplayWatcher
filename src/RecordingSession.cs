using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
namespace ReplayWatcher
{
    internal static class CaptureEvidence
    {
        internal static string CorePath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"NVIDIA Corporation\ShadowPlay\CaptureCore.log"); } }
        internal static string Tail(string path, long maximum = 1024 * 1024)
        {
            try { using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) { stream.Position = Math.Max(0, stream.Length - maximum); using (var reader = new StreamReader(stream)) return reader.ReadToEnd(); } } catch { return ""; }
        }
        internal static bool? ParseManual(string text)
        {
            MatchCollection matches = Regex.Matches(text, @"active concurrent sessions\s*\[(?<sessions>[^\]]*)\]|SetCaptureState:[^\r\n]*?manual\[(?<manual>0[xX][0-9A-Fa-f]+|[0-9]+)\]", RegexOptions.IgnoreCase);
            if (matches.Count == 0) return null;
            Match last = matches[matches.Count - 1];
            if (last.Groups["sessions"].Success) return last.Groups["sessions"].Value.Split(',').Any(s => s.Trim() == "MR");
            string value = last.Groups["manual"].Value;
            return Convert.ToUInt64(value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value.Substring(2) : value, value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? 16 : 10) != 0;
        }
        internal static bool? ManualState()
        {
            // The latest runtime session notification, not a saved preference.
            return ParseManual(Tail(CorePath));
        }
        internal static string FindManualPath(string text)
        {
            int started = text.LastIndexOf("StartManualCapture", StringComparison.Ordinal);
            if (started < 0) return null;
            string before = text.Substring(0, started);
            MatchCollection locations = Regex.Matches(before, @"SetOutputLocation\s*:\s*(.+?),\s*bSplitFiles", RegexOptions.IgnoreCase);
            if (locations.Count == 0) return null;
            string path = locations[locations.Count - 1].Groups[1].Value.Trim();
            if (!Path.IsPathRooted(path) || path.StartsWith(@"\\") || !path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || path.IndexOf(".DVR.", StringComparison.OrdinalIgnoreCase) >= 0) return null;
            return path;
        }
    }
    internal sealed class RecordingSession
    {
        internal bool OwnsRecording { get; private set; }
        internal string VideoPath { get; private set; }
        internal DateTime StartedAt { get; private set; }
        private int[] keys;
        private DateTime requestedAt;
        internal async Task Start()
        {
            if (OwnsRecording) throw new InvalidOperationException(L.T("Пробная запись уже идёт."));
            NvidiaSnapshot nvidia = Nvidia.Discover();
            if (!nvidia.Running) throw new InvalidOperationException(L.T("Включите оверлей NVIDIA."));
            bool? current = CaptureEvidence.ManualState();
            if (current == true) throw new InvalidOperationException(L.T("Уже идёт другая запись NVIDIA. Сначала остановите её — приложение не будет её прерывать."));
            if (!current.HasValue) throw new InvalidOperationException(L.T("NVIDIA не предоставила состояние записи. Проверьте, что оверлей и Instant Replay запущены."));
            keys = Nvidia.ReadHotkey("ManualHKey");
            if (keys == null) throw new InvalidOperationException(L.T("В NVIDIA не назначено сочетание для обычной записи."));
            if (!Keyboard.Ready(keys)) throw new InvalidOperationException(L.T("Отпустите служебные клавиши и повторите попытку."));
            if (CaptureEvidence.ManualState() != false) throw new InvalidOperationException(L.T("Состояние записи изменилось. Повторите проверку."));
            requestedAt = DateTime.UtcNow;
            if (!Keyboard.Toggle(keys)) throw new InvalidOperationException(L.T("Windows не приняла команду записи."));
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(200);
                if (CaptureEvidence.ManualState() == true)
                {
                    OwnsRecording = true; StartedAt = DateTime.UtcNow;
                    string evidence = CaptureEvidence.Tail(CaptureEvidence.CorePath);
                    VideoPath = CaptureEvidence.FindManualPath(evidence);
                    if (VideoPath != null && File.Exists(VideoPath) && File.GetCreationTimeUtc(VideoPath) < requestedAt.AddSeconds(-2)) VideoPath = null;
                    Storage.Log(L.T("Пробная запись NVIDIA запущена")); return;
                }
            }
            throw new InvalidOperationException(L.T("NVIDIA не подтвердила запуск. Возможно, захват заблокирован защищённым содержимым."));
        }
        internal async Task<string> Stop()
        {
            if (!OwnsRecording) return VideoPath;
            bool? current = CaptureEvidence.ManualState();
            if (!current.HasValue) throw new InvalidOperationException(L.T("Состояние записи недоступно. Остановите запись через оверлей NVIDIA."));
            if (current.Value)
            {
                string activePath = CaptureEvidence.FindManualPath(CaptureEvidence.Tail(CaptureEvidence.CorePath));
                if (VideoPath != null && activePath != null && !String.Equals(Path.GetFullPath(VideoPath), Path.GetFullPath(activePath), StringComparison.OrdinalIgnoreCase))
                {
                    OwnsRecording = false;
                    throw new InvalidOperationException(L.T("Пробная запись уже завершена, а сейчас идёт другая запись NVIDIA. Её приложение не остановит."));
                }
                if (!Keyboard.Ready(keys)) throw new InvalidOperationException(L.T("Отпустите служебные клавиши и нажмите «Остановить» снова."));
                if (CaptureEvidence.ManualState() == true && !Keyboard.Toggle(keys)) throw new InvalidOperationException(L.T("Windows не приняла команду остановки."));
            }
            for (int i = 0; i < 40; i++)
            {
                await Task.Delay(200);
                if (CaptureEvidence.ManualState() == false) { OwnsRecording = false; break; }
            }
            if (OwnsRecording) throw new InvalidOperationException(L.T("NVIDIA пока не подтвердила остановку. Проверьте оверлей."));
            Storage.Log(L.T("Пробная запись NVIDIA остановлена"));
            if (VideoPath == null)
            {
                string candidate = CaptureEvidence.FindManualPath(CaptureEvidence.Tail(CaptureEvidence.CorePath));
                if (candidate != null && File.Exists(candidate) && File.GetCreationTimeUtc(candidate) >= requestedAt.AddSeconds(-2)) VideoPath = candidate;
            }
            if (VideoPath == null) throw new IOException(L.T("Запись завершена, но путь к ролику не получен. Его можно найти в галерее NVIDIA."));
            long previous = -1;
            for (int i = 0; i < 50; i++)
            {
                await Task.Delay(200);
                try { var file = new FileInfo(VideoPath); if (file.Exists && file.Length > 0 && file.Length == previous) { using (File.Open(VideoPath, FileMode.Open, FileAccess.Read, FileShare.Read)) return VideoPath; } previous = file.Exists ? file.Length : -1; } catch (IOException) { }
            }
            throw new IOException(L.T("Ролик ещё сохраняется. Откройте его позже в галерее NVIDIA."));
        }
    }
}
