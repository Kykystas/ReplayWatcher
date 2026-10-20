using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using NAudio.MediaFoundation;

namespace ReplayWatcher
{
    internal sealed class AudioTrack
    {
        internal int Number, Channels, SampleRate, StreamIndex;
        internal double Peak, Rms, Seconds;
        internal string Error;
        internal bool Truncated;
        internal bool HasSignal { get { return Peak >= 0.005 && Rms >= 0.0001; } }
        public override string ToString() { return L.T("Аудиодорожка ") + Number + (Error != null ? L.T(" — не прочитана") : HasSignal ? L.T(" — звук есть") : L.T(" — тишина / очень тихо")); }
    }
    internal static class ClipAnalysis
    {
        internal static List<AudioTrack> Read(string file, CancellationToken cancellation)
        {
            // Only existing local files chosen by the user; no URLs or network shares.
            if (!Path.IsPathRooted(file) || file.StartsWith(@"\\") || !File.Exists(file)) throw new IOException(L.T("Выберите существующий локальный видеофайл."));
            MediaFoundationApi.Startup();
            List<int> indexes = new List<int>(); IMFSourceReader reader = null;
            try
            {
                MediaFoundationInterop.MFCreateSourceReaderFromURL(file, null, out reader);
                for (int i = 0; i < 32; i++)
                {
                    IMFMediaType native = null;
                    try { reader.GetNativeMediaType(i, 0, out native); if (new MediaType(native).MajorType == MediaTypes.MFMediaType_Audio) indexes.Add(i); }
                    catch (COMException) { break; }
                    finally { if (native != null) Marshal.ReleaseComObject(native); }
                }
            }
            finally { if (reader != null) Marshal.ReleaseComObject(reader); }
            List<AudioTrack> tracks = new List<AudioTrack>();
            foreach (int index in indexes)
            {
                cancellation.ThrowIfCancellationRequested();
                AudioTrack track = new AudioTrack { Number = tracks.Count + 1, StreamIndex = index }; tracks.Add(track);
                try { Decode(file, index, track, cancellation); }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { track.Error = ex.Message; }
            }
            return tracks;
        }
        internal static void Extract(string file, AudioTrack track, string outputPath, CancellationToken cancellation)
        {
            Decode(file, track.StreamIndex, new AudioTrack(), cancellation, outputPath);
        }
        private static void Decode(string file, int index, AudioTrack track, CancellationToken cancellation, string outputPath = null)
        {
            IMFSourceReader reader = null; IMFMediaType native = null; MediaType pcm = null; IMFMediaType output = null; NAudio.Wave.WaveFileWriter wave = null;
            try
            {
                MediaFoundationInterop.MFCreateSourceReaderFromURL(file, null, out reader);
                reader.SetStreamSelection(MediaFoundationInterop.MF_SOURCE_READER_ALL_STREAMS, false);
                reader.SetStreamSelection(index, true);
                reader.GetNativeMediaType(index, 0, out native);
                MediaType original = new MediaType(native);
                pcm = new MediaType(); pcm.MajorType = MediaTypes.MFMediaType_Audio; pcm.SubType = AudioSubtypes.MFAudioFormat_PCM;
                pcm.ChannelCount = original.ChannelCount; pcm.SampleRate = original.SampleRate; pcm.BitsPerSample = 16;
                reader.SetCurrentMediaType(index, IntPtr.Zero, pcm.MediaFoundationObject);
                reader.GetCurrentMediaType(index, out output);
                MediaType format = new MediaType(output);
                if (format.BitsPerSample != 16 || format.SubType != AudioSubtypes.MFAudioFormat_PCM) throw new NotSupportedException(L.T("Декодер не предоставил PCM 16 bit."));
                track.Channels = format.ChannelCount; track.SampleRate = format.SampleRate;
                if (outputPath != null) wave = new NAudio.Wave.WaveFileWriter(outputPath, new NAudio.Wave.WaveFormat(track.SampleRate, 16, track.Channels));
                long count = 0; double squareSum = 0; int emptySamples = 0;
                for (int packets = 0; packets < 1000000; packets++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    IMFSample sample = null; IMFMediaBuffer buffer = null; bool locked = false;
                    try
                    {
                        int actual; MF_SOURCE_READER_FLAG flags; ulong timestamp;
                        reader.ReadSample(index, 0, out actual, out flags, out timestamp, out sample);
                        if ((flags & MF_SOURCE_READER_FLAG.MF_SOURCE_READERF_ENDOFSTREAM) != 0) break;
                        if (((int)flags & 1) != 0) throw new InvalidDataException(L.T("Ошибка декодирования аудио."));
                        if (sample == null) { if (++emptySamples > 100) throw new InvalidDataException(L.T("Декодер не возвращает аудиоданные.")); continue; }
                        emptySamples = 0; sample.ConvertToContiguousBuffer(out buffer);
                        IntPtr pointer; int maximum, length; buffer.Lock(out pointer, out maximum, out length); locked = true;
                        byte[] bytes = new byte[length]; Marshal.Copy(pointer, bytes, 0, length);
                        if (wave != null) wave.Write(bytes, 0, bytes.Length);
                        for (int i = 0; i + 1 < length; i += 2)
                        {
                            double value = BitConverter.ToInt16(bytes, i) / 32768.0;
                            track.Peak = Math.Max(track.Peak, Math.Abs(value)); squareSum += value * value; count++;
                        }
                        track.Seconds = (double)count / Math.Max(1, track.SampleRate * track.Channels);
                        if (track.Seconds >= 600) { track.Truncated = true; break; }
                    }
                    finally { if (locked) buffer.Unlock(); if (buffer != null) Marshal.ReleaseComObject(buffer); if (sample != null) Marshal.ReleaseComObject(sample); }
                }
                track.Rms = count == 0 ? 0 : Math.Sqrt(squareSum / count);
            }
            finally
            {
                if (wave != null) wave.Dispose();
                if (output != null) Marshal.ReleaseComObject(output);
                if (pcm != null) Marshal.ReleaseComObject(pcm.MediaFoundationObject);
                if (native != null) Marshal.ReleaseComObject(native);
                if (reader != null) Marshal.ReleaseComObject(reader);
            }
        }
    }
}
