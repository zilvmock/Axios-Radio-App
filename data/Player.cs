using NAudio.Wave;
using System;
using System.Threading.Tasks;

namespace Axios.data
{
    public class Player
    {
        public static float DefaultVolume = 0.05f;
        public float LastVolume { get; set; }

        private static WaveOutEvent waveOut;
        private static MediaFoundationReader audioReader;

        private bool isInitializing;
        private object initLock;

        public Player(string url)
        {
            isInitializing = false;
            initLock = new object();
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
            waveOut.Volume = DefaultVolume;
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

            lock (initLock)
            {
                if (isInitializing)
                {
                    return;
                }

                isInitializing = true;
            }

            Task.Run(() =>
            {
                waveOut.Stop();
                waveOut.Init(audioReader);
                waveOut.Play();

                lock (initLock)
                {
                    isInitializing = false;
                }
            });
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
