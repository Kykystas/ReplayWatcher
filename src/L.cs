using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
namespace ReplayWatcher
{
    internal static class L
    {
        private static string language = "ru";
        internal static string Normalize(string value) { return value == "en" ? "en" : "ru"; }
        internal static string Language { get { return language; } set { language = Normalize(value); } }
        internal static bool English { get { return language == "en"; } }
        private static readonly Dictionary<string, string> catalog = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { " — звук есть", " — audio detected" },
            { " — не прочитана", " — unreadable" },
            { " — тишина / очень тихо", " — silent / very quiet" },
            { " / 30 сек.", " / 30 sec." },
            { " сек.", " sec." },
            { "\n\nДанные приложения:\n", "\n\nApplication data:\n" },
            { "\n\nНастройки NVIDIA:\n", "\n\nNVIDIA settings:\n" },
            { "\n\nРежим микрофона NVIDIA: ", "\n\nNVIDIA microphone mode: " },
            { "\n\nФлаг повтора недокументирован NVIDIA. Неизвестное состояние не вызывает нажатий клавиш.", "\n\nNVIDIA's replay flag is undocumented. No shortcuts are sent when the status is unknown." },
            { "\nЗакрытие окна сворачивает приложение в трей.  ·  v0.4.0\nMade by Kykystas with the help of AI", "\nClosing the window minimizes to the tray.  ·  v0.4.0\nMade by Kykystas with the help of AI" },
            { "■  Остановить", "■  Stop recording" },
            { "■  Стоп звука", "■  Stop audio" },
            { "▶  Прослушать", "▶  Listen" },
            { "●  Записать и прослушать проверку…", "●  Record and listen to a test…" },
            { "●  Начать запись", "●  Start recording" },
            { "Авто — микрофон NVIDIA / устройство Windows по умолчанию", "Auto — NVIDIA microphone / default Windows device" },
            { "Автовосстановление активно · проверка каждые 2 секунды", "Automatic recovery active · checking every 2 seconds" },
            { "Автовосстановление включено", "Automatic recovery enabled" },
            { "Автовосстановление выключено", "Automatic recovery disabled" },
            { "Автовосстановление продолжится после закрытия проверки записи.", "Recovery will resume when the recording test is closed." },
            { "Автоматически включать повтор, если он отключился", "Automatically restore replay when it turns off" },
            { "Автоматическое восстановление NVIDIA Instant Replay", "Automatic NVIDIA Instant Replay recovery" },
            { "Аудиодорожка ", "Audio track " },
            { "В аудиосеансе NVIDIA есть сигнал", "Signal detected in the NVIDIA audio session" },
            { "В настройках NVIDIA микрофон выключен", "Microphone disabled in NVIDIA settings" },
            { "В ролике не удалось прочитать звук. Проверьте микрофон в NVIDIA.", "Could not read audio from the video. Check your NVIDIA microphone settings." },
            { "В NVIDIA выбран режим «Нажми и говори». Удерживайте свою клавишу для проверки.", "NVIDIA is set to Push to talk. Hold your assigned key to test." },
            { "В NVIDIA не назначено сочетание включения повтора.", "No Instant Replay hotkey is assigned in NVIDIA." },
            { "В NVIDIA не назначено сочетание для обычной записи.", "No manual recording hotkey is assigned in NVIDIA." },
            { "Включите оверлей в NVIDIA App или GeForce Experience.", "Enable the overlay in NVIDIA App or GeForce Experience." },
            { "Включите оверлей NVIDIA.", "Enable the NVIDIA overlay." },
            { "Включите проверку, чтобы видеть уровень звука и сеанс NVIDIA.", "Enable monitoring to see microphone levels and the NVIDIA session." },
            { "Выберите существующий локальный видеофайл.", "Choose an existing local video file." },
            { "Выбранный микрофон отключён", "Selected microphone disconnected" },
            { "Выйти", "Exit" },
            { "Выключен в NVIDIA", "Disabled in NVIDIA" },
            { "Выключен в Windows", "Muted in Windows" },
            { "Готово к проверке", "Ready to test" },
            { "Готово. Выбрана вторая дорожка; если голос в другой — переключите её выше.", "Ready. Track 2 is selected; choose another track above if needed." },
            { "Готово. Нажмите «Прослушать» и проверьте, слышен ли ваш голос.", "Ready. Press Listen and check that you can hear your voice." },
            { "Декодер не возвращает аудиоданные.", "The decoder is not returning audio data." },
            { "Декодер не предоставил PCM 16 bit.", "The decoder did not provide 16-bit PCM audio." },
            { "Диагностика", "Diagnostics" },
            { "Дождитесь завершения текущей команды.", "Wait for the current operation to finish." },
            { "Есть звук", "Signal detected" },
            { "Журнал Replay Watcher", "Replay Watcher activity log" },
            { "Журнал", "Activity log" },
            { "Запись завершена, но путь к ролику не получен. Его можно найти в галерее NVIDIA.", "Recording ended, but its path is unavailable. Find the video in the NVIDIA gallery." },
            { "Запись идёт. Скажите несколько слов и нажмите «Остановить».", "Recording. Say a few words, then press Stop recording." },
            { "Запись начнётся только после нажатия кнопки.", "Recording starts only when you press the button." },
            { "Запись остановлена", "Recording stopped" },
            { "Запись: ", "Recording: " },
            { "запишите проверку кнопкой ниже", "record a test using the button below" },
            { "Запускать вместе с Windows", "Start with Windows" },
            { "Индикатор показывает входящий сигнал. Чтобы проверить звук в видео, нажмите «Проверить клип».", "The meter shows the input signal. To check video audio, click Check clip." },
            { "Индикатор сигнала микрофона", "Microphone signal indicator" },
            { "Индикатор сигнала NVIDIA", "NVIDIA signal indicator" },
            { "Микрофон выключен", "Microphone muted" },
            { "Микрофон не найден", "Microphone not found" },
            { "Микрофон недоступен", "Microphone unavailable" },
            { "Микрофон отключён или громкость равна нулю", "Microphone muted or volume is zero" },
            { "Микрофон слышен · активный сеанс NVIDIA не найден", "Microphone signal detected · no active NVIDIA session" },
            { "Микрофон слышен · сеанс NVIDIA активен", "Microphone signal detected · NVIDIA session active" },
            { "Микрофон USB", "USB microphone" },
            { "Микрофон", "Microphone" },
            { "Мониторинг выключен", "Monitoring disabled" },
            { "Нажмите «Начать запись», поговорите и остановите запись.\nЗаписывает NVIDIA: экран и микрофон, выбранный в её настройках. Автостоп — 30 секунд.", "Start recording, say a few words, then stop.\nNVIDIA records your screen and its selected microphone. Auto-stop: 30 seconds." },
            { "нажмите «Проверить клип»", "click Check clip" },
            { "Не назначено", "Not assigned" },
            { "не найден", "not found" },
            { "не найдены", "not available" },
            { "Не удалось воспроизвести звук: ", "Could not play audio: " },
            { "Не удалось завершить проверку — подробности в состоянии выше.", "Check could not finish — see the status above." },
            { "Не удалось запустить Replay Watcher.\n", "Could not start Replay Watcher.\n" },
            { "Не удалось изменить автозапуск: ", "Could not change Windows startup: " },
            { "Не удалось обновить устройства: ", "Could not refresh devices: " },
            { "Не удалось прочитать состояние микрофона", "Could not read microphone status" },
            { "Не удалось сохранить настройки: ", "Could not save settings: " },
            { "Недоступен", "Unavailable" },
            { "Нет данных", "No data" },
            { "Ожидание проверки…\nЗакрытие окна сворачивает приложение в трей.  ·  v0.4.0\nMade by Kykystas with the help of AI", "Waiting for a check…\nClosing the window minimizes to the tray.  ·  v0.4.0\nMade by Kykystas with the help of AI" },
            { "Ожидаю подтверждения запуска от NVIDIA…", "Waiting for NVIDIA to confirm recording…" },
            { "Ожидаю подтверждения. Следующая попытка через ", "Waiting for confirmation. Next attempt in " },
            { "Останавливаю и ожидаю сохранения видео…", "Stopping and waiting for the video to save…" },
            { "Открыть", "Open" },
            { "Отправлена команда включения. Ожидаю подтверждения NVIDIA.", "Enable command sent. Waiting for NVIDIA confirmation." },
            { "Отпустите служебные клавиши и нажмите «Остановить» снова.", "Release modifier keys and press Stop recording again." },
            { "Отпустите служебные клавиши и повторите попытку.", "Release modifier keys and try again." },
            { "Ошибка воспроизведения: ", "Playback error: " },
            { "Ошибка декодирования аудио.", "Audio decoding error." },
            { "Ошибка запуска: ", "Startup error: " },
            { "Повтор включён", "Replay is on" },
            { "повтор включён", "replay is on" },
            { "Повтор выключен. Готовлю восстановление…", "Replay is off. Preparing recovery…" },
            { "Повтор выключен", "Replay is off" },
            { "повтор выключен", "replay is off" },
            { "Подключите микрофон или выберите другое устройство.", "Connect a microphone or select another device." },
            { "Поиск NVIDIA Overlay…", "Looking for NVIDIA Overlay…" },
            { "Последняя проверка: ", "Last checked: " },
            { "Пробная запись уже завершена, а сейчас идёт другая запись NVIDIA. Её приложение не остановит.", "The test recording has ended and another NVIDIA recording is running. The app will not stop it." },
            { "Пробная запись уже идёт.", "A test recording is already running." },
            { "Пробная запись NVIDIA запущена", "NVIDIA test recording started" },
            { "Пробная запись NVIDIA остановлена", "NVIDIA test recording stopped" },
            { "Пробую включить повтор…", "Trying to enable replay…" },
            { "Проверено вручную в ", "Manually checked at " },
            { "Проверено вручную в 14:30:05 · Повтор включён\nЗакрытие окна сворачивает приложение в трей.  ·  v0.4.0\nMade by Kykystas with the help of AI", "Manually checked at 14:30:05 · Replay is on\nClosing the window minimizes to the tray.  ·  v0.4.0\nMade by Kykystas with the help of AI" },
            { "Проверить сейчас", "Check now" },
            { "Проверка временно недоступна: ", "Check temporarily unavailable: " },
            { "Проверка выключена", "Check disabled" },
            { "Проверка записи микрофона", "Microphone recording test" },
            { "Проверка микрофона включена", "Microphone monitoring enabled" },
            { "Проверка микрофона выключена", "Microphone monitoring disabled" },
            { "Проверка не завершена — повторная попытка автоматически.", "Check incomplete — retrying automatically." },
            { "Проверка не открывает запись и не сохраняет звук.", "Monitoring does not start a recording or save audio." },
            { "Проверьте подключение и разрешения Windows. ", "Check the device connection and Windows permissions. " },
            { "Проверять микрофон", "Monitor microphone" },
            { "Проверяю NVIDIA…", "Checking NVIDIA…" },
            { "Прослушивание сохранённой дорожки NVIDIA.", "Playing the saved NVIDIA audio track." },
            { "Ролик ещё сохраняется. Откройте его позже в галерее NVIDIA.", "The video is still saving. Open it later in the NVIDIA gallery." },
            { "Ролик останется в папке записей NVIDIA. Если голос записан отдельно, выберите его дорожку для прослушивания.", "The video stays in NVIDIA's recordings folder. Select the voice track if audio was recorded separately." },
            { "Ручная проверка: ", "Manual check: " },
            { "Сеанс активен · тихо", "Session active · quiet" },
            { "Сеанс не найден", "Session not found" },
            { "Сеанс NVIDIA активен · сейчас тихо", "NVIDIA session active · quiet right now" },
            { "Сейчас тихо · активный сеанс NVIDIA не найден", "Quiet right now · no active NVIDIA session" },
            { "Сейчас тихо", "Quiet right now" },
            { "Сигнал в NVIDIA", "NVIDIA signal" },
            { "Сигнал микрофона", "Microphone signal" },
            { "Скажите несколько слов", "Say a few words" },
            { "Состояние записи изменилось. Повторите проверку.", "Recording status changed. Try the test again." },
            { "Состояние записи недоступно. Остановите запись через оверлей NVIDIA.", "Recording status is unavailable. Stop recording using the NVIDIA overlay." },
            { "Состояние не определено", "Status unknown" },
            { "Состояние неизвестно — переключение не отправляется.", "Status unknown — no toggle will be sent." },
            { "состояние неизвестно", "status unknown" },
            { "Состояние появится автоматически", "Status will appear automatically" },
            { "Уже идёт другая запись NVIDIA. Сначала остановите её — приложение не будет её прерывать.", "Another NVIDIA recording is running. Stop it first; the app will not interrupt it." },
            { "Чтобы проверить звук в видео, запишите и прослушайте короткую проверку.", "To check the audio in a video, record and listen to a short test." },
            { "Язык интерфейса", "Interface language" },
            { "NVIDIA не найдена", "NVIDIA not found" },
            { "NVIDIA не подтвердила запуск. Возможно, захват заблокирован защищённым содержимым.", "NVIDIA did not confirm recording. Protected content may be blocking capture." },
            { "NVIDIA не предоставила состояние записи. Проверьте, что оверлей и Instant Replay запущены.", "NVIDIA recording status is unavailable. Check that the overlay and Instant Replay are running." },
            { "NVIDIA обнаружена · оверлей не запущен", "NVIDIA detected · overlay not running" },
            { "NVIDIA обнаружена · оверлей работает   |   Alt + Shift + F10", "NVIDIA detected · overlay running   |   Alt + Shift + F10" },
            { "NVIDIA обнаружена · оверлей работает", "NVIDIA detected · overlay running" },
            { "NVIDIA пока не подтвердила остановку. Проверьте оверлей.", "NVIDIA has not confirmed stopping. Check the overlay." },
            { "NVIDIA Overlay не найден", "NVIDIA Overlay not found" },
            { "Replay Watcher 0.4.0 запущен", "Replay Watcher 0.4.0 started" },
            { "Replay Watcher: проверка недоступна", "Replay Watcher: check unavailable" },
            { "Replay Watcher: проверяю состояние", "Replay Watcher: checking status" },
            { "Windows не приняла команду включения.", "Windows did not accept the enable command." },
            { "Windows не приняла команду записи.", "Windows did not accept the recording command." },
            { "Windows не приняла команду остановки.", "Windows did not accept the stop command." },
        };
        internal static string T(string russian)
        {
            string translated;
            return English && catalog.TryGetValue(russian, out translated) ? translated : russian;
        }
        internal static string Translate(string text)
        {
            if (String.IsNullOrEmpty(text)) return text;
            if (English) { string exact; if (catalog.TryGetValue(text, out exact)) return exact; }
            else { foreach (var pair in catalog) if (pair.Value == text) return pair.Key; }
            // Dynamic captions consist of catalog fragments plus timestamps, numbers or error details.
            foreach (var pair in catalog.OrderByDescending(p => English ? p.Key.Length : p.Value.Length))
                text = text.Replace(English ? pair.Key : pair.Value, English ? pair.Value : pair.Key);
            return text;
        }
        internal static void Apply(Control root, Control exclude = null)
        {
            if (root == exclude) return;
            // Hardware names and editable content must not be translated.
            if (!(root is ListControl) && !(root is TextBoxBase)) root.Text = Translate(root.Text);
            root.AccessibleName = Translate(root.AccessibleName);
            root.AccessibleDescription = Translate(root.AccessibleDescription);
            foreach (Control child in root.Controls) Apply(child, exclude);
        }
    }
}
