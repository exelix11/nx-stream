using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;
using OpenTK.Graphics;
using osum.Graphics.Sprites;
using System.Linq;

namespace osum.Graphics.Renderers
{
    public record TextStyle(bool Bold, float Size, uint Color, float Spacing, bool HorizontalCenter);

    public record struct RasterizedText(TextureGl Texture, float AdvancedPixels, int LineCount, float LineHeight, int TruncatedAt)
    {
        public static RasterizedText Empty => new(null, 0f, 0, 0f, -1);
    }

    // Very hacky stuff here, we use fontstash to render it to a fbo and then get a texture we can give the to the pText.
    // I did try implementing a pText that overrides Draw() by calling directly to the gl fontstash backend but couldn't get it to work well, in particular with the text positioning and bounding box calculation that pSprite expects.
    // This is partly ai assisted so probably not everything is strictly needed.
    // Note that to better manage state i also did some manual modifications to the fontstash backend to avoid it doing some opengl state changes that instead we do here.
    internal unsafe class NativeTextRendererLibnx : NativeTextRenderer
    {
        private readonly IntPtr fons;
        private readonly IntPtr boldFont, normalFont;

        private int fbo;

        // Since fontstash doesn't do layout we also need to manually figure out line wrapping.
        // To reduce allocations we have a single utf8 buffer cache that grows as needed,
        // then we provide font stash the raw start,end pointers.
        private byte[] utf8Buffer = new byte[256];
        private readonly List<Line> parsedLiens = new();
        private readonly GlState glStateBackup = new();

        private struct Line
        {
            public int Start, End;      // byte offsets into _utf8, End exclusive
            public float MinX, MaxX;      // ink bounds relative to the pen
            public float Advance;
        }

        public int Padding { get; set; } = 4;

        public NativeTextRendererLibnx()
        {
            fons = FontStash.glfonsCreate(2048, 2048, FontStash.ZeroTopLeft);
            boldFont = FontStash.fonsAddFont(fons, "CondensedExtraBold", Path.GetFullPath(@"Skins/Default/Futura-CondensedExtraBold.ttf"));
            normalFont = FontStash.fonsAddFont(fons, "Medium", Path.GetFullPath(@"Skins/Default/Futura-Medium.ttf"));
            GL.GenFramebuffers(1, out fbo);
        }

        internal override pTexture CreateText(string text, float size, Vector2 restrictBounds, Color4 colour, bool shadow,
            bool bold, bool underline, TextAlignment alignment, bool forceAa,
            out Vector2 measured,
            Color4 background, Color4 border, int borderWidth, bool measureOnly, string fontFace)
        {
            size *= 2;

            var center = alignment == TextAlignment.Centre;
            var style = new TextStyle(bold, size, FontStash.Rgba(colour.R, colour.G, colour.B, colour.A), 0f, center);
            var raster = Render(text, style, restrictBounds, measureOnly);

            // Best effort size, in practice it's probably wrong
            measured = new Vector2(raster.AdvancedPixels, raster.LineHeight * raster.LineCount);

            if (measureOnly || raster.Texture == null)
            {
                Logging.Write($"Text '{text}' measured {measured.X}x{measured.Y} with {raster.LineCount} lines, truncated at {raster.TruncatedAt}");
                return null;
            }

            // TODO: Not sure how much this is needed, it's used when constructing pTextures
            var width = (int)(raster.Texture.TextureWidth * 960f / GameBase.SpriteSheetResolution);
            var height = (int)(raster.Texture.TextureHeight * 960f / GameBase.SpriteSheetResolution);

            measured = new Vector2(width, height);
            Logging.Write($"Text '{text}' measured {measured.X}x{measured.Y} texture {raster.Texture.TextureWidth}x{raster.Texture.TextureHeight} with {raster.LineCount} lines, truncated at {raster.TruncatedAt}");

            raster.Texture.TextureWidth = width;
            raster.Texture.TextureHeight = height;
            return new pTexture(raster.Texture, width, height);
        }

        private void ApplyStyle(in TextStyle st)
        {
            FontStash.fonsClearState(fons);
            FontStash.fonsSetFont(fons, st.Bold ? boldFont : normalFont);
            FontStash.fonsSetSize(fons, st.Size);
            FontStash.fonsSetColor(fons, st.Color);
            FontStash.fonsSetSpacing(fons, st.Spacing);

            // Force alignment
            FontStash.fonsSetAlign(fons, FontStash.AlignLeft | FontStash.AlignBaseline);
        }

        public RasterizedText Render(string text, TextStyle style, Vector2 bounds = default, bool measureOnly = false)
        {
            if (string.IsNullOrEmpty(text)) return default;

            float maxW = bounds.X > 0f ? bounds.X : float.MaxValue;
            float maxH = bounds.Y > 0f ? bounds.Y : float.MaxValue;

            ApplyStyle(style);

            int lenBytes = Encoding.UTF8.GetMaxByteCount(text.Length);
            if (utf8Buffer.Length < lenBytes) utf8Buffer = new byte[lenBytes];
            lenBytes = Encoding.UTF8.GetBytes(text, 0, text.Length, utf8Buffer, 0);

            // Everything below here is AI, good luck.
            // i don't use a coding agent so it's only generated based on what i asked which is why not everything especially related to the gl context is needed.

            FontStash.fonsVertMetrics(fons, out float asc, out float desc, out float lineh);
            if (lineh <= 0f) lineh = asc - desc;

            fixed (byte* buf = utf8Buffer)
            {
                int stopByte = BuildLines(buf, lenBytes, maxW, maxH, asc, desc, lineh);
                int truncAt = stopByte < 0 ? -1 : Encoding.UTF8.GetCharCount(utf8Buffer, 0, stopByte);

                if (parsedLiens.Count == 0)
                    return RasterizedText.Empty;

                float minX = float.MaxValue, maxX = float.MinValue, widest = 0f;
                for (int i = 0; i < parsedLiens.Count; i++)
                {
                    var ln = parsedLiens[i];
                    if (ln.End <= ln.Start) continue;               // blank line, still spaced
                    if (ln.MinX < minX) minX = ln.MinX;
                    if (ln.MaxX > maxX) maxX = ln.MaxX;
                    if (ln.Advance > widest) widest = ln.Advance;
                }
                if (minX > maxX) { minX = 0f; maxX = 0f; }          // every line was blank

                int w = (int)Math.Ceiling(maxX - minX) + Padding * 2;
                int h = (int)Math.Ceiling(asc - desc + (parsedLiens.Count - 1) * lineh) + Padding * 2;
                if (w <= 0 || h <= 0)
                    return RasterizedText.Empty;

                TextureGl tex = null;

                if (!measureOnly)
                    tex = RenderLines(buf, w, h, Padding - minX, Padding + asc, lineh, style);

                return new RasterizedText(
                    Texture: tex,
                    AdvancedPixels: widest,
                    LineCount: parsedLiens.Count,
                    LineHeight: lineh,
                    TruncatedAt: truncAt);
            }
        }

        /// <summary>Fills _lines with the byte ranges to draw. Returns the byte offset where
        /// layout stopped for height reasons, or -1 if everything fit.</summary>
        private int BuildLines(byte* buf, int len, float maxW, float maxH,
                               float asc, float desc, float lineh)
        {
            parsedLiens.Clear();

            byte* end = buf + len;
            byte* p = buf;
            float* bounds = stackalloc float[4];

            while (true)
            {
                // Only emit a line whose full box fits: a glyph row sliced through the middle
                // looks worse than one that is simply absent.
                if (asc - desc + parsedLiens.Count * lineh > maxH)
                    return (int)(p - buf);

                // Extent of this hard line (up to \n or the end of the buffer). 
                byte* nl = p;
                while (nl < end && *nl != (byte)'\n') nl++;

                byte* lineEnd = nl;
                if (lineEnd > p && *(lineEnd - 1) == (byte)'\r') lineEnd--;   // CRLF

                // Emit one or more visual rows for it, wrapping at maxW.
                byte* seg = p;
                do
                {
                    byte* segEnd = maxW == float.MaxValue ? lineEnd : FindBreak(seg, lineEnd, maxW, bounds);

                    // Don't draw or measure the whitespace we broke on.
                    byte* drawEnd = segEnd;
                    while (drawEnd > seg && IsSpace(*(drawEnd - 1))) drawEnd--;

                    float advance = 0f, minx = 0f, maxx = 0f;
                    if (drawEnd > seg)
                    {
                        advance = FontStash.fonsTextBounds(fons, 0f, 0f, seg, drawEnd, bounds);
                        minx = bounds[0];
                        maxx = bounds[2];
                    }

                    parsedLiens.Add(new Line
                    {
                        Start = (int)(seg - buf),
                        End = (int)(drawEnd - buf),
                        MinX = minx,
                        MaxX = maxx,
                        Advance = advance,
                    });

                    seg = segEnd;

                    // Another wrapped row follows -- check it fits before committing to it.
                    if (seg < lineEnd && asc - desc + parsedLiens.Count * lineh > maxH)
                        return (int)(seg - buf);
                }
                while (seg < lineEnd);

                if (nl >= end) return -1;
                p = nl + 1;                                        // step past the \n
            }
        }

        /// <summary>End of the longest run of whole words that fits maxWidth. Falls back to a
        /// character break when a single word is wider than the line. The returned pointer is
        /// where the next row starts; trailing spaces before it are not meant to be drawn.</summary>
        private byte* FindBreak(byte* p, byte* end, float maxWidth, float* bounds)
        {
            byte* brk = FindCharBreak(p, end, maxWidth, bounds);
            if (brk >= end) return end;

            // Whitespace at the end of a row costs nothing, so let it hang past the right edge
            // instead of pushing the break back a whole word.
            while (brk < end && IsSpace(*brk)) brk++;
            if (brk >= end) return end;

            // Back up to just after the last break opportunity. If there is none, the run is one
            // over-long word: keep the character break so the caller still makes progress.
            byte* w = brk;
            while (w > p && !IsBreakAfter(*(w - 1))) w--;
            return w > p ? w : brk;
        }

        static bool IsSpace(byte c) => c == (byte)' ' || c == (byte)'\t';
        static bool IsBreakAfter(byte c) => IsSpace(c) || c == (byte)'-';

        /// <summary>Longest prefix of [p, end) whose advance width fits maxWidth. Always
        /// returns at least one codepoint so callers cannot spin.</summary>
        private byte* FindCharBreak(byte* p, byte* end, float maxWidth, float* bounds)
        {
            if (p >= end) return end;

            if (FontStash.fonsTextBounds(fons, 0f, 0f, p, end, bounds) <= maxWidth)
                return end;                                        // common case, one measure

            // Advance width only grows as the run gets longer, so binary search the codepoint
            // boundaries rather than stepping one character at a time.
            byte* lo = NextCodepoint(p, end);                      // forced minimum, always fits
            byte* hi = end;                                        // known not to fit

            while (true)
            {
                byte* mid = SnapDown(lo, lo + (long)(hi - lo) / 2);
                if (mid <= lo)
                {
                    mid = NextCodepoint(lo, hi);
                    if (mid >= hi) break;                          // no boundary strictly between
                }

                if (FontStash.fonsTextBounds(fons, 0f, 0f, p, mid, bounds) <= maxWidth)
                    lo = mid;
                else
                    hi = mid;
            }

            return lo;
        }

        /// <summary>Back up to the start of the codepoint containing q, never below min.</summary>
        private static byte* SnapDown(byte* min, byte* q)
        {
            while (q > min && (*q & 0xC0) == 0x80) q--;
            return q;
        }

        /// <summary>Start of the codepoint after the one at p.</summary>
        private static byte* NextCodepoint(byte* p, byte* end)
        {
            if (p >= end) return end;
            p++;
            while (p < end && (*p & 0xC0) == 0x80) p++;
            return p;
        }

        private TextureGl RenderLines(byte* buf, int w, int h, float originX, float firstBaseline, float lineh, TextStyle style)
        {
            glStateBackup.Save();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.TexEnv(TextureEnvTarget.TextureEnv, TextureEnvParameter.TextureEnvMode, (int)TextureEnvMode.Modulate);
            GL.GetInteger(GetPName.TextureBinding2D, out int prevTex);

            // Create our texture   
            var tex = new TextureGl(w, h) { Id = GL.GenTexture() };
            tex.Bind();
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8,
                          tex.potWidth, tex.potHeight, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            // Not AI comment: this is the relevant part that was removed from the gl backend and we do here instead
            // Mostly to integrate with the SpriteManager state management
            GL.EnableClientState(ArrayCap.ColorArray);
            SpriteManager.TexturesEnabled = true;

            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                    TextureTarget.Texture2D, tex.Id, 0);
            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
            {
                GL.BindTexture(TextureTarget.Texture2D, prevTex);
                glStateBackup.Restore();
                tex.Delete();
                throw new InvalidOperationException($"Text FBO incomplete ({w}x{h}): {status}");
            }

            GL.Viewport(0, 0, w, h);
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.ScissorTest);
            GL.Disable(EnableCap.CullFace);

            GL.MatrixMode(MatrixMode.Projection);
            GL.PushMatrix();
            GL.LoadIdentity();
            GL.Ortho(0.0, w, 0.0, h, -1.0, 1.0);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.PushMatrix();
            GL.LoadIdentity();

            GL.Enable(EnableCap.Blend);

            // Must not use pre-multiplied alpha.
            GL.BlendFuncSeparate(BlendingFactorSrc.One, BlendingFactorDest.Zero,
                                 BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);

            GL.ClearColor((style.Color & 0xFF) / 255f,
                          ((style.Color >> 8) & 0xFF) / 255f,
                          ((style.Color >> 16) & 0xFF) / 255f,
                          0f);

            float widest = parsedLiens.Max(x => x.Advance);

            for (int i = 0; i < parsedLiens.Count; i++)
            {
                var ln = parsedLiens[i];
                if (ln.End <= ln.Start) continue;                  // blank line, spacing only
                var offset = style.HorizontalCenter ? (widest - ln.Advance) / 2f : 0f;
                FontStash.fonsDrawText(fons, originX + offset, firstBaseline + i * lineh,
                                       buf + ln.Start, buf + ln.End);
            }

            GL.MatrixMode(MatrixMode.Projection);
            GL.PopMatrix();
            GL.MatrixMode(MatrixMode.Modelview);
            GL.PopMatrix();

            GL.DisableClientState(ArrayCap.ColorArray);

            GL.BindTexture(TextureTarget.Texture2D, prevTex);
            glStateBackup.Restore();

            return tex;
        }

        private class GlState
        {
            // Beware of AI slop, i asked to backup everything that could intefere with the rest of the engine.
            private readonly int[] _viewport = new int[4];
            private readonly float[] _color = new float[4];
            private readonly float[] _clear = new float[4];
            private int _fbo, _arrayBuffer, _vao;
            private int _bSrcRgb, _bDstRgb, _bSrcA, _bDstA;
            private bool _blend, _depth, _scissor, _tex2D, _cull;
            private bool _texture2DEnabled;
            private int _texEnvMode, _activeUnit;

            public void Save()
            {
                GL.GetInteger(GetPName.ActiveTexture, out _activeUnit);
                GL.GetTexEnv(TextureEnvTarget.TextureEnv, TextureEnvParameter.TextureEnvMode, out _texEnvMode);

                GL.GetInteger(GetPName.FramebufferBinding, out _fbo);
                GL.GetInteger(GetPName.ArrayBufferBinding, out _arrayBuffer);

                _vao = 0;
                GL.GetInteger(GetPName.VertexArrayBinding, out _vao);

                GL.GetInteger(GetPName.Viewport, _viewport);
                GL.GetFloat(GetPName.CurrentColor, _color);
                GL.GetFloat(GetPName.ColorClearValue, _clear);
                GL.GetInteger(GetPName.BlendSrcRgb, out _bSrcRgb);
                GL.GetInteger(GetPName.BlendDstRgb, out _bDstRgb);
                GL.GetInteger(GetPName.BlendSrcAlpha, out _bSrcA);
                GL.GetInteger(GetPName.BlendDstAlpha, out _bDstA);
                _blend = GL.IsEnabled(EnableCap.Blend);
                _depth = GL.IsEnabled(EnableCap.DepthTest);
                _scissor = GL.IsEnabled(EnableCap.ScissorTest);
                _tex2D = GL.IsEnabled(EnableCap.Texture2D);
                _cull = GL.IsEnabled(EnableCap.CullFace);

                _texture2DEnabled = SpriteManager.TexturesEnabled;
            }

            public void Restore()
            {
                SpriteManager.TexturesEnabled = _texture2DEnabled;

                GL.TexEnv(TextureEnvTarget.TextureEnv, TextureEnvParameter.TextureEnvMode, _texEnvMode);

                GL.BlendFuncSeparate((BlendingFactorSrc)_bSrcRgb, (BlendingFactorDest)_bDstRgb,
                                     (BlendingFactorSrc)_bSrcA, (BlendingFactorDest)_bDstA);
                GL.ClearColor(_clear[0], _clear[1], _clear[2], _clear[3]);
                GL.Color4(_color[0], _color[1], _color[2], _color[3]);

                if (!_blend) GL.Disable(EnableCap.Blend);
                if (_depth) GL.Enable(EnableCap.DepthTest);
                if (_scissor) GL.Enable(EnableCap.ScissorTest);
                if (_cull) GL.Enable(EnableCap.CullFace);
                if (_tex2D) GL.Enable(EnableCap.Texture2D); else GL.Disable(EnableCap.Texture2D);

                GL.Viewport(_viewport[0], _viewport[1], _viewport[2], _viewport[3]);
                GL.BindBuffer(BufferTarget.ArrayBuffer, _arrayBuffer);
                GL.BindVertexArray(_vao);
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);

                GL.ActiveTexture((TextureUnit)_activeUnit);
            }
        }
    }

    public static unsafe class FontStash
    {
        public const int ZeroTopLeft = 1;
        public const int AlignLeft = 1 << 0;
        public const int AlignBaseline = 1 << 6;

        [DllImport("osu_internal", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr glfonsCreate(int width, int height, int flags);

        [DllImport("osu_internal", CallingConvention = CallingConvention.Cdecl)]
        public static extern void glfonsDelete(IntPtr stash);

        [DllImport("osu_internal", CallingConvention = CallingConvention.Cdecl)]
        public static extern int fonsAddFont(IntPtr stash,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string path);

        [DllImport("osu_internal", CallingConvention = CallingConvention.Cdecl)]
        public static extern void fonsClearState(IntPtr stash);

        [DllImport("osu_internal", CallingConvention = CallingConvention.Cdecl)]
        public static extern void fonsSetFont(IntPtr stash, IntPtr font);

        [DllImport("osu_internal", CallingConvention = CallingConvention.Cdecl)]
        public static extern void fonsSetSize(IntPtr stash, float size);

        [DllImport("osu_internal", CallingConvention = CallingConvention.Cdecl)]
        public static extern void fonsSetColor(IntPtr stash, uint color);

        [DllImport("osu_internal", CallingConvention = CallingConvention.Cdecl)]
        public static extern void fonsSetSpacing(IntPtr stash, float spacing);

        [DllImport("osu_internal", CallingConvention = CallingConvention.Cdecl)]
        public static extern void fonsSetAlign(IntPtr stash, int align);

        [DllImport("osu_internal", CallingConvention = CallingConvention.Cdecl)]
        public static extern void fonsVertMetrics(IntPtr stash,
            out float ascender, out float descender, out float lineh);

        [DllImport("osu_internal", CallingConvention = CallingConvention.Cdecl)]
        public static extern float fonsDrawText(IntPtr stash, float x, float y, byte* str, byte* end);

        [DllImport("osu_internal", CallingConvention = CallingConvention.Cdecl)]
        public static extern float fonsTextBounds(IntPtr stash, float x, float y,
                                                  byte* str, byte* end, float* bounds);

        public static uint Rgba(byte r, byte g, byte b, byte a)
            => (uint)(r | (g << 8) | (b << 16) | (a << 24));

        public static uint Rgba(float r, float g, float b, float a)
            => Rgba((byte)(r * 255f), (byte)(g * 255f), (byte)(b * 255f), (byte)(a * 255f));
    }
}