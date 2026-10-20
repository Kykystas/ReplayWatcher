using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;

namespace ReplayWatcher
{
    internal sealed class Settings
    {
        public bool AutoRestore { get; set; }
        public bool MicrophoneMonitoring { get; set; }
        public string MicrophoneId { get; set; }
        public string Language { get; set; }
        public Settings() { AutoRestore = true; MicrophoneId = ""; Language = "ru"; }
    }

    internal static class Storage
    {
        internal static readonly string Root = GetDataRoot();
        internal static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        private static string GetDataRoot()
        {
            // The portable distribution keeps its data next to the application.
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            try { Directory.CreateDirectory(root); string probe = Path.Combine(root, ".write-test"); File.WriteAllText(probe, ""); File.Delete(probe); return root; }
            catch { root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReplayWatcher"); Directory.CreateDirectory(root); return root; }
        }
        internal static Settings Load()
        {
            try { return Json.Deserialize<Settings>(File.ReadAllText(Path.Combine(Root, "settings.json"))) ?? new Settings(); }
            catch { return new Settings(); }
        }
        internal static void Save(Settings settings)
        {
            string target = Path.Combine(Root, "settings.json"), pending = target + ".tmp";
            File.WriteAllText(pending, Json.Serialize(settings), Encoding.UTF8);
            if (File.Exists(target)) File.Replace(pending, target, null); else File.Move(pending, target);
        }
        internal static void Log(string text)
        {
            try
            {
                string path = Path.Combine(Root, "watcher.log");
                if (File.Exists(path) && new FileInfo(path).Length > 1048576)
                {
                    string previous = path + ".previous";
                    if (File.Exists(previous)) File.Delete(previous);
                    File.Move(path, previous);
                }
                File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }
    }

    internal static class Startup
    {
        private static string Link { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Replay Watcher.lnk"); } }
        internal static bool Enabled { get { return File.Exists(Link); } }
        internal static void Set(bool enabled)
        {
            if (!enabled) { if (File.Exists(Link)) File.Delete(Link); return; }
            object shell = null, link = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                dynamic automation = shell; link = automation.CreateShortcut(Link); dynamic shortcut = link;
                shortcut.TargetPath = System.Windows.Forms.Application.ExecutablePath;
                shortcut.IconLocation = System.Windows.Forms.Application.ExecutablePath + ",0";
                shortcut.Arguments = "--background";
                shortcut.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                shortcut.Description = "NVIDIA Instant Replay monitoring"; shortcut.Save();
            }
            finally { if (link != null) Marshal.FinalReleaseComObject(link); if (shell != null) Marshal.FinalReleaseComObject(shell); }
        }
    }
}
