using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace ReplayWatcher
{
    internal sealed class MicChoice
    {
        internal string Id, Name;
        public override string ToString() { return Name; }
    }
    internal sealed class MicSnapshot
    {
        internal float Peak, NvidiaPeak;
        internal string Device, Text, Detail;
        internal bool NvidiaActive, Muted, Problem, DeviceAvailable;
    }
    internal sealed class MicrophoneMonitor : IDisposable
    {
        private MMDeviceEnumerator enumerator;
        private List<MMDevice> devices = new List<MMDevice>();
        private List<AudioSessionControl> sessions = new List<AudioSessionControl>();
        private MMDevice selected;
        private DateTime refreshed = DateTime.MinValue, lastSignal = DateTime.MinValue;
        private string selection = null;
        internal List<MicChoice> Choices = new List<MicChoice>();
        private void ReleaseDevices()
        {
            foreach (AudioSessionControl session in sessions) session.Dispose(); sessions.Clear();
            foreach (MMDevice device in devices) device.Dispose(); devices.Clear(); selected = null;
        }
        private bool HasNvidiaSession(MMDevice device)
        {
            try
            {
                SessionCollection collection = device.AudioSessionManager.Sessions;
                for (int i = 0; i < collection.Count; i++)
                    using (AudioSessionControl session = collection[i])
                        if (session.State == AudioSessionState.AudioSessionStateActive && Nvidia.IsCaptureProcess((int)session.GetProcessID)) return true;
            }
            catch { }
            return false;
        }
        private void Refresh(string id)
        {
            ReleaseDevices();
            if (enumerator == null) enumerator = new MMDeviceEnumerator();
            foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)) devices.Add(device);
            Choices = devices.Select(d => new MicChoice { Id = d.ID, Name = d.FriendlyName }).ToList();
            if (!String.IsNullOrEmpty(id)) selected = devices.FirstOrDefault(d => d.ID == id);
            else
            {
                selected = devices.FirstOrDefault(HasNvidiaSession);
                if (selected == null)
                {
                    string defaultId = null;
                    try { using (MMDevice defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications)) defaultId = defaultDevice.ID; } catch { }
                    selected = devices.FirstOrDefault(d => d.ID == defaultId) ?? devices.FirstOrDefault();
                }
            }
            if (selected != null)
            {
                try
                {
                    SessionCollection collection = selected.AudioSessionManager.Sessions;
                    for (int i = 0; i < collection.Count; i++)
                    {
                        AudioSessionControl session = collection[i];
                        if (Nvidia.IsCaptureProcess((int)session.GetProcessID)) sessions.Add(session); else session.Dispose();
                    }
                }
                catch { }
            }
            if (selection != id) lastSignal = DateTime.MinValue;
            selection = id; refreshed = DateTime.UtcNow;
        }
        internal MicSnapshot Poll(string id, string micMode)
        {
            try
            {
                if ((DateTime.UtcNow - refreshed).TotalSeconds > 3 || selection != id) Refresh(id);
                if (selected == null) return new MicSnapshot { Problem = true, Device = L.T("Микрофон не найден"), Text = L.T("Подключите микрофон или выберите другое устройство."), Detail = L.T("Проверка не открывает запись и не сохраняет звук.") };
                MicSnapshot result = new MicSnapshot { DeviceAvailable = true, Device = selected.FriendlyName, Peak = selected.AudioMeterInformation.MasterPeakValue, Muted = selected.AudioEndpointVolume.Mute || selected.AudioEndpointVolume.MasterVolumeLevelScalar <= 0.001f };
                foreach (AudioSessionControl session in sessions)
                    try
                    {
                        if (session.State != AudioSessionState.AudioSessionStateActive) continue;
                        result.NvidiaActive = true;
                        if (!session.SimpleAudioVolume.Mute && session.SimpleAudioVolume.Volume > 0.001f) result.NvidiaPeak = Math.Max(result.NvidiaPeak, session.AudioMeterInformation.MasterPeakValue);
                    }
                    catch { }
                if (result.Peak > 0.0035f) lastSignal = DateTime.UtcNow;
                bool recent = lastSignal != DateTime.MinValue && (DateTime.UtcNow - lastSignal).TotalSeconds < 5;
                if (result.Muted) { result.Text = L.T("Микрофон отключён или громкость равна нулю"); result.Problem = true; }
                else if (micMode == "off") { result.Text = L.T("В настройках NVIDIA микрофон выключен"); result.Problem = true; }
                else if (result.NvidiaPeak > 0.0035f) result.Text = L.T("В аудиосеансе NVIDIA есть сигнал");
                else if (result.NvidiaActive) result.Text = recent ? L.T("Микрофон слышен · сеанс NVIDIA активен") : L.T("Сеанс NVIDIA активен · сейчас тихо");
                else result.Text = recent ? L.T("Микрофон слышен · активный сеанс NVIDIA не найден") : L.T("Сейчас тихо · активный сеанс NVIDIA не найден");
                result.Detail = micMode == "ptt" ? L.T("В NVIDIA выбран режим «Нажми и говори». Удерживайте свою клавишу для проверки.") : L.T("Индикатор показывает входящий сигнал. Чтобы проверить звук в видео, нажмите «Проверить клип».");
                return result;
            }
            catch (Exception ex)
            {
                refreshed = DateTime.MinValue;
                return new MicSnapshot { Problem = true, Device = L.T("Микрофон недоступен"), Text = L.T("Не удалось прочитать состояние микрофона"), Detail = L.T("Проверьте подключение и разрешения Windows. ") + ex.Message };
            }
        }
        public void Dispose()
        {
            ReleaseDevices(); if (enumerator != null) enumerator.Dispose(); enumerator = null; refreshed = DateTime.MinValue;
        }
    }
}
