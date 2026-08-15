using System;
using System.ComponentModel;
using System.Diagnostics;
using Libnx;
using OpenTK.Graphics;
using osum.GameModes;
using osum.GameModes.Play;
using osum.Helpers;

namespace osum.Support.Libnx
{
    public class GameWindowLibnx : GameWindow
    {
        public bool Running = true;

        const int DOCK_CHECK_INTERVAL_MS = 4000;
        bool isDocked = false;
        int lastDockCheckTime = 0;

        public GameWindowLibnx()
            : base(1280, 720, GraphicsMode.Default, "osu!stream", GameWindowFlags.Fullscreen, DisplayDevice.Default, 3, 0, GraphicsContextFlags.Default)
        {
            VSync = VSyncMode.On;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            MakeCurrent();
            
            GameBase.Instance.Initialize();
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

            var now = Clock.GetTime(ClockTypes.Game);
            if (now > lastDockCheckTime + DOCK_CHECK_INTERVAL_MS)
            {
                lastDockCheckTime = now;
                var docked = Applet.GetDockStatus();
                if (!this.isDocked && docked)
                {
                    (Director.CurrentMode as Player)?.Pause();
                    GameBase.Notify("This game can only be played in portable mode. Please undock your Switch to continue playing.");
                }
                
                this.isDocked = docked;
            }
            
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