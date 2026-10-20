using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ReplayWatcher
{
    internal static class Keyboard
    {
        [StructLayout(LayoutKind.Sequential)] internal struct Mouse { public int x, y; public uint data, flags, time; public UIntPtr extra; }
        [StructLayout(LayoutKind.Sequential)] internal struct Key { public ushort vk, scan; public uint flags, time; public UIntPtr extra; }
        [StructLayout(LayoutKind.Explicit)] internal struct Union { [FieldOffset(0)] public Mouse mouse; [FieldOffset(0)] public Key key; }
        [StructLayout(LayoutKind.Sequential)] internal struct Input { public uint type; public Union data; }
        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint n, Input[] inputs, int size);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder info, uint length, out uint needed);
        [DllImport("user32.dll")] private static extern bool CloseDesktop(IntPtr handle);
        internal static bool Ready(int[] keys)
        {
            foreach (int k in new[] { 16, 17, 18, 91, 92 }) if ((GetAsyncKeyState(k) & 0x8000) != 0) return false;
            foreach (int k in keys) if ((GetAsyncKeyState(k) & 0x8000) != 0) return false;
            IntPtr desktop = OpenInputDesktop(0, false, 1); if (desktop == IntPtr.Zero) return false;
            try { StringBuilder name = new StringBuilder(128); uint size; return GetUserObjectInformation(desktop, 2, name, 256, out size) && name.ToString() == "Default"; }
            finally { CloseDesktop(desktop); }
        }
        internal static bool Toggle(int[] keys)
        {
            Input[] inputs = new Input[keys.Length * 2];
            for (int i = 0; i < keys.Length; i++)
            {
                inputs[i].type = 1; inputs[i].data.key.vk = (ushort)keys[i];
                int j = inputs.Length - 1 - i; inputs[j].type = 1; inputs[j].data.key.vk = (ushort)keys[i]; inputs[j].data.key.flags = 2;
            }
            uint count = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Input)));
            if (count == inputs.Length) return true;
            Input[] releases = new Input[keys.Length];
            for (int i = 0; i < keys.Length; i++) { releases[i].type = 1; releases[i].data.key.vk = (ushort)keys[keys.Length - 1 - i]; releases[i].data.key.flags = 2; }
            SendInput((uint)releases.Length, releases, Marshal.SizeOf(typeof(Input))); return false;
        }
    }
    internal sealed class RecoveryDecision
    {
        internal bool Attempt;
        internal string Text;
    }
    internal sealed class RecoveryPolicy
    {
        private DateTime offSince = DateTime.MinValue, nextAttempt = DateTime.MinValue;
        internal RecoveryDecision Evaluate(bool? state, bool enabled, bool running, bool bound, DateTime now)
        {
            if (!state.HasValue) { offSince = DateTime.MinValue; return new RecoveryDecision { Text = L.T("Состояние неизвестно — переключение не отправляется.") }; }
            if (state.Value) { offSince = DateTime.MinValue; return new RecoveryDecision { Text = enabled ? L.T("Автовосстановление активно · проверка каждые 2 секунды") : L.T("Автовосстановление выключено") }; }
            if (!enabled) return new RecoveryDecision { Text = L.T("Автовосстановление выключено") };
            if (!running) return new RecoveryDecision { Text = L.T("Включите оверлей в NVIDIA App или GeForce Experience.") };
            if (!bound) return new RecoveryDecision { Text = L.T("В NVIDIA не назначено сочетание включения повтора.") };
            if (offSince == DateTime.MinValue) offSince = now;
            if ((now - offSince).TotalSeconds < 3) return new RecoveryDecision { Text = L.T("Повтор выключен. Готовлю восстановление…") };
            if (now < nextAttempt) return new RecoveryDecision { Text = L.T("Ожидаю подтверждения. Следующая попытка через ") + (int)Math.Ceiling((nextAttempt - now).TotalSeconds) + L.T(" сек.") };
            return new RecoveryDecision { Attempt = true, Text = L.T("Пробую включить повтор…") };
        }
        internal void Attempted(DateTime now) { nextAttempt = now.AddSeconds(10); }
    }
}
