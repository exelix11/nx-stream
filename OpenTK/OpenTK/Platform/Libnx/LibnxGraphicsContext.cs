using System;
using System.Collections.Generic;
using System.Text;

using OpenTK.Graphics;
using System.Runtime.InteropServices;
using System.Diagnostics;
using OpenTK.Platform.Egl;

namespace OpenTK.Platform.Libnx
{
	class LibnxGraphicsContext : Egl.EglContext
	{
		public LibnxGraphicsContext(ContextHandle handle, EglWindowInfo window, IGraphicsContext sharedContext, int major, int minor, GraphicsContextFlags flags) : base(handle, window, sharedContext, major, minor, flags)
		{
		}

		public override void LoadAll()
		{
			base.LoadAll();
			new OpenTK.Graphics.OpenGL.GL().LoadEntryPoints();
		}
	}
}