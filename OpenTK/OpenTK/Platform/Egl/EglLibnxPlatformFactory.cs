using System;
using System.Collections.Generic;
using System.Text;

namespace OpenTK.Platform.Libnx
{
	using System.ComponentModel;
	using System.Drawing;
	using System.Runtime.InteropServices;
	using Graphics;
	using OpenTK.Input;
	using OpenTK.Platform.Egl;
	using OpenTK.Platform.Libnx;

	class LibnxStub : IKeyboardDriver, IInputDriver
	{

	}

	class LibnxDefaultWindow : EglWindowInfo, INativeWindow
	{
		[DllImport("libnx")]
		private static extern bool appletMainLoop();

		[DllImport("libnx")]
		internal static extern nint nwindowGetDefault();

		public LibnxDefaultWindow(nint handle, nint display, nint surface) : base(handle, display, surface) { }

		public string Title { get; set; }
		public bool Focused => true;
		public bool Visible { get; set; }
		public bool Exists => true;

		public IWindowInfo WindowInfo => this;

		public WindowState WindowState { get; set; }
		public WindowBorder WindowBorder { get; set; }
		public Rectangle Bounds { get => new Rectangle(0, 0, 1280, 720); set { } }
		public Point Location { get; set; }
		public Size Size { get => new Size(1280, 720); set { } }
		public int X { get; set; }
		public int Y { get; set; }
		public int Width { get => 1280; set { } }
		public int Height { get => 720; set { } }
		public Rectangle ClientRectangle { get => new Rectangle(0, 0, 1280, 720); set { } }
		public Size ClientSize { get => new Size(1280, 720); set { } }

		public IInputDriver InputDriver => new LibnxStub();

		public event EventHandler<EventArgs> Move;
		public event EventHandler<EventArgs> Resize;
		public event EventHandler<CancelEventArgs> Closing;
		public event EventHandler<EventArgs> Closed;
		public event EventHandler<EventArgs> Disposed;
		public event EventHandler<EventArgs> IconChanged;
		public event EventHandler<EventArgs> TitleChanged;
		public event EventHandler<EventArgs> VisibleChanged;
		public event EventHandler<EventArgs> FocusedChanged;
		public event EventHandler<EventArgs> WindowBorderChanged;
		public event EventHandler<EventArgs> WindowStateChanged;
		public event EventHandler<KeyPressEventArgs> KeyPress;
		public event EventHandler<EventArgs> MouseLeave;
		public event EventHandler<EventArgs> MouseEnter;

		public void Close()
		{
			Closed?.Invoke(this, EventArgs.Empty);
		}

		public Point PointToClient(Point point)
		{
			return point;
		}

		public Point PointToScreen(Point point)
		{
			return point;
		}

		public void ProcessEvents()
		{
			if (!appletMainLoop())
				Close();
		}
	}

	class EglLibnxFactory : IPlatformFactory
	{
		[DllImport("glad")]
		private static extern nint gladLoadGL();

		static bool GladLoaded = false;
		static object GladLoadLock = new object();

		const int EGL_CONTEXT_OPENGL_PROFILE_MASK_KHR = 0x30FD;
		const int EGL_CONTEXT_OPENGL_CORE_PROFILE_BIT_KHR = 0x00000001;
		const int EGL_CONTEXT_OPENGL_COMPATIBILITY_PROFILE_BIT_KHR = 0x00000002;
		const int EGL_CONTEXT_MAJOR_VERSION_KHR = 0x3098;
		const int EGL_CONTEXT_MINOR_VERSION_KHR = 0x30FB;

		public bool IsLegacyContext { get; private set; }

		public IDisplayDeviceDriver CreateDisplayDeviceDriver()
		{
			return new LibnxDisplayDevice();
		}

		public GraphicsContext.GetCurrentContextDelegate CreateGetCurrentGraphicsContext()
		{
			return (GraphicsContext.GetCurrentContextDelegate)delegate
			{
				return new ContextHandle(Egl.GetCurrentContext());
			};
		}

		public IGraphicsContext CreateGLContext(GraphicsMode mode, IWindowInfo windowIface, IGraphicsContext shareContext, bool directRendering, int major, int minor, GraphicsContextFlags flags)
		{
			if (windowIface is not LibnxDefaultWindow window)
				throw new NotSupportedException("Only the default window is supported on this platform.");

			if (!Egl.Initialize(window.Display, out _, out _))
				throw new GraphicsContextException(String.Format("Failed to initialize libnx EGL, error {0}.", Egl.GetError()));

			if (!Egl.BindAPI(Egl.OPENGL_API))
				throw new GraphicsContextException(String.Format("Failed to bind OpenGL ES API, error {0}.", Egl.GetError()));

			IsLegacyContext = major < 4;

			var config = new IntPtr[1];

			// As per switch-examples
			Egl.ChooseConfig(window.Display, [
				Egl.RENDERABLE_TYPE, Egl.OPENGL_BIT,
				Egl.RED_SIZE,     8,
				Egl.GREEN_SIZE,   8,
				Egl.BLUE_SIZE,    8,
				Egl.ALPHA_SIZE,   8,
				Egl.DEPTH_SIZE,   24,
				Egl.STENCIL_SIZE, 8,
				Egl.NONE
			], config, 1, out var num_config);

			if (num_config == 0)
				throw new GraphicsContextException(String.Format("Failed to choose EGL config, error {0}.", Egl.GetError()));

			window.CreateWindowSurface(config[0]);

			IntPtr context = IntPtr.Zero;

			if (IsLegacyContext) 
			{
				// Mesa compatibility mode
				context = Egl.CreateContext(window.Display, config[0], IntPtr.Zero, [
					EGL_CONTEXT_OPENGL_PROFILE_MASK_KHR, EGL_CONTEXT_OPENGL_COMPATIBILITY_PROFILE_BIT_KHR,
					EGL_CONTEXT_MAJOR_VERSION_KHR, major,
					EGL_CONTEXT_MINOR_VERSION_KHR, minor,
					Egl.NONE
				]);
			}
			else
			{
				// As per switch-examples
				context = Egl.CreateContext(window.Display, config[0], IntPtr.Zero, [
					EGL_CONTEXT_OPENGL_PROFILE_MASK_KHR, EGL_CONTEXT_OPENGL_CORE_PROFILE_BIT_KHR,
					EGL_CONTEXT_MAJOR_VERSION_KHR, major,
					EGL_CONTEXT_MINOR_VERSION_KHR, minor,
					Egl.NONE
				]);
			}

			if (context == IntPtr.Zero)
				throw new GraphicsContextException(String.Format("Failed to create EGL context, error {0}.", Egl.GetError()));

			Egl.MakeCurrent(window.Display, window.Surface, window.Surface, context);

			lock (GladLoadLock)
			{
				if (!GladLoaded)
				{
					// Switch examples does this after init egl, do we need it?
					gladLoadGL();
					GladLoaded = true;
				}
			}

			return new LibnxGraphicsContext(new ContextHandle(context), window, null, major, minor, flags);
		}

		public IGraphicsContext CreateGLContext(ContextHandle handle, IWindowInfo window, IGraphicsContext shareContext, bool directRendering, int major, int minor, GraphicsContextFlags flags)
		{
			throw new NotImplementedException();
		}

		public IGraphicsMode CreateGraphicsMode()
		{
			return new EglGraphicsMode();
		}

		public virtual OpenTK.Input.IKeyboardDriver CreateKeyboardDriver()
		{
			return new LibnxStub();
		}

		public INativeWindow CreateNativeWindow(int x, int y, int width, int height, string title, GraphicsMode mode, GameWindowFlags options, DisplayDevice device)
		{
			var display = Egl.GetDisplay(IntPtr.Zero);
			return new LibnxDefaultWindow(LibnxDefaultWindow.nwindowGetDefault(), display, IntPtr.Zero);
		}
	}
}
