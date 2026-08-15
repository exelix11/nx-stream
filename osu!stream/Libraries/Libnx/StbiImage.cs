using System;
using System.Runtime.InteropServices;

namespace osum
{
	public class StbiImage : IDisposable
	{
		[DllImport("osu_internal")]
		static extern IntPtr stbi_load_from_memory(in byte buffer, int len, out int x, out int y, out int comp, int req_comp);
	
		[DllImport("osu_internal")]
		static extern void stbi_image_free(IntPtr retval_from_stbi_load);

		public readonly int Width;
		public readonly int Height;
		public readonly int Channels;
		public IntPtr Data { get; private set; }

		public StbiImage(byte[] buffer, int req_comp = 0)
		{
			if (buffer == null)
				throw new ArgumentNullException(nameof(buffer));

			int x, y, comp;
			Data = stbi_load_from_memory(in buffer[0], buffer.Length, out x, out y, out comp, req_comp);
			if (Data == IntPtr.Zero)
				throw new Exception("Failed to load image");

			Width = x;
			Height = y;
			Channels = comp;
		}

		public void Dispose()
		{
			if (Data != IntPtr.Zero)
			{
				stbi_image_free(Data);
				Data = IntPtr.Zero;
			}

			GC.SuppressFinalize(this);
		}
	}
}