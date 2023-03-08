using NAudio.Wave;
using System;
using System.Threading.Tasks;

namespace Axios.data
{
    internal class Player
    {
        public static float defaultVolume = 0.05f;

        private static WaveOutEvent waveOut;
        private static MediaFoundationReader audioReader;

        private bool isInitializing = false;
        private object initLock = new object();

        public Player(string url)
        {
            waveOut = new WaveOutEvent();
            Task initTask = Task.Run(() =>
            {
                try
                {
                    audioReader = new MediaFoundationReader(url);
                }
                catch(Exception) { throw new Exception(); }
            });
            if (!initTask.Wait(TimeSpan.FromSeconds(3)))
            {
                throw new TimeoutException();
            }
            waveOut.Volume = defaultVolume;
        }

        public void StartPlaying()
        {
            if (IsPlaying()) return;
            lock (initLock)
            {
                if (!isInitializing)
                {
                    isInitializing = true;
                    Task.Run(() =>
                    {
                        lock (initLock)
                        {
                            waveOut.Init(audioReader);
                            waveOut.Play();
                            isInitializing = false;
                        }
                    });
                }
            }
        }

        public void PausePlaying()
        {
            if (waveOut == null) { return; }
            waveOut.Pause();
        }

        public void ResumePlaying()
        {
            if (waveOut == null || audioReader == null || IsPlaying()) { return; }
            if (!isInitializing)
            {
                lock (initLock)
                {
                    if (!isInitializing)
                    {
                        isInitializing = true;
                        Task.Run(() =>
                        {
                            lock (initLock)
                            {
                                waveOut.Stop();
                                waveOut.Init(audioReader);
                                waveOut.Play();
                                isInitializing = false;
                            }
                        });
                    }
                }
            }
        }


        public void EndPlaying()
        {
            if (waveOut == null || audioReader == null) { return; }
            waveOut.Stop();
            waveOut.Dispose();
            audioReader.Dispose();
        }

        public bool IsPlaying()
        {
            if (waveOut == null) { return false; }
            if (waveOut.PlaybackState == PlaybackState.Playing) { return true; }
            return false;
        }

        public void SetVolume(float volume)
        {
            if (waveOut == null) { return; }
            waveOut.Volume = volume;
        }

        public float GetVolume()
        {
            if (waveOut == null) { return 0f; }
            return waveOut.Volume;
        }

    }
}
