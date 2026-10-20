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
        private LanguageSwitch languageSwitch;
        private MicSnapshot lastMic;
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
            L.Language = settings.Language; settings.Language = L.Language;
            Text = "Replay Watcher"; ClientSize = new Size(760, 832); MinimumSize = new Size(760, 862);
            StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 10); BackColor = Theme.Background; ForeColor = Theme.Text; Theme.DarkTitle(this);
            AutoScaleMode = AutoScaleMode.Dpi;
            BuildInterface();
            ContextMenuStrip menu = new ContextMenuStrip { BackColor = Theme.Surface, ForeColor = Theme.Text };
            menu.Items.Add(L.T("Открыть"), null, delegate { ShowWindow(); });
            menu.Items.Add(L.T("Выйти"), null, delegate { Quit(); });
            tray = new NotifyIcon { Text = L.T("Replay Watcher: проверяю состояние"), Icon = trayIcons.Unknown, ContextMenuStrip = menu, Visible = !preview };
            tray.DoubleClick += delegate { ShowWindow(); };
            timer = new System.Windows.Forms.Timer { Interval = 200 }; timer.Tick += Tick;
            Shown += delegate
            {
                if (preview) return;
                if (background) { Hide(); ShowInTaskbar = false; }
                timer.Start(); Log(L.T("Replay Watcher 0.4.0 запущен")); Tick(null, EventArgs.Empty);
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
            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 124));
            header.Controls.Add(LabelOf("Replay Watcher", 24, Theme.Accent, true), 0, 0);
            languageSwitch = new LanguageSwitch { Checked = L.English, Anchor = AnchorStyles.Top | AnchorStyles.Right, BackColor = Theme.Background, Margin = new Padding(0, 4, 0, 0) };
            languageSwitch.CheckedChanged += delegate { ChangeLanguage(); };
            header.Controls.Add(languageSwitch, 1, 0); root.Controls.Add(header, 0, 0);
            root.Controls.Add(LabelOf(L.T("Автоматическое восстановление NVIDIA Instant Replay"), 10, muted, false), 0, 1);
            TableLayoutPanel status = new TableLayoutPanel { BackColor = Theme.Surface, Dock = DockStyle.Fill, Padding = new Padding(18, 8, 18, 8), RowCount = 2, ColumnCount = 1, Margin = new Padding(0, 8, 0, 6) };
            status.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); status.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            replayTitle = LabelOf(L.T("Проверяю NVIDIA…"), 20, ink, true); replayDetail = LabelOf(L.T("Состояние появится автоматически"), 10, muted, false);
            status.Controls.Add(replayTitle); status.Controls.Add(replayDetail); root.Controls.Add(status, 0, 2);
            autoRestore = new CheckBox { Text = L.T("Автоматически включать повтор, если он отключился"), Checked = settings.AutoRestore, Dock = DockStyle.Fill, AutoSize = true };
            autoRestore.CheckedChanged += delegate { settings.AutoRestore = autoRestore.Checked; Save(); Log(settings.AutoRestore ? L.T("Автовосстановление включено") : L.T("Автовосстановление выключено")); recoveryAt = DateTime.MinValue; };
            root.Controls.Add(autoRestore, 0, 3);
            nvidiaStatus = LabelOf(L.T("Поиск NVIDIA Overlay…"), 9, muted, false); root.Controls.Add(nvidiaStatus, 0, 4);
            GroupBox micBox = new GroupBox { Text = L.T("Микрофон"), Dock = DockStyle.Fill, Padding = new Padding(14, 22, 14, 10), Margin = new Padding(0), BackColor = Theme.Surface, ForeColor = Theme.Text };
            TableLayoutPanel mic = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7 };
            foreach (int height in new[] { 32, 36, 22, 24, 34, 42 }) mic.RowStyles.Add(new RowStyle(SizeType.Absolute, height)); mic.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            micEnabled = new CheckBox { Text = L.T("Проверять микрофон"), Checked = settings.MicrophoneMonitoring, Dock = DockStyle.Fill, AutoSize = true };
            micEnabled.CheckedChanged += delegate
            {
                settings.MicrophoneMonitoring = micEnabled.Checked; Save();
                microphones.Enabled = micEnabled.Checked;
                if (!micEnabled.Checked) { if (monitor != null) monitor.Dispose(); monitor = null; meter.Value = 0; SetMicOff(); }
                Log(micEnabled.Checked ? L.T("Проверка микрофона включена") : L.T("Проверка микрофона выключена"));
            };
            microphones = new DarkComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Enabled = settings.MicrophoneMonitoring };
            microphones.Items.Add(new MicChoice { Id = "", Name = L.T("Авто — микрофон NVIDIA / устройство Windows по умолчанию") }); microphones.SelectedIndex = 0;
            microphones.SelectedIndexChanged += delegate { if (!refreshingChoices && microphones.SelectedItem is MicChoice) { settings.MicrophoneId = ((MicChoice)microphones.SelectedItem).Id; Save(); } };
            meter = new LevelMeter { Dock = DockStyle.Fill, Maximum = 100, Margin = new Padding(0, 4, 0, 6) };
            micDevice = LabelOf("", 9, muted, false); micDetail = LabelOf("", 9, muted, false);
            TableLayoutPanel signalRows = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Margin = new Padding(0) };
            signalRows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 26));
            signalRows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            signalRows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            signalRows.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); signalRows.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            micLamp = LabelOf("●", 15, muted, false); nvidiaMicLamp = LabelOf("●", 15, muted, false);
            micLamp.AccessibleName = L.T("Индикатор сигнала микрофона"); nvidiaMicLamp.AccessibleName = L.T("Индикатор сигнала NVIDIA");
            micTitle = LabelOf("", 10, ink, true); nvidiaMicTitle = LabelOf("", 10, ink, true);
            signalRows.Controls.Add(micLamp, 0, 0); signalRows.Controls.Add(LabelOf(L.T("Сигнал микрофона"), 10, ink, false), 1, 0); signalRows.Controls.Add(micTitle, 2, 0);
            signalRows.Controls.Add(nvidiaMicLamp, 0, 1); signalRows.Controls.Add(LabelOf(L.T("Сигнал в NVIDIA"), 10, ink, false), 1, 1); signalRows.Controls.Add(nvidiaMicTitle, 2, 1);
            mic.RowStyles[4].Height = 66;
            checkClip = ButtonOf(L.T("●  Записать и прослушать проверку…"), delegate { CheckClip(); });
            checkClip.AutoSize = false; checkClip.Dock = DockStyle.Fill; checkClip.Margin = new Padding(0, 2, 0, 0);
            mic.Controls.Add(micEnabled, 0, 0); mic.Controls.Add(microphones, 0, 1); mic.Controls.Add(meter, 0, 2); mic.Controls.Add(micDevice, 0, 3); mic.Controls.Add(signalRows, 0, 4); mic.Controls.Add(micDetail, 0, 5); mic.Controls.Add(checkClip, 0, 6); micBox.Controls.Add(mic); root.Controls.Add(micBox, 0, 5);
            startup = new CheckBox { Text = L.T("Запускать вместе с Windows"), Dock = DockStyle.Fill, AutoSize = true, Checked = !preview && Startup.Enabled };
            startup.CheckedChanged += delegate { if (preview) return; try { Startup.Set(startup.Checked); } catch (Exception ex) { MessageBox.Show(this, L.T("Не удалось изменить автозапуск: ") + ex.Message); } };
            root.Controls.Add(startup, 0, 6);
            FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0), WrapContents = false };
            buttons.Controls.Add(ButtonOf(L.T("Журнал"), delegate { ShowLog(); })); buttons.Controls.Add(ButtonOf(L.T("Диагностика"), delegate { ShowDiagnostics(); }));
            buttons.Controls.Add(ButtonOf(L.T("Проверить сейчас"), delegate { RefreshNow(); }));
            buttons.Controls.Add(ButtonOf(L.T("Выйти"), delegate { Quit(); })); root.Controls.Add(buttons, 0, 7);
            lastChecked = LabelOf(L.T("Ожидание проверки…\nЗакрытие окна сворачивает приложение в трей.  ·  v0.4.0\nMade by Kykystas with the help of AI"), 9, muted, false); root.Controls.Add(lastChecked, 0, 8);
            foreach (var box in new[] { autoRestore, micEnabled, startup }) { box.FlatStyle = FlatStyle.Flat; box.FlatAppearance.CheckedBackColor = Theme.Accent; box.FlatAppearance.BorderColor = Theme.Border; }
            SetMicOff();
        }
        private void SetMicOff()
        {
            micDevice.Text = L.T("Мониторинг выключен");
            SetSignal(micLamp, micTitle, L.T("Проверка выключена"), false, false);
            SetSignal(nvidiaMicLamp, nvidiaMicTitle, L.T("Проверка выключена"), false, false);
            micDetail.Text = L.T("Включите проверку, чтобы видеть уровень звука и сеанс NVIDIA.");
        }
        private void ChangeLanguage()
        {
            L.Language = languageSwitch.Checked ? "en" : "ru";
            settings.Language = L.Language; Save();
            L.Apply(this, micDevice);
            if (!settings.MicrophoneMonitoring) SetMicOff();
            else if (lastMic == null || !lastMic.DeviceAvailable) micDevice.Text = L.Translate(micDevice.Text);
            foreach (MicChoice choice in microphones.Items)
                if (String.IsNullOrEmpty(choice.Id) || choice.Name == "Выбранный микрофон отключён" || choice.Name == "Selected microphone disconnected")
                    choice.Name = L.Translate(choice.Name);
            microphones.Invalidate();
            tray.Text = L.Translate(tray.Text);
            foreach (ToolStripItem item in tray.ContextMenuStrip.Items) item.Text = L.Translate(item.Text);
            foreach (Form form in OwnedForms) L.Apply(form);
            if (activity != null && !activity.IsDisposed)
                for (int i = 0; i < activity.Items.Count; i++) activity.Items[i] = L.Translate(Convert.ToString(activity.Items[i]));
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
            SetSignal(micLamp, micTitle, !available ? L.T("Недоступен") : mic.Muted ? L.T("Выключен в Windows") : mic.Peak > 0.0035f ? L.T("Есть звук") : L.T("Сейчас тихо"),
                available && !mic.Muted && mic.Peak > 0.0035f, !available || mic.Muted);
            string status = !available ? L.T("Нет данных") : micMode == "off" ? L.T("Выключен в NVIDIA") : mic.Muted ? L.T("Микрофон выключен") : !mic.NvidiaActive ? L.T("Сеанс не найден") : mic.NvidiaPeak > 0.0035f ? L.T("Есть звук") : L.T("Сеанс активен · тихо");
            SetSignal(nvidiaMicLamp, nvidiaMicTitle, status,
                available && !mic.Muted && micMode != "off" && mic.NvidiaActive && mic.NvidiaPeak > 0.0035f,
                !available || mic.Muted || micMode == "off" || !mic.NvidiaActive);
        }
        private void Save() { if (preview) return; try { Storage.Save(settings); } catch (Exception ex) { Log(L.T("Не удалось сохранить настройки: ") + ex.Message); } }
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
                    lastChecked.Text = L.T("Проверено вручную в ") + DateTime.Now.ToString("HH:mm:ss") + " · " + replayTitle.Text
                        + L.T("\nЗакрытие окна сворачивает приложение в трей.  ·  v0.4.0\nMade by Kykystas with the help of AI");
                    Log(L.T("Ручная проверка: ") + replayTitle.Text);
                }
                else lastChecked.Text = L.T("Не удалось завершить проверку — подробности в состоянии выше.");
            }
            catch (Exception ex) { lastChecked.Text = L.T("Не удалось обновить устройства: ") + ex.Message; Log(lastChecked.Text); }
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
                    replayTitle.Text = state == true ? L.T("Повтор включён") : state == false ? L.T("Повтор выключен") : L.T("Состояние не определено");
                    replayTitle.ForeColor = state == true ? Theme.Accent : Theme.Muted;
                    replayDetail.Text = decision.Text;
                    if (recordingTestOpen && state == false) replayDetail.Text = L.T("Автовосстановление продолжится после закрытия проверки записи.");
                    nvidiaStatus.Text = (nvidia.OverlayPath == null ? L.T("NVIDIA не найдена") : nvidia.Running ? L.T("NVIDIA обнаружена · оверлей работает") : L.T("NVIDIA обнаружена · оверлей не запущен")) + "   |   " + Nvidia.ShortcutText(nvidia.Hotkey);
                    tray.Text = "Replay Watcher: " + (state == true ? L.T("повтор включён") : state == false ? L.T("повтор выключен") : L.T("состояние неизвестно"));
                    Icon statusIcon = state == true ? trayIcons.On : state == false ? trayIcons.Off : trayIcons.Unknown;
                    if (tray.Icon != statusIcon) tray.Icon = statusIcon;
                    if (!recordingTestOpen && decision.Attempt && nvidia.Hotkey != null && Keyboard.Ready(nvidia.Hotkey) && Nvidia.ReadLive() == false)
                    {
                        bool sent = Keyboard.Toggle(nvidia.Hotkey); recovery.Attempted(now);
                        Log(sent ? L.T("Отправлена команда включения. Ожидаю подтверждения NVIDIA.") : L.T("Windows не приняла команду включения."));
                    }
                }
                if (settings.MicrophoneMonitoring)
                {
                    if (monitor == null) monitor = new MicrophoneMonitor();
                    MicSnapshot mic = monitor.Poll(settings.MicrophoneId, nvidia.MicMode); lastMic = mic;
                    meter.Value = Math.Max(0, Math.Min(100, (int)(Math.Sqrt(mic.Peak) * 100)));
                    micDevice.Text = mic.Device; UpdateSignalRows(mic, nvidia.MicMode); micDetail.Text = mic.Detail.Replace(L.T("нажмите «Проверить клип»"), L.T("запишите проверку кнопкой ниже"));
                    UpdateMicrophones();
                }
                if (now >= manualNoticeUntil)
                {
                    lastChecked.ForeColor = Theme.Muted;
                    lastChecked.Text = L.T("Последняя проверка: ") + recoveryAt.ToLocalTime().ToString("HH:mm:ss")
                        + L.T("\nЗакрытие окна сворачивает приложение в трей.  ·  v0.4.0\nMade by Kykystas with the help of AI");
                }
                lastTickSucceeded = true;
            }
            catch (Exception ex) { tray.Icon = trayIcons.Unknown; tray.Text = L.T("Replay Watcher: проверка недоступна"); replayDetail.Text = L.T("Проверка временно недоступна: ") + ex.Message; lastChecked.Text = L.T("Проверка не завершена — повторная попытка автоматически."); Storage.Log(ex.GetType().Name + ": " + ex.Message); discoveryAt = DateTime.MinValue; }
        }
        private void UpdateMicrophones()
        {
            string signature = String.Join("|", monitor.Choices.Select(c => c.Id));
            if (Convert.ToString(microphones.Tag) == signature) return;
            refreshingChoices = true;
            try
            {
                microphones.Items.Clear(); microphones.Items.Add(new MicChoice { Id = "", Name = L.T("Авто — микрофон NVIDIA / устройство Windows по умолчанию") });
                foreach (MicChoice choice in monitor.Choices) microphones.Items.Add(choice);
                int index = 0; for (int i = 1; i < microphones.Items.Count; i++) if (((MicChoice)microphones.Items[i]).Id == settings.MicrophoneId) index = i;
                // Do not overwrite the selected ID if a device is temporarily disconnected.
                if (index == 0 && !String.IsNullOrEmpty(settings.MicrophoneId)) { microphones.Items.Add(new MicChoice { Id = settings.MicrophoneId, Name = L.T("Выбранный микрофон отключён") }); index = microphones.Items.Count - 1; }
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
            Form dialog = new Form { Text = L.T("Журнал Replay Watcher"), Size = new Size(780, 420), StartPosition = FormStartPosition.CenterParent, BackColor = Theme.Background, ForeColor = Theme.Text }; Theme.DarkTitle(dialog);
            activity = new ListBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10), HorizontalScrollbar = true, BackColor = Theme.Surface, ForeColor = Theme.Text, BorderStyle = BorderStyle.None };
            try { foreach (string line in File.ReadAllLines(Path.Combine(Storage.Root, "watcher.log")).Reverse().Take(200)) activity.Items.Add(L.Translate(line)); } catch { }
            dialog.Controls.Add(activity); dialog.Show(this);
        }
        private void ShowDiagnostics()
        {
            if (nvidia == null) return;
            MessageBox.Show(this, "NVIDIA Overlay:\n" + (nvidia.OverlayPath ?? L.T("не найден")) + L.T("\n\nНастройки NVIDIA:\n") + (nvidia.ConfigPath ?? L.T("не найдены")) + L.T("\n\nРежим микрофона NVIDIA: ") + nvidia.MicMode + L.T("\n\nДанные приложения:\n") + Storage.Root + L.T("\n\nФлаг повтора недокументирован NVIDIA. Неизвестное состояние не вызывает нажатий клавиш."), L.T("Диагностика"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        private void ShowWindow() { ShowInTaskbar = true; Show(); WindowState = FormWindowState.Normal; Activate(); }
        private void Quit() { closing = true; Close(); }
        internal void RenderPreview(string file, bool microphone)
        {
            replayTitle.Text = L.T("Повтор включён"); replayTitle.ForeColor = Theme.Accent;
            replayDetail.Text = L.T("Автовосстановление активно · проверка каждые 2 секунды");
            nvidiaStatus.Text = L.T("NVIDIA обнаружена · оверлей работает   |   Alt + Shift + F10");
            lastChecked.Text = L.T("Проверено вручную в 14:30:05 · Повтор включён\nЗакрытие окна сворачивает приложение в трей.  ·  v0.4.0\nMade by Kykystas with the help of AI");
            lastChecked.ForeColor = Theme.Accent;
            if (microphone)
            {
                micEnabled.Checked = true; microphones.SelectedIndex = 0; meter.Value = 46;
                micDevice.Text = L.T("Микрофон USB");
                UpdateSignalRows(new MicSnapshot { DeviceAvailable = true, Peak = 0.21f, NvidiaPeak = 0.18f, NvidiaActive = true }, "on");
                micDetail.Text = L.T("Чтобы проверить звук в видео, запишите и прослушайте короткую проверку.");
            }
            Show(); Application.DoEvents();
            using (Bitmap image = new Bitmap(Width, Height)) { DrawToBitmap(image, new Rectangle(0, 0, Width, Height)); image.Save(file); }
            closing = true; Close();
        }
    }

}
