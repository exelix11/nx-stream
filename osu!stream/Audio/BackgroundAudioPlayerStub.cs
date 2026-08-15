using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using osum.Helpers;

namespace osum.Audio
{
    internal class BackgroundAudioPlayerStub : BackgroundAudioPlayer
    {
        DateTime started;
        
		public override float CurrentPower => 1;
        public override bool IsElapsing => true;

        /// <summary>
        /// Plays the loaded audio.
        /// </summary>
        /// <returns></returns>
        public override bool Play()
        {            
            started = DateTime.Now;
            return true;
        }

        /// <summary>
        /// Stops the playing audio.
        /// </summary>
        /// <returns></returns>
        public override bool Stop(bool reset = true)
        {
            if (reset)
                started = default;

            return true;
        }

        /// <summary>
        /// Updates this instance. Called every frame when loaded as a component.
        /// </summary>
        public override void Update()
        {
            
        }
        
        public override bool Load(byte[] audio, bool looping, string identifier = null)
        {
            return true;
        }

        public override bool Unload()
        {
            return true;
        }

        public override double CurrentTime
        {
            get
            {
                if (started == default) return 0;
                return (DateTime.Now - started).TotalSeconds;
            }
        }

        public override bool Pause()
        {
            return true;
        }

        public override bool SeekTo(int milliseconds)
        {
            return true;
        }

		protected override void updateVolume()
		{
            
		}
    }
}