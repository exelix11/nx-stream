using System;
using System.ComponentModel;
using Libnx;
using OpenTK.Graphics;
using osum.GameModes;
using osum.GameModes.Play;

namespace osum.Support.Libnx
{
    public class GameWindowLibnx : GameWindow
    {
        public bool Running = true;
        IDisposable focusGuard;

        public GameWindowLibnx()
            : base(1280, 720, GraphicsMode.Default, "osu!stream", GameWindowFlags.Fullscreen, DisplayDevice.Default, 3, 0, GraphicsContextFlags.Default)
        {
            VSync = VSyncMode.On;
        }

        void AppletFocusChange(Applet.AppletFocusState state)
        {
            Logging.Write($"Focus change detected: {state}");
            if (state == Applet.AppletFocusState.InFocus) 
                Applet.SetSuspendOnFocusLoss(false);
            else 
            {
                (Director.CurrentMode as Player)?.Pause();
                Applet.SetSuspendOnFocusLoss(true);
            }
        }

        void AppletOperationModeChange(Applet.AppletOperationMode mode)
        {
            return;

            Logging.Write($"Operation mode change detected: {mode}");
            (Director.CurrentMode as Player)?.Pause();
            
            var isDocked = mode == Applet.AppletOperationMode.Console;
            var message = "This game can only be played with a touch screen. Please undock your Switch to continue playing.";
            
            if (isDocked && GameBase.NotificationQueue.Count == 0)
                GameBase.Notify(message);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Trick to get suspend notifications: ask the os to not suspend us,
            // When the notification that the app is in the background arrives pause the game then ask to suspend again.
            // When we get focus back ask again not to suspend for the next time.
            // If we don't do this we only get OnFocus notifications when the game resumes which are too late to pause the game due to the clocks advancing
            Applet.SetSuspendOnFocusLoss(false);

            focusGuard = Applet.HookAppletEvents(
                focusChange: AppletFocusChange,
                operationModeChange: AppletOperationModeChange);

            MakeCurrent();
            
            GameBase.Instance.Initialize();

            // In case we start docked
            AppletOperationModeChange(Applet.appletGetOperationMode());
        }

		protected override void OnUnload(EventArgs e)
		{
            focusGuard?.Dispose();
			base.OnUnload(e);
		}

        protected override void OnClosing(CancelEventArgs e)
        {
            if (Director.CurrentOsuMode == OsuMode.PlayTest || Director.CurrentOsuMode == OsuMode.PositioningTest)
            {
                e.Cancel = true;
                Director.ChangeMode(OsuMode.PositioningTest, null);
                return;
            }

            if (Director.CurrentOsuMode != OsuMode.MainMenu)
            {
                e.Cancel = true;
                Director.ChangeMode(OsuMode.MainMenu, new FadeTransition(200, 400));
            }

            GameBase.Config.SaveConfig();
            base.OnClosing(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (GameBase.Instance != null) GameBase.Instance.SetupScreen();
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);
            
            if (GameBase.Instance != null)
                GameBase.Instance.Update();
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            if (GameBase.Instance != null) GameBase.Instance.Draw();
            else
            {
                // Paint the screen red
                GL.ClearColor(1f, 0f, 0f, 1f);
                GL.Clear(ClearBufferMask.ColorBufferBit);
            }

            // display
            SwapBuffers();
        }
    }
}