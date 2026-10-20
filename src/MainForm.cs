using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ReplayWatcher
{
    internal sealed class MainForm : Form
    {
        private readonly Settings settings;
        private readonly RecoveryPolicy recovery = new RecoveryPolicy();
        private readonly EventWaitHandle showSignal;
        private readonly NotifyIcon tray;
        private readonly TrayIcons trayIcons = new TrayIcons();
        private readonly System.Windows.Forms.Timer timer;
        private readonly bool background, preview;
        private Label replayTitle, replayDetail, nvidiaStatus, micTitle, nvidiaMicTitle, micLamp, nvidiaMicLamp, micDetail, micDevice, lastChecked;
        private CheckBox autoRestore, micEnabled, startup;
        private ComboBox microphones;
        private LevelMeter meter;
        private Button checkClip;
        private ListBox activity;
        private MicrophoneMonitor monitor;
        private NvidiaSnapshot nvidia;
        private DateTime discoveryAt = DateTime.MinValue, recoveryAt = DateTime.MinValue;
        private DateTime manualNoticeUntil = DateTime.MinValue;
        private bool lastTickSucceeded;
        private bool? lastState;
        private bool closing, refreshingChoices, recordingTestOpen;
        internal MainForm(Settings configuration, EventWaitHandle signal, bool startBackground, bool renderPreview)
        {
            settings = configuration; showSignal = signal; background = startBackground; preview = renderPreview;
            Text = "Replay Watcher"; ClientSize = new Size(760, 832); MinimumSize = new Size(760, 862);
            StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 10); BackColor = Theme.Background; ForeColor = Theme.Text; Theme.DarkTitle(this);
            AutoScaleMode = AutoScaleMode.Dpi;
            BuildInterface();
            ContextMenuStrip menu = new ContextMenuStrip { BackColor = Theme.Surface, ForeColor = Theme.Text };
            menu.Items.Add("Открыть", null, delegate { ShowWindow(); });
            menu.Items.Add("Выйти", null, delegate { Quit(); });
            tray = new NotifyIcon { Text = "Replay Watcher: проверяю состояние", Icon = trayIcons.Unknown, ContextMenuStrip = menu, Visible = !preview };
            tray.DoubleClick += delegate { ShowWindow(); };
            timer = new System.Windows.Forms.Timer { Interval = 200 }; timer.Tick += Tick;
            Shown += delegate
            {
                if (preview) return;
                if (background) { Hide(); ShowInTaskbar = false; }
                timer.Start(); Log("Replay Watcher 0.3.8 запущен"); Tick(null, EventArgs.Empty);
            };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (e.CloseReason == CloseReason.UserClosing && !closing && !preview) { e.Cancel = true; Hide(); ShowInTaskbar = false; }
            };
            FormClosed += delegate { timer.Stop(); if (monitor != null) monitor.Dispose(); tray.Visible = false; tray.Dispose(); trayIcons.Dispose(); };
        }
        private static Label LabelOf(string text, float size, Color color, bool bold)
        {
            return new Label { Text = text, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = color, Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
        }
        private static Button ButtonOf(string text, EventHandler action)
        {
            return Theme.Button(text, action);
        }
        private void BuildInterface()
        {
            Color ink = Theme.Text, muted = Theme.Muted;
            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24, 14, 24, 14), ColumnCount = 1, RowCount = 9 };
            foreach (int height in new[] { 44, 30, 104, 44, 32, 312, 38, 52 }) root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); Controls.Add(root);
            root.Controls.Add(LabelOf("Replay Watcher", 24, Theme.Accent, true), 0, 0);
            root.Controls.Add(LabelOf("Автоматическое восстановление NVIDIA Instant Replay", 10, muted, false), 0, 1);
            TableLayoutPanel status = new TableLayoutPanel { BackColor = Theme.Surface, Dock = DockStyle.Fill, Padding = new Padding(18, 8, 18, 8), RowCount = 2, ColumnCount = 1, Margin = new Padding(0, 8, 0, 6) };
            status.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); status.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            replayTitle = LabelOf("Проверяю NVIDIA…", 20, ink, true); replayDetail = LabelOf("Состояние появится автоматически", 10, muted, false);
            status.Controls.Add(replayTitle); status.Controls.Add(replayDetail); root.Controls.Add(status, 0, 2);
            autoRestore = new CheckBox { Text = "Автоматически включать повтор, если он отключился", Checked = settings.AutoRestore, Dock = DockStyle.Fill, AutoSize = true };
            autoRestore.CheckedChanged += delegate { settings.AutoRestore = autoRestore.Checked; Save(); Log(settings.AutoRestore ? "Автовосстановление включено" : "Автовосстановление выключено"); recoveryAt = DateTime.MinValue; };
            root.Controls.Add(autoRestore, 0, 3);
            nvidiaStatus = LabelOf("Поиск NVIDIA Overlay…", 9, muted, false); root.Controls.Add(nvidiaStatus, 0, 4);
            GroupBox micBox = new GroupBox { Text = "Микрофон", Dock = DockStyle.Fill, Padding = new Padding(14, 22, 14, 10), Margin = new Padding(0), BackColor = Theme.Surface, ForeColor = Theme.Text };
            TableLayoutPanel mic = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7 };
            foreach (int height in new[] { 32, 36, 22, 24, 34, 42 }) mic.RowStyles.Add(new RowStyle(SizeType.Absolute, height)); mic.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            micEnabled = new CheckBox { Text = "Проверять микрофон", Checked = settings.MicrophoneMonitoring, Dock = DockStyle.Fill, AutoSize = true };
            micEnabled.CheckedChanged += delegate
            {
                settings.MicrophoneMonitoring = micEnabled.Checked; Save();
                microphones.Enabled = micEnabled.Checked;
                if (!micEnabled.Checked) { if (monitor != null) monitor.Dispose(); monitor = null; meter.Value = 0; SetMicOff(); }
                Log(micEnabled.Checked ? "Проверка микрофона включена" : "Проверка микрофона выключена");
            };
            microphones = new DarkComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Enabled = settings.MicrophoneMonitoring };
            microphones.Items.Add(new MicChoice { Id = "", Name = "Авто — микрофон NVIDIA / устройство Windows по умолчанию" }); microphones.SelectedIndex = 0;
            microphones.SelectedIndexChanged += delegate { if (!refreshingChoices && microphones.SelectedItem is MicChoice) { settings.MicrophoneId = ((MicChoice)microphones.SelectedItem).Id; Save(); } };
            meter = new LevelMeter { Dock = DockStyle.Fill, Maximum = 100, Margin = new Padding(0, 4, 0, 6) };
            micDevice = LabelOf("", 9, muted, false); micDetail = LabelOf("", 9, muted, false);
            TableLayoutPanel signalRows = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Margin = new Padding(0) };
            signalRows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 26));
            signalRows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            signalRows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            signalRows.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); signalRows.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            micLamp = LabelOf("●", 15, muted, false); nvidiaMicLamp = LabelOf("●", 15, muted, false);
            micLamp.AccessibleName = "Индикатор сигнала микрофона"; nvidiaMicLamp.AccessibleName = "Индикатор сигнала NVIDIA";
            micTitle = LabelOf("", 10, ink, true); nvidiaMicTitle = LabelOf("", 10, ink, true);
            signalRows.Controls.Add(micLamp, 0, 0); signalRows.Controls.Add(LabelOf("Сигнал микрофона", 10, ink, false), 1, 0); signalRows.Controls.Add(micTitle, 2, 0);
            signalRows.Controls.Add(nvidiaMicLamp, 0, 1); signalRows.Controls.Add(LabelOf("Сигнал в NVIDIA", 10, ink, false), 1, 1); signalRows.Controls.Add(nvidiaMicTitle, 2, 1);
            mic.RowStyles[4].Height = 66;
            checkClip = ButtonOf("●  Записать и прослушать проверку…", delegate { CheckClip(); });
            checkClip.AutoSize = false; checkClip.Dock = DockStyle.Fill; checkClip.Margin = new Padding(0, 2, 0, 0);
            mic.Controls.Add(micEnabled, 0, 0); mic.Controls.Add(microphones, 0, 1); mic.Controls.Add(meter, 0, 2); mic.Controls.Add(micDevice, 0, 3); mic.Controls.Add(signalRows, 0, 4); mic.Controls.Add(micDetail, 0, 5); mic.Controls.Add(checkClip, 0, 6); micBox.Controls.Add(mic); root.Controls.Add(micBox, 0, 5);
            startup = new CheckBox { Text = "Запускать вместе с Windows", Dock = DockStyle.Fill, AutoSize = true, Checked = !preview && Startup.Enabled };
            startup.CheckedChanged += delegate { if (preview) return; try { Startup.Set(startup.Checked); } catch (Exception ex) { MessageBox.Show(this, "Не удалось изменить автозапуск: " + ex.Message); } };
            root.Controls.Add(startup, 0, 6);
            FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0), WrapContents = false };
            buttons.Controls.Add(ButtonOf("Журнал", delegate { ShowLog(); })); buttons.Controls.Add(ButtonOf("Диагностика", delegate { ShowDiagnostics(); }));
            buttons.Controls.Add(ButtonOf("Проверить сейчас", delegate { RefreshNow(); }));
            buttons.Controls.Add(ButtonOf("Выйти", delegate { Quit(); })); root.Controls.Add(buttons, 0, 7);
            lastChecked = LabelOf("Ожидание проверки…\nЗакрытие окна сворачивает приложение в трей.  ·  v0.3.8\nMade by Kykystas with the help of AI", 9, muted, false); root.Controls.Add(lastChecked, 0, 8);
            foreach (var box in new[] { autoRestore, micEnabled, startup }) { box.FlatStyle = FlatStyle.Flat; box.FlatAppearance.CheckedBackColor = Theme.Accent; box.FlatAppearance.BorderColor = Theme.Border; }
            SetMicOff();
        }
        private void SetMicOff()
        {
            micDevice.Text = "Мониторинг выключен";
            SetSignal(micLamp, micTitle, "Проверка выключена", false, false);
            SetSignal(nvidiaMicLamp, nvidiaMicTitle, "Проверка выключена", false, false);
            micDetail.Text = "Включите проверку, чтобы видеть уровень звука и сеанс NVIDIA.";
        }
        private static void SetSignal(Label lamp, Label state, string text, bool signal, bool problem)
        {
            state.Text = text;
            lamp.ForeColor = problem ? Color.FromArgb(245, 175, 70) : signal ? Color.FromArgb(72, 211, 133) : Theme.Muted;
            state.ForeColor = problem ? lamp.ForeColor : signal ? Theme.Text : Theme.Muted;
            lamp.AccessibleDescription = text;
        }
        private void UpdateSignalRows(MicSnapshot mic, string micMode)
        {
            bool available = mic.DeviceAvailable;
            SetSignal(micLamp, micTitle, !available ? "Недоступен" : mic.Muted ? "Выключен в Windows" : mic.Peak > 0.0035f ? "Есть звук" : "Сейчас тихо",
                available && !mic.Muted && mic.Peak > 0.0035f, !available || mic.Muted);
            string status = !available ? "Нет данных" : micMode == "off" ? "Выключен в NVIDIA" : mic.Muted ? "Микрофон выключен" : !mic.NvidiaActive ? "Сеанс не найден" : mic.NvidiaPeak > 0.0035f ? "Есть звук" : "Сеанс активен · тихо";
            SetSignal(nvidiaMicLamp, nvidiaMicTitle, status,
                available && !mic.Muted && micMode != "off" && mic.NvidiaActive && mic.NvidiaPeak > 0.0035f,
                !available || mic.Muted || micMode == "off" || !mic.NvidiaActive);
        }
        private void Save() { if (preview) return; try { Storage.Save(settings); } catch (Exception ex) { Log("Не удалось сохранить настройки: " + ex.Message); } }
        private void Log(string text) { if (preview) return; Storage.Log(text); if (activity != null && !activity.IsDisposed) { activity.Items.Insert(0, DateTime.Now.ToString("HH:mm:ss") + "  " + text); if (activity.Items.Count > 200) activity.Items.RemoveAt(200); } }
        private void RefreshNow()
        {
            if (preview) return;
            try
            {
                discoveryAt = DateTime.MinValue; recoveryAt = DateTime.MinValue;
                // Reopen audio devices too, so newly connected microphones are discovered immediately.
                if (monitor != null) { monitor.Dispose(); monitor = null; }
                Tick(null, EventArgs.Empty);
                manualNoticeUntil = DateTime.UtcNow.AddSeconds(6);
                lastChecked.ForeColor = Theme.Accent;
                if (lastTickSucceeded)
                {
                    lastChecked.Text = "Проверено вручную в " + DateTime.Now.ToString("HH:mm:ss") + " · " + replayTitle.Text
                        + "\nЗакрытие окна сворачивает приложение в трей.  ·  v0.3.8\nMade by Kykystas with the help of AI";
                    Log("Ручная проверка: " + replayTitle.Text);
                }
                else lastChecked.Text = "Не удалось завершить проверку — подробности в состоянии выше.";
            }
            catch (Exception ex) { lastChecked.Text = "Не удалось обновить устройства: " + ex.Message; Log(lastChecked.Text); }
        }
        private void Tick(object sender, EventArgs e)
        {
            lastTickSucceeded = false;
            try
            {
                if (showSignal != null && showSignal.WaitOne(0)) ShowWindow();
                DateTime now = DateTime.UtcNow;
                if ((now - discoveryAt).TotalSeconds >= 6) { nvidia = Nvidia.Discover(); discoveryAt = now; }
                if ((now - recoveryAt).TotalSeconds >= 2)
                {
                    recoveryAt = now; bool? state = Nvidia.ReadLive(); nvidia.Replay = state;
                    if (state != lastState) { lastState = state; Log("NVIDIA Instant Replay: " + (state == true ? "ON" : state == false ? "OFF" : "UNKNOWN")); }
                    RecoveryDecision decision = recovery.Evaluate(state, settings.AutoRestore, nvidia.Running, nvidia.Hotkey != null, now);
                    replayTitle.Text = state == true ? "Повтор включён" : state == false ? "Повтор выключен" : "Состояние не определено";
                    replayTitle.ForeColor = state == true ? Theme.Accent : Theme.Muted;
                    replayDetail.Text = decision.Text;
                    if (recordingTestOpen && state == false) replayDetail.Text = "Автовосстановление продолжится после закрытия проверки записи.";
                    nvidiaStatus.Text = (nvidia.OverlayPath == null ? "NVIDIA не найдена" : nvidia.Running ? "NVIDIA обнаружена · оверлей работает" : "NVIDIA обнаружена · оверлей не запущен") + "   |   " + Nvidia.ShortcutText(nvidia.Hotkey);
                    tray.Text = "Replay Watcher: " + (state == true ? "повтор включён" : state == false ? "повтор выключен" : "состояние неизвестно");
                    Icon statusIcon = state == true ? trayIcons.On : state == false ? trayIcons.Off : trayIcons.Unknown;
                    if (tray.Icon != statusIcon) tray.Icon = statusIcon;
                    if (!recordingTestOpen && decision.Attempt && nvidia.Hotkey != null && Keyboard.Ready(nvidia.Hotkey) && Nvidia.ReadLive() == false)
                    {
                        bool sent = Keyboard.Toggle(nvidia.Hotkey); recovery.Attempted(now);
                        Log(sent ? "Отправлена команда включения. Ожидаю подтверждения NVIDIA." : "Windows не приняла команду включения.");
                    }
                }
                if (settings.MicrophoneMonitoring)
                {
                    if (monitor == null) monitor = new MicrophoneMonitor();
                    MicSnapshot mic = monitor.Poll(settings.MicrophoneId, nvidia.MicMode);
                    meter.Value = Math.Max(0, Math.Min(100, (int)(Math.Sqrt(mic.Peak) * 100)));
                    micDevice.Text = mic.Device; UpdateSignalRows(mic, nvidia.MicMode); micDetail.Text = mic.Detail.Replace("нажмите «Проверить клип»", "запишите проверку кнопкой ниже");
                    UpdateMicrophones();
                }
                if (now >= manualNoticeUntil)
                {
                    lastChecked.ForeColor = Theme.Muted;
                    lastChecked.Text = "Последняя проверка: " + recoveryAt.ToLocalTime().ToString("HH:mm:ss")
                        + "\nЗакрытие окна сворачивает приложение в трей.  ·  v0.3.8\nMade by Kykystas with the help of AI";
                }
                lastTickSucceeded = true;
            }
            catch (Exception ex) { tray.Icon = trayIcons.Unknown; tray.Text = "Replay Watcher: проверка недоступна"; replayDetail.Text = "Проверка временно недоступна: " + ex.Message; lastChecked.Text = "Проверка не завершена — повторная попытка автоматически."; Storage.Log(ex.GetType().Name + ": " + ex.Message); discoveryAt = DateTime.MinValue; }
        }
        private void UpdateMicrophones()
        {
            string signature = String.Join("|", monitor.Choices.Select(c => c.Id));
            if (Convert.ToString(microphones.Tag) == signature) return;
            refreshingChoices = true;
            try
            {
                microphones.Items.Clear(); microphones.Items.Add(new MicChoice { Id = "", Name = "Авто — микрофон NVIDIA / устройство Windows по умолчанию" });
                foreach (MicChoice choice in monitor.Choices) microphones.Items.Add(choice);
                int index = 0; for (int i = 1; i < microphones.Items.Count; i++) if (((MicChoice)microphones.Items[i]).Id == settings.MicrophoneId) index = i;
                // Do not overwrite the selected ID if a device is temporarily disconnected.
                if (index == 0 && !String.IsNullOrEmpty(settings.MicrophoneId)) { microphones.Items.Add(new MicChoice { Id = settings.MicrophoneId, Name = "Выбранный микрофон отключён" }); index = microphones.Items.Count - 1; }
                microphones.SelectedIndex = index; microphones.Tag = signature;
            }
            finally { refreshingChoices = false; }
        }
        private void CheckClip()
        {
            recordingTestOpen = true;
            try { using (var dialog = new RecordingTestDialog()) dialog.ShowDialog(this); }
            finally { recordingTestOpen = false; recoveryAt = DateTime.MinValue; }
        }
        private void ShowLog()
        {
            Form dialog = new Form { Text = "Журнал Replay Watcher", Size = new Size(780, 420), StartPosition = FormStartPosition.CenterParent, BackColor = Theme.Background, ForeColor = Theme.Text }; Theme.DarkTitle(dialog);
            activity = new ListBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10), HorizontalScrollbar = true, BackColor = Theme.Surface, ForeColor = Theme.Text, BorderStyle = BorderStyle.None };
            try { foreach (string line in File.ReadAllLines(Path.Combine(Storage.Root, "watcher.log")).Reverse().Take(200)) activity.Items.Add(line); } catch { }
            dialog.Controls.Add(activity); dialog.Show(this);
        }
        private void ShowDiagnostics()
        {
            if (nvidia == null) return;
            MessageBox.Show(this, "NVIDIA Overlay:\n" + (nvidia.OverlayPath ?? "не найден") + "\n\nНастройки NVIDIA:\n" + (nvidia.ConfigPath ?? "не найдены") + "\n\nРежим микрофона NVIDIA: " + nvidia.MicMode + "\n\nДанные приложения:\n" + Storage.Root + "\n\nФлаг повтора недокументирован NVIDIA. Неизвестное состояние не вызывает нажатий клавиш.", "Диагностика", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        private void ShowWindow() { ShowInTaskbar = true; Show(); WindowState = FormWindowState.Normal; Activate(); }
        private void Quit() { closing = true; Close(); }
        internal void RenderPreview(string file, bool microphone)
        {
            replayTitle.Text = "Повтор включён"; replayTitle.ForeColor = Theme.Accent;
            replayDetail.Text = "Автовосстановление активно · проверка каждые 2 секунды";
            nvidiaStatus.Text = "NVIDIA обнаружена · оверлей работает   |   Alt + Shift + F10";
            lastChecked.Text = "Проверено вручную в 14:30:05 · Повтор включён\nЗакрытие окна сворачивает приложение в трей.  ·  v0.3.8\nMade by Kykystas with the help of AI";
            lastChecked.ForeColor = Theme.Accent;
            if (microphone)
            {
                micEnabled.Checked = true; microphones.SelectedIndex = 0; meter.Value = 46;
                micDevice.Text = "Микрофон USB";
                UpdateSignalRows(new MicSnapshot { DeviceAvailable = true, Peak = 0.21f, NvidiaPeak = 0.18f, NvidiaActive = true }, "on");
                micDetail.Text = "Чтобы проверить звук в видео, запишите и прослушайте короткую проверку.";
            }
            Show(); Application.DoEvents();
            using (Bitmap image = new Bitmap(Width, Height)) { DrawToBitmap(image, new Rectangle(0, 0, Width, Height)); image.Save(file); }
            closing = true; Close();
        }
    }

}
