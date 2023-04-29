using NAudio.Wave;
using System;
using System.Threading.Tasks;

namespace Axios.data
{
    public class AudioPlayer
    {
        public float LastVolume { get; set; }
        public const float DefaultVolume = 0.05f;

        private readonly WaveOutEvent _waveOut;
        private MediaFoundationReader _audioReader;
        private readonly object _initLock;
        private readonly object _playLock;
        private bool _isInitializing;

        /// <summary>
        /// Initializes a new instance of the AudioPlayer class with a specified URL.
        /// </summary>
        /// <param name="url">The URL to play audio from.</param>
        public AudioPlayer(string url)
        {
            _isInitializing = false;
            _initLock = new object();
            _playLock = new object();
            _waveOut = new WaveOutEvent();
            Task initTask = Task.Run(() =>
            {
                try { _audioReader = new MediaFoundationReader(url); }
                catch (Exception e) { throw new Exception("Failed to create an audio reader.", e); }
            });

            if (!initTask.Wait(TimeSpan.FromSeconds(3))) { throw new TimeoutException(); }
            _waveOut.Volume = DefaultVolume;
        }

        /// <summary>
        /// Starts playing the audio.
        /// </summary>
        public void StartPlaying()
        {
            if (IsPlaying()) { return; }
            if (_isInitializing) { return; }

            _isInitializing = true;

            lock (_initLock)
            {
                Task.Run(() =>
                {
                    lock (_playLock)
                    {
                        _waveOut.Init(_audioReader);
                        _waveOut.Play();
                        _isInitializing = false;
                    }
                });
            }
        }

        /// <summary>
        /// Resumes playing the audio.
        /// </summary>
        public void ResumePlaying()
        {
            _waveOut.Init(_audioReader);
            _waveOut.Play();
        }

        /// <summary>
        /// Ends and disposes the audio player object.
        /// </summary>
        public void EndAndDispose()
        {
            _waveOut.Stop();
            _waveOut.Dispose();
            _audioReader.Dispose();
        }

        /// <summary>
        /// Pauses the audio playback.
        /// </summary>
        public void PausePlaying() => _waveOut.Pause();

        /// <summary>
        /// Returns a value indicating whether the audio is currently playing.
        /// </summary>
        public bool IsPlaying() => _waveOut.PlaybackState == PlaybackState.Playing;

        /// <summary>
        /// Sets the volume of the audio player.
        /// </summary>
        /// <param name="volume">The volume level to set.</param>
        public void SetVolume(float volume) => _waveOut.Volume = volume;

        /// <summary>
        /// Returns the current volume of the audio player.
        /// </summary>
        public float GetVolume() => _waveOut.Volume;
    }
}
