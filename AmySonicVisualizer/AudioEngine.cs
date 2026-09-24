using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NAudio.Wave;

namespace AmySonicVisualizer
{
    /// <summary>
    /// Centralized infrastructure for managing audio playback, state, and shared sample data.
    /// </summary>
    public class AudioEngine : IDisposable
    {
        private AudioFileReader? _audioReader;
        private WaveOutEvent? _waveOut;
        private float[] _monoSamples = Array.Empty<float>();

        public float[] MonoSamples => _monoSamples;
        public int SampleRate => _audioReader?.WaveFormat.SampleRate ?? 44100;

        public bool IsLoaded => _audioReader != null;
        public bool IsLoading { get; private set; }

        public event EventHandler? AudioLoading;
        public event EventHandler? FileLoaded;

        public double CurrentTime
        {
            get => _audioReader?.CurrentTime.TotalSeconds ?? 0;
            set { if (_audioReader != null) _audioReader.CurrentTime = TimeSpan.FromSeconds(value); }
        }

        public double TotalTime => _audioReader?.TotalTime.TotalSeconds ?? 1;

        public double Progress
        {
            get => Math.Clamp(CurrentTime / TotalTime, 0.0, 1.0);
            set => CurrentTime = value * TotalTime;
        }

        public async Task LoadAsync(string filePath)
        {
            IsLoading = true;
            AudioLoading?.Invoke(this, EventArgs.Empty);

            _waveOut?.Stop();
            _waveOut?.Dispose();
            _audioReader?.Dispose();

            _audioReader = new AudioFileReader(filePath);
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_audioReader);

            // Downmix the entire file to a mono sample array in the background for our visualizers
            _monoSamples = await Task.Run(() =>
            {
                var samples = new List<float>((int)(_audioReader.Length / 4));
                float[] buffer = new float[_audioReader.WaveFormat.SampleRate * _audioReader.WaveFormat.Channels];
                int read;
                int channels = _audioReader.WaveFormat.Channels;

                long originalPosition = _audioReader.Position;
                _audioReader.Position = 0;

                while ((read = _audioReader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (int i = 0; i < read; i += channels)
                    {
                        float sum = 0;
                        for (int c = 0; c < channels; c++) sum += buffer[i + c];
                        samples.Add(sum / channels);
                    }
                }

                _audioReader.Position = originalPosition;
                return samples.ToArray();
            });

            IsLoading = false;
            FileLoaded?.Invoke(this, EventArgs.Empty);
            _waveOut.Play();
        }

        public void TogglePlayback()
        {
            if (_waveOut != null)
            {
                if (_waveOut.PlaybackState == PlaybackState.Playing)
                    _waveOut.Pause();
                else
                    _waveOut.Play();
            }
        }

        public void Dispose()
        {
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _audioReader?.Dispose();
        }
    }
}