using OpenTK.Graphics;
using osum.Audio;
using osum.GameModes.Play.Components;
using osum.GameModes.SongSelect;
using osum.GameplayElements;
using osum.GameplayElements.Beatmaps;
using osum.GameplayElements.HitObjects;
using osum.GameplayElements.HitObjects.Osu;
using osum.Graphics.Sprites;

namespace osum.GameModes.Play
{
	internal class OffsetTest : Player
	{
		private BackButton backButton;
		private pButton autoPlayButton;

		public override void Initialize()
		{
            Difficulty = Difficulty.Easy;
			Beatmap = null;

			AudioEngine.Music.Load("test.mp3", true);

			base.Initialize();

			Beatmap = new Beatmap();
			Beatmap.ControlPoints.Add(new ControlPoint(0, 50, TimeSignatures.SimpleQuadruple, SampleSet.Normal, CustomSampleSet.Default, 100, true, false));

			// Remove the pause button
			topMostSpriteManager.Clear();

			GameBase.Scheduler.Add(delegate
			{
				backButton = new BackButton(delegate
				{
					Director.ChangeMode(OsuMode.MainMenu);
				}, true);
				backButton.FadeInFromZero(500);
				topMostSpriteManager.Add(backButton);
			}, 500);
			
			autoPlayButton = new pButton("Autoplay", new Vector2(100,30), new(), Color4.White, delegate { 
				Autoplay = !Autoplay;
				autoPlayButton.Colour = Autoplay ? Color4.Green : Color4.White;
			});

			// Both size and default color behave differently when assigned manually...
			autoPlayButton.s_BackingPlate.Scale = new Vector2(0.5f, 1);
			autoPlayButton.Colour = Color4.White;

			topMostSpriteManager.Add(autoPlayButton);

			LoadMap();
		}

		public override void Dispose()
		{
			// Important or map selection from the main menu crashes
			Beatmap = null;
			base.Dispose();
		}

		internal override bool Pause(bool showMenu)
		{
			return false;
		}

		private void prepareInteract()
		{
			resetScore();
			playfieldBackground.ChangeColour(PlayfieldBackground.COLOUR_STANDARD, false);

			Difficulty = Difficulty.Easy;

			AudioEngine.Music.SeekTo(0);
			Resume();
			loadBeatmap();
		}

		public override void Update()
		{
			base.Update();

			if (HitObjectManager.AllNotesHit)
				LoadMap();
		}

  		protected override bool CheckForCompletion()
        {
            return false;
        } 
		
		protected override void UpdateStream()
        {
        }

		void LoadMap()
		{
			prepareInteract();

			const int x1 = 190;
			const int x2 = 512 - 190;
			const int y1 = 120;
			const int y2 = 384 - 120;

			HitObjectManager.Add(new HitCircle(HitObjectManager, new Vector2(x1, y1), 3000, true, 0, HitObjectSoundType.Normal), Difficulty);
			HitObjectManager.Add(new HitCircle(HitObjectManager, new Vector2(x2, y1), 3500, false, 0, HitObjectSoundType.Normal), Difficulty);
			HitObjectManager.Add(new HitCircle(HitObjectManager, new Vector2(x2, y2), 4000, false, 0, HitObjectSoundType.Normal), Difficulty);
			HitObjectManager.Add(new HitCircle(HitObjectManager, new Vector2(x1, y2), 4500, false, 0, HitObjectSoundType.Normal), Difficulty);
			HitObjectManager.Add(new HitCircle(HitObjectManager, new Vector2(x1, y1), 5000, false, 0, HitObjectSoundType.Normal), Difficulty);
			HitObjectManager.Add(new HitCircle(HitObjectManager, new Vector2(x2, y1), 5500, false, 0, HitObjectSoundType.Normal), Difficulty);
			HitObjectManager.Add(new HitCircle(HitObjectManager, new Vector2(x2, y2), 6000, false, 0, HitObjectSoundType.Finish), Difficulty);
			HitObjectManager.Add(new HitCircle(HitObjectManager, new Vector2(x1, y2), 6500, true, 0, HitObjectSoundType.Normal), Difficulty);
			HitObjectManager.Add(new HitCircle(HitObjectManager, new Vector2(x1, y1), 7000, false, 0, HitObjectSoundType.Normal), Difficulty);
			HitObjectManager.Add(new HitCircle(HitObjectManager, new Vector2(x2, y1), 7500, false, 0, HitObjectSoundType.Normal), Difficulty);
			HitObjectManager.Add(new HitCircle(HitObjectManager, new Vector2(x2, y2), 8000, false, 0, HitObjectSoundType.Normal), Difficulty);
			HitObjectManager.Add(new HitCircle(HitObjectManager, new Vector2(x1, y2), 8500, false, 0, HitObjectSoundType.Normal), Difficulty);
			HitObjectManager.Add(new HitCircle(HitObjectManager, new Vector2(x1, y1), 9000, false, 0, HitObjectSoundType.Normal), Difficulty);
			HitObjectManager.Add(new HitCircle(HitObjectManager, new Vector2(x2, y1), 9500, false, 0, HitObjectSoundType.Normal), Difficulty);
			HitObjectManager.Add(new HitCircle(HitObjectManager, new Vector2(x2, y2), 10000, false, 0, HitObjectSoundType.Finish), Difficulty);

			HitObjectManager.PostProcessing();

			HitObjectManager.SetActiveStream(Difficulty.Easy);

			Logging.Write("OffsetTest started.");
		}
	}
}