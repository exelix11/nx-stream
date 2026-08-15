using System.Drawing;
using System.IO;
using osum.AssetManager;
using osum.Audio;
using osum.GameModes;
using osum.Input;
using osum.Input.Sources;

namespace osum.Support.Libnx
{
    public class GameBaseLibnx : GameBase
    {
        public GameWindowLibnx Window;

        override public string PathConfig => "sdmc:/osu-stream/";

        public GameBaseLibnx(OsuMode mode = OsuMode.Unknown) : base(mode)
        {
            Directory.SetCurrentDirectory(PathConfig);
            // TODO: Is switch a handheld or a tablet? this makes the game behave like it's a tablet
            IsHandheld = false;
        }

        public override void Run()
        {
            Window = new GameWindowLibnx();
            Window.Run();
            Director.CurrentMode.Dispose();
        }

        protected override BackgroundAudioPlayer InitializeBackgroundAudio()
        {
            return new BackgroundAudioPlayerLibnx();
        }

        protected override SoundEffectPlayer InitializeSoundEffects()
        {
            return new SoundEffectPlayerOpenAL();
        }

        protected override NativeAssetManager InitializeAssetManager()
        {
            return new NativeAssetManager("sdmc:/osu-stream/");
        }

		public override void OpenUrl(string url)
		{
			Notify(url);
		}

        protected override void InitializeInput()
        {
            var input = new InputSourceLibnx();
            InputManager.AddSource(input);
            Components.Add(input);
        }

        public override void SetupScreen()
        {
            NativeSize = new Size(1280, 720);
            base.SetupScreen();
        }

        public void Exit()
        {
            Window.Running = false;
        }
    }
}