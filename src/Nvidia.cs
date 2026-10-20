using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace ReplayWatcher
{
    internal sealed class NvidiaSnapshot
    {
        internal string OverlayPath, ConfigPath, MicMode, CaptureDirectory;
        internal bool Running;
        internal bool? Replay;
        internal int[] Hotkey;
        internal string Description { get { return OverlayPath == null ? "NVIDIA Overlay не найден" : Path.GetDirectoryName(OverlayPath); } }
    }

    internal static class Nvidia
    {
        internal const string RegistryPath = @"Software\NVIDIA Corporation\Global\ShadowPlay\NVSPCAPS";
        internal const string LiveFlag = "{1B1D3DAA-601D-49E5-8508-81736CA28C6D}";
        internal static bool? Decode(object value)
        {
            byte[] bytes = value as byte[];
            if (bytes == null || bytes.Length != 4) return null;
            int state = BitConverter.ToInt32(bytes, 0);
            return state == 0 ? false : state == 1 ? true : (bool?)null;
        }
        internal static bool? ReadLive()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath)) return key == null ? null : Decode(key.GetValue(LiveFlag));
        }
        private static int ReadNumber(RegistryKey key, string name)
        {
            object value = key.GetValue(name);
            byte[] bytes = value as byte[];
            return bytes != null && bytes.Length == 4 ? BitConverter.ToInt32(bytes, 0) : value is int ? (int)value : -1;
        }
        internal static bool ValidHotkey(int[] keys)
        {
            return keys != null && keys.Length >= 2 && keys.Length <= 5 && keys.All(k => k > 0 && k < 255)
                && keys.Distinct().Count() == keys.Length && keys.Any(k => k == 16 || k == 17 || k == 18)
                && keys.Any(k => k != 16 && k != 17 && k != 18);
        }
        internal static int[] ReadHotkey(string prefix = "IRToggleHKey")
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
            {
                if (key == null) return null;
                int count = ReadNumber(key, prefix + "Count");
                if (count < 2 || count > 5) return null;
                int[] keys = Enumerable.Range(0, count).Select(i => ReadNumber(key, prefix + i)).ToArray();
                return ValidHotkey(keys) ? keys : null;
            }
        }
        internal static string ShortcutText(int[] keys)
        {
            if (keys == null) return "Не назначено";
            return String.Join(" + ", keys.Select(k => k == 16 ? "Shift" : k == 17 ? "Ctrl" : k == 18 ? "Alt" : ((System.Windows.Forms.Keys)k).ToString()));
        }
        internal static bool IsCaptureProcess(int id)
        {
            try { using (Process process = Process.GetProcessById(id)) return new[] { "nvcontainer", "nvsphelper64", "NVIDIA Share", "NVIDIA Overlay" }.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase); }
            catch { return false; }
        }
        internal static IEnumerable<string> OverlayCandidates(string location)
        {
            if (String.IsNullOrWhiteSpace(location)) return new string[0];
            if (location.IndexOf('\0') >= 0) return new string[0];
            try
            {
                location = Environment.ExpandEnvironmentVariables(location.Trim().Trim('"'));
                if (!Path.IsPathRooted(location)) return new string[0];
                return new[] {
                    Path.Combine(location, @"CEF\NVIDIA Overlay.exe"),
                    Path.Combine(location, "NVIDIA Overlay.exe"),
                    Path.Combine(location, "NVIDIA Share.exe"),
                    Path.Combine(location, @"ShadowPlay\NVIDIA Share.exe")
                };
            }
            catch (ArgumentException) { return new string[0]; }
            catch (NotSupportedException) { return new string[0]; }
        }
        internal static string FindOverlay(IEnumerable<string> candidates, Func<string, bool> exists)
        {
            return candidates.Distinct(StringComparer.OrdinalIgnoreCase).FirstOrDefault(exists);
        }
        internal static NvidiaSnapshot Discover()
        {
            NvidiaSnapshot result = new NvidiaSnapshot { Replay = ReadLive(), Hotkey = ReadHotkey(), MicMode = "unknown" };
            string[] processNames = { "NVIDIA Overlay", "NVIDIA Share" };
            foreach (string name in processNames)
            {
                foreach (Process process in Process.GetProcessesByName(name))
                {
                    using (process)
                    {
                        result.Running = true;
                        try { string path = process.MainModule.FileName; if (File.Exists(path)) result.OverlayPath = path; } catch { }
                    }
                }
            }
            List<string> candidates = new List<string>();
            foreach (string folder in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) }.Distinct())
            {
                if (String.IsNullOrWhiteSpace(folder)) continue;
                candidates.AddRange(OverlayCandidates(Path.Combine(folder, @"NVIDIA Corporation\NVIDIA App")));
                candidates.AddRange(OverlayCandidates(Path.Combine(folder, @"NVIDIA Corporation\NVIDIA GeForce Experience")));
            }
            // Custom installation paths can be discovered through Windows' uninstall inventory.
            if (result.OverlayPath == null)
                foreach (RegistryHive hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
                foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                    try
                    {
                        using (RegistryKey machine = RegistryKey.OpenBaseKey(hive, view))
                        using (RegistryKey uninstall = machine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                        {
                            if (uninstall == null) continue;
                            foreach (string sub in uninstall.GetSubKeyNames())
                                try
                                {
                                using (RegistryKey entry = uninstall.OpenSubKey(sub))
                                {
                                    if (entry == null) continue;
                                    string name = Convert.ToString(entry.GetValue("DisplayName"));
                                    if (name.IndexOf("NVIDIA App", StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("GeForce Experience", StringComparison.OrdinalIgnoreCase) < 0) continue;
                                    string location = Convert.ToString(entry.GetValue("InstallLocation"));
                                    candidates.AddRange(OverlayCandidates(location));
                                }
                                }
                                catch { /* An inaccessible installer entry must not hide other installations. */ }
                        }
                    }
                    catch { }
            if (result.OverlayPath == null) result.OverlayPath = FindOverlay(candidates, File.Exists);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            result.ConfigPath = new[] { Path.Combine(local, @"NVIDIA Corporation\NVIDIA Overlay\ShareSettings.json"), Path.Combine(local, @"NVIDIA Corporation\NVIDIA Share\ShareSettings.json") }.FirstOrDefault(File.Exists);
            if (result.ConfigPath != null)
                try
                {
                    var document = Storage.Json.DeserializeObject(File.ReadAllText(result.ConfigPath)) as Dictionary<string, object>;
                    var settings = document["settings"] as Dictionary<string, object>;
                    var mic = settings["micmode"] as Dictionary<string, object>;
                    result.MicMode = Convert.ToString(mic["mode"]);
                }
                catch { }
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                if (key != null)
                {
                    object value = key.GetValue("DefaultPathW"); byte[] bytes = value as byte[];
                    result.CaptureDirectory = bytes == null ? value as string : Encoding.Unicode.GetString(bytes).TrimEnd('\0');
                }
            return result;
        }
    }
}
