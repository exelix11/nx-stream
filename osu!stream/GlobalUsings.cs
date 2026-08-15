global using OpenTK;
global using osum.Debugging;

#if iOS || ANDROID
global using OpenTK.Graphics.ES11;
#if iOS
global using Foundation;
global using ObjCRuntime;
global using OpenGLES;
#endif

global using TextureTarget = OpenTK.Graphics.ES11.All;
global using TextureParameterName = OpenTK.Graphics.ES11.All;
global using EnableCap = OpenTK.Graphics.ES11.All;
global using ArrayCap = OpenTK.Graphics.ES11.All;
global using BlendingFactorSrc = OpenTK.Graphics.ES11.All;
global using BlendingFactorDest = OpenTK.Graphics.ES11.All;
global using PixelStoreParameter = OpenTK.Graphics.ES11.All;
global using VertexPointerType = OpenTK.Graphics.ES11.All;
global using ColorPointerType = OpenTK.Graphics.ES11.All;
global using ClearBufferMask = OpenTK.Graphics.ES11.All;
global using TexCoordPointerType = OpenTK.Graphics.ES11.All;
global using BeginMode = OpenTK.Graphics.ES11.All;
global using DepthFunction = OpenTK.Graphics.ES11.All;
global using MatrixMode = OpenTK.Graphics.ES11.All;
global using PixelInternalFormat = OpenTK.Graphics.ES11.All;
global using PixelFormat = OpenTK.Graphics.ES11.All;
global using PixelType = OpenTK.Graphics.ES11.All;
global using ShaderType = OpenTK.Graphics.ES11.All;
global using VertexAttribPointerType = OpenTK.Graphics.ES11.All;
global using ProgramParameter = OpenTK.Graphics.ES11.All;
global using ShaderParameter = OpenTK.Graphics.ES11.All;
#if iOS
global using CoreGraphics;
global using UIKit;
#endif

#elif GLES2
global using OpenTK.Graphics.ES20;
#else
global using OpenTK.Graphics.OpenGL;
#endif
