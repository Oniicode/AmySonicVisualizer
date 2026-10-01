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
        private float[] _monoSamples = [];

        // --- Smooth Playback Tracking Fields ---
        private double _seekTime = 0;
        private long _bytesPlayedAtSeek = 0;
        private double _visualPauseTime = 0;

        // --- Playback State Tracking ---
        private PlaybackState _lastKnownState = PlaybackState.Stopped;

        // --- Volume Control ---
        private float _volume = 1.0f;

        public float[] MonoSamples => _monoSamples;
        public int SampleRate => _audioReader?.WaveFormat.SampleRate ?? 44100;

        public bool IsLoaded => _audioReader != null;
        public bool IsLoading { get; private set; }
        public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;

        // Expose current exact state if subscribers need more than a boolean
        public PlaybackState CurrentPlaybackState => _waveOut?.PlaybackState ?? PlaybackState.Stopped;

        public string? CurrentFilePath { get; private set; }

        public float Volume
        {
            get => _volume;
            set
            {
                float clampedValue = Math.Clamp(value, 0.0f, 1.0f);
                if (_volume != clampedValue)
                {
                    _volume = clampedValue;
                    if (_waveOut != null)
                    {
                        _waveOut.Volume = clampedValue;
                    }
                    VolumeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler? AudioLoading;
        public event EventHandler? FileLoading;
        public event EventHandler? FileLoaded;
        public event EventHandler? PlaybackStateChanged;
        public event EventHandler? VolumeChanged;

        public double CurrentTime
        {
            get
            {
                if (_audioReader == null)
                    return 0;

                // During load, fall back to reader to keep the high-speed scan animation
                if (IsLoading)
                    return _audioReader.CurrentTime.TotalSeconds;

                if (_waveOut != null)
                {
                    if (_waveOut.PlaybackState == PlaybackState.Playing)
                    {
                        // Hardware-synced, continuous smooth interpolation based on bytes actually played
                        long bytesPlayed = _waveOut.GetPosition() - _bytesPlayedAtSeek;
                        double secondsPlayed = (double)bytesPlayed / _audioReader.WaveFormat.AverageBytesPerSecond;
                        return Math.Clamp(_seekTime + secondsPlayed, 0.0, TotalTime);
                    }
                    else if (_waveOut.PlaybackState == PlaybackState.Paused)
                    {
                        // Return exact visual position to prevent "jumping forward" to the buffered read-ahead position
                        return _visualPauseTime;
                    }
                }

                // If stopped naturally or just opened
                return _audioReader.CurrentTime.TotalSeconds;
            }
            set
            {
                if (_audioReader != null)
                {
                    double clampedValue = Math.Clamp(value, 0.0, TotalTime);
                    _audioReader.CurrentTime = TimeSpan.FromSeconds(clampedValue);

                    // Reset smooth tracking fields to the new manual seek position
                    _seekTime = clampedValue;
                    _visualPauseTime = clampedValue;
                    if (_waveOut != null)
                    {
                        _bytesPlayedAtSeek = _waveOut.GetPosition();
                    }
                }
            }
        }

        public double TotalTime => _audioReader?.TotalTime.TotalSeconds ?? 1;

        public double Progress
        {
            get => Math.Clamp(CurrentTime / TotalTime, 0.0, 1.0);
            set => CurrentTime = value * TotalTime;
        }

        private void UpdatePlaybackState()
        {
            var currentState = CurrentPlaybackState;
            if (_lastKnownState != currentState)
            {
                _lastKnownState = currentState;
                PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e) 
            => UpdatePlaybackState();

        public async Task LoadAsync(string filePath)
        {
            IsLoading = true;
            AudioLoading?.Invoke(this, EventArgs.Empty);

            if (_waveOut != null)
            {
                _waveOut.PlaybackStopped -= OnPlaybackStopped; // Unhook to prevent leaks
                _waveOut.Stop();
                UpdatePlaybackState(); // Immediately notify if it was playing
                _waveOut.Dispose();
            }

            _audioReader?.Dispose();

            CurrentFilePath = filePath;
            _seekTime = 0;
            _bytesPlayedAtSeek = 0;
            _visualPauseTime = 0;

            FileLoading?.Invoke(this, EventArgs.Empty);

            _audioReader = new AudioFileReader(filePath);
            _waveOut = new WaveOutEvent();
            _waveOut.Volume = _volume; // Apply current volume when creating new WaveOutEvent
            _waveOut.PlaybackStopped += OnPlaybackStopped; // Hook up natural stop monitoring
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
                        for (int c = 0; c < channels; c++)
                            sum += buffer[i + c];
                        samples.Add(sum / channels);
                    }
                }

                _audioReader.Position = originalPosition;
                return samples.ToArray();
            });

            IsLoading = false;
            FileLoaded?.Invoke(this, EventArgs.Empty);

            // Capture perfect zero baseline before play
            _seekTime = 0;
            _bytesPlayedAtSeek = _waveOut.GetPosition();
            _waveOut.Play();
            UpdatePlaybackState(); // Fire state change for Play
        }

        public void TogglePlayback()
        {
            if (_waveOut == null)
                return;

            if (_waveOut.PlaybackState == PlaybackState.Playing)
            {
                _visualPauseTime = CurrentTime; // Freeze perfectly in place
                _waveOut.Pause();
            }
            else
            {
                // Resync base values so playback smoothly picks up from the visual pause cursor
                if (_waveOut.PlaybackState == PlaybackState.Paused)
                {
                    _seekTime = _visualPauseTime;
                }
                else
                {
                    _seekTime = CurrentTime;
                }

                _bytesPlayedAtSeek = _waveOut.GetPosition();
                _waveOut.Play();
            }

            // Sync up the latest event after manually modifying state
            UpdatePlaybackState();
        }

        public void Dispose()
        {
            if (_waveOut != null)
            {
                _waveOut.PlaybackStopped -= OnPlaybackStopped;
                _waveOut.Stop();
            }
            _waveOut?.Dispose();
            _audioReader?.Dispose();

            UpdatePlaybackState(); // Catch the final transition to Stopped/Disposed
        }
    }
}