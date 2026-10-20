using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
namespace ReplayWatcher
{
    // Explicit developer integration check. Unlike --self-test, this records a short NVIDIA video.
    internal static class RecordingSmokeTest
    {
        internal static int Run() { return RunAsync().GetAwaiter().GetResult(); }
        private static async Task<int> RunAsync()
        {
            var session = new RecordingSession(); string wave = Path.Combine(Path.GetTempPath(), "ReplayWatcher-smoke-" + Guid.NewGuid().ToString("N") + ".wav");
            try
            {
                await session.Start(); await Task.Delay(3000);
                string video = await session.Stop();
                var tracks = ClipAnalysis.Read(video, CancellationToken.None);
                if (tracks.Count == 0) throw new Exception("Saved audio not readable");
                foreach (var track in tracks)
                {
                    if (track.Error != null) throw new Exception(track.Error);
                    ClipAnalysis.Extract(video, track, wave, CancellationToken.None);
                    using (var reader = new WaveFileReader(wave))
                    using (var output = new WasapiOut())
                    {
                        if (reader.Length == 0) throw new Exception("Empty extracted audio");
                        output.Init(reader); // Validate every saved track without automatically playing the user's voice.
                    }
                    File.Delete(wave);
                }
                File.WriteAllText(Path.Combine(Storage.Root, "recording-smoke-test.txt"), "PASS: NVIDIA start confirmed; stop confirmed; saved video found; audio decoded; playback device initialized.\nVideo: " + video + "\nTracks: " + tracks.Count);
                return 0;
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(Storage.Root, "recording-smoke-test.txt"), ex.ToString()); return 1; }
            finally { if (session.OwnsRecording) try { session.Stop().GetAwaiter().GetResult(); } catch { } try { File.Delete(wave); } catch { } }
        }
    }
}
