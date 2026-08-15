using osum.AssetManager;

namespace osum.Audio
{
    /// <summary>
    /// Play short-lived sound effects, and handle caching.
    /// </summary>
    public class SoundEffectPlayerStub : SoundEffectPlayer
    {
        int counter = 0;

        public SoundEffectPlayerStub()
        {
            sourceInfo = new Source[MAX_SOURCES];
            for (int i = 0; i < MAX_SOURCES; i++)
                sourceInfo[i] = new SourceStub();
        }

        private void CheckUnload()
        {
            
        }

        /// <summary>
        /// Loads the specified sound file.
        /// </summary>
        /// <param name="filename">Filename of a 44khz 16-bit wav sample.</param>
        /// <returns>-1 on error, bufferId on success.</returns>
        public override int Load(string filename)
        {
#if DEBUG
            if (!NativeAssetManager.Instance.FileExists(filename)) return -1;
#endif
            byte[] bytes = NativeAssetManager.Instance.GetFileBytes(filename);

            return ++counter;
        }
    }

    public class SourceStub : Source
    {
        public SourceStub()
            : base(-1)
        {
        }

        bool playing;

        public override int BufferId {get; set;}
        public override bool Playing => playing;
        private float audioFrequency = -1;
        public override float Pitch => 1;

        internal override void Play()
        {
            playing = true;
        }

        internal override void Stop()
        {
            playing = false;
        }

        internal override void DeleteBuffer()
        {
            base.DeleteBuffer();
        }

        public override bool Looping {get; set;}

        public override float Volume {get; set;}
    }
}