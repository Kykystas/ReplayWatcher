using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;

namespace ReplayWatcher
{
    internal sealed class RecordingTestDialog : Form
    {
        private readonly RecordingSession session = new RecordingSession();
        private readonly Label status, instructions, elapsed;
        private readonly Button start, stop, play;
        private readonly ComboBox trackChoice;
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 200 };
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private WasapiOut output;
        private WaveFileReader reader;
        private string playbackPath;
        private bool busy, closing;
        internal RecordingTestDialog(bool preview = false)
        {
            Text = L.T("Проверка записи микрофона"); ClientSize = new Size(700, 440); MinimumSize = new Size(700, 480); StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Background; ForeColor = Theme.Text; Font = new Font("Segoe UI", 10); Theme.DarkTitle(this);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 7 };
            foreach (int height in new[] { 44, 68, 50, 36, 44, 50 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(new Label { Text = L.T("Скажите несколько слов"), Font = new Font("Segoe UI", 21, FontStyle.Bold), Dock = DockStyle.Fill, ForeColor = Theme.Accent }, 0, 0);
            instructions = new Label { Text = L.T("Нажмите «Начать запись», поговорите и остановите запись.\nЗаписывает NVIDIA: экран и микрофон, выбранный в её настройках. Автостоп — 30 секунд."), Dock = DockStyle.Fill, ForeColor = Theme.Muted };
            layout.Controls.Add(instructions, 0, 1);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            start = Theme.Button(L.T("●  Начать запись"), async delegate { await StartRecording(); }); start.BackColor = Color.FromArgb(121, 29, 46);
            stop = Theme.Button(L.T("■  Остановить"), async delegate { await StopRecording(); }); stop.Enabled = false;
            play = Theme.Button(L.T("▶  Прослушать"), async delegate { await Play(); }); play.Enabled = false;
            buttons.Controls.Add(start); buttons.Controls.Add(stop); buttons.Controls.Add(play); layout.Controls.Add(buttons, 0, 2);
            elapsed = new Label { Text = L.T("Готово к проверке"), Dock = DockStyle.Fill, ForeColor = Theme.Muted }; layout.Controls.Add(elapsed, 0, 3);
            trackChoice = new DarkComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
            trackChoice.SelectedIndexChanged += delegate { StopPlayback(); }; layout.Controls.Add(trackChoice, 0, 4);
            status = new Label { Text = L.T("Запись начнётся только после нажатия кнопки."), Dock = DockStyle.Fill, ForeColor = Theme.Text }; layout.Controls.Add(status, 0, 5);
            var footer = new Label { Text = L.T("Ролик останется в папке записей NVIDIA. Если голос записан отдельно, выберите его дорожку для прослушивания."), Dock = DockStyle.Fill, ForeColor = Theme.Muted }; layout.Controls.Add(footer, 0, 6); Controls.Add(layout);
            if (!preview)
            {
                timer.Tick += async delegate { if (session.OwnsRecording) { int seconds = (int)(DateTime.UtcNow - session.StartedAt).TotalSeconds; elapsed.Text = L.T("Запись: ") + seconds + L.T(" / 30 сек."); if (seconds >= 30 && !busy) await StopRecording(); } };
                timer.Start();
            }
            FormClosing += async delegate(object sender, FormClosingEventArgs e)
            {
                if (closing) return;
                if (busy) { e.Cancel = true; status.Text = L.T("Дождитесь завершения текущей команды."); return; }
                if (session.OwnsRecording) { e.Cancel = true; await StopRecording(); if (!session.OwnsRecording) { closing = true; Close(); } }
            };
            FormClosed += delegate { cancellation.Cancel(); timer.Stop(); timer.Dispose(); StopPlayback(); cancellation.Dispose(); };
        }
        private async Task StartRecording()
        {
            if (busy) return; busy = true; start.Enabled = false; play.Enabled = false; trackChoice.Enabled = false; StopPlayback();
            status.Text = L.T("Ожидаю подтверждения запуска от NVIDIA…");
            try { await session.Start(); status.Text = L.T("Запись идёт. Скажите несколько слов и нажмите «Остановить»."); stop.Enabled = true; }
            catch (Exception ex) { status.Text = ex.Message; start.Enabled = !session.OwnsRecording; stop.Enabled = session.OwnsRecording; }
            finally { busy = false; }
        }
        private async Task StopRecording()
        {
            if (busy) return; busy = true; stop.Enabled = false; status.Text = L.T("Останавливаю и ожидаю сохранения видео…");
            try
            {
                string file = await session.Stop(); elapsed.Text = L.T("Запись остановлена");
                var tracks = await Task.Run(() => ClipAnalysis.Read(file, cancellation.Token));
                if (IsDisposed) return;
                trackChoice.Items.Clear(); foreach (AudioTrack track in tracks) if (track.Error == null) trackChoice.Items.Add(track);
                if (trackChoice.Items.Count == 0) throw new InvalidDataException(L.T("В ролике не удалось прочитать звук. Проверьте микрофон в NVIDIA."));
                trackChoice.SelectedIndex = trackChoice.Items.Count == 2 ? 1 : 0; trackChoice.Enabled = trackChoice.Items.Count > 1; play.Enabled = true;
                status.Text = trackChoice.Items.Count == 1 ? L.T("Готово. Нажмите «Прослушать» и проверьте, слышен ли ваш голос.") : L.T("Готово. Выбрана вторая дорожка; если голос в другой — переключите её выше.");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { status.Text = ex.Message; stop.Enabled = session.OwnsRecording; }
            finally { busy = false; start.Enabled = !session.OwnsRecording; }
        }
        private async Task Play()
        {
            if (output != null && output.PlaybackState == PlaybackState.Playing) { StopPlayback(); return; }
            var track = trackChoice.SelectedItem as AudioTrack; if (track == null || busy) return;
            busy = true; play.Enabled = false; start.Enabled = false; trackChoice.Enabled = false; StopPlayback();
            try
            {
                playbackPath = Path.Combine(Path.GetTempPath(), "ReplayWatcher-playback-" + Guid.NewGuid().ToString("N") + ".wav");
                string target = playbackPath;
                await Task.Run(() => ClipAnalysis.Extract(session.VideoPath, track, target, cancellation.Token));
                if (IsDisposed) return;
                reader = new WaveFileReader(playbackPath); output = new WasapiOut(); output.Init(reader);
                output.PlaybackStopped += delegate(object sender, StoppedEventArgs e) { if (!IsDisposed && IsHandleCreated) try { BeginInvoke((Action)delegate { play.Text = L.T("▶  Прослушать"); if (e.Exception != null) status.Text = L.T("Ошибка воспроизведения: ") + e.Exception.Message; }); } catch (InvalidOperationException) { } };
                output.Play(); play.Text = L.T("■  Стоп звука"); status.Text = L.T("Прослушивание сохранённой дорожки NVIDIA.");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { status.Text = L.T("Не удалось воспроизвести звук: ") + ex.Message; StopPlayback(); }
            finally { if (!IsDisposed) { busy = false; play.Enabled = true; start.Enabled = true; trackChoice.Enabled = trackChoice.Items.Count > 1; } }
        }
        private void StopPlayback()
        {
            if (output != null) { output.Stop(); output.Dispose(); output = null; }
            if (reader != null) { reader.Dispose(); reader = null; }
            if (playbackPath != null) { try { File.Delete(playbackPath); } catch { } playbackPath = null; }
            if (play != null) play.Text = L.T("▶  Прослушать");
        }
        internal void RenderPreview(string file)
        {
            Show(); Application.DoEvents(); using (var image = new Bitmap(Width, Height)) { DrawToBitmap(image, new Rectangle(0, 0, Width, Height)); image.Save(file); } closing = true; Close();
        }
    }
}
