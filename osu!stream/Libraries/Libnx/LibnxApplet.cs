using System;
using System.Runtime.InteropServices;

namespace Libnx;

public static class Applet
{
	[DllImport("libnx")]
	public static extern AppletOperationMode appletGetOperationMode();

	[StructLayout(LayoutKind.Sequential)]
	struct AppletHookCookie // We don't access this struct directly so the effective layout doesn't matter
	{
		IntPtr next;
		IntPtr callback;
		IntPtr param;
	};

	[DllImport("libnx")]
	static extern void appletHook(IntPtr cookie, AppletHookFn callback, IntPtr param);

	[DllImport("libnx")]
	static extern void appletUnhook(IntPtr cookie);

	[DllImport("libnx")]
	static extern AppletFocusState appletGetFocusState();

	[DllImport("libnx")]
	static extern uint appletSetFocusHandlingMode(AppletFocusHandlingMode mode);

	public enum AppletHookType : int
	{
		OnFocusState = 0,
		OnOperationMode,
		OnPerformanceMode,
		OnExitRequest,
		OnResume,
		OnCaptureButtonShortPressed,
		OnAlbumScreenShotTaken,
		RequestToDisplay,
	};

	public delegate void AppletHookFn(AppletHookType hook, IntPtr param);

	public enum AppletFocusHandlingMode : int
	{
		SuspendHomeSleep = 0,       ///< Suspend only when HOME menu is open / console is sleeping (default).
		NoSuspend,                  ///< Don't suspend when out of focus.
		SuspendHomeSleepNotify,     ///< Suspend only when HOME menu is open / console is sleeping but still receive OnFocusState hook.
		AlwaysSuspend,              ///< Always suspend when out of focus, regardless of the reason.
	};

	public enum AppletFocusState : int
	{
		InFocus = 1,                  ///< Applet is focused.
		OutOfFocus = 2,                  ///< Out of focus - LibraryApplet open.
		Background = 3                   ///< Out of focus - HOME menu open / console is sleeping.
	};

	public enum AppletOperationMode : int
	{
		Handheld = 0,
		Console = 1,
	}

	public static void SetSuspendOnFocusLoss(bool suspend)
	{
		var res = appletSetFocusHandlingMode(suspend ? AppletFocusHandlingMode.SuspendHomeSleepNotify : AppletFocusHandlingMode.NoSuspend);
		if (res != 0)
			Logging.Write($"Failed to set focus handling mode to {suspend}: {res}");
	}

	public static IDisposable HookAppletEvents(
		Action<AppletFocusState> focusChange = null,
		Action<AppletOperationMode> operationModeChange = null)
	{
		var res = new HookDisposeGuard()
		{
			FocusChange = focusChange,
			OperationModeChange = operationModeChange
		};

		appletHook(res.Cookie, static (hook, param) =>
		{
			Logging.Write($"Applet hook called: {hook} with param {param}");
			try
			{
				if (hook == AppletHookType.OnOperationMode)
				{
					var guard = (HookDisposeGuard)GCHandle.FromIntPtr(param).Target!;
					guard.OperationModeChange?.Invoke(appletGetOperationMode());
				}
				else if (hook == AppletHookType.OnFocusState)
				{
					var guard = (HookDisposeGuard)GCHandle.FromIntPtr(param).Target!;
					guard.FocusChange?.Invoke(appletGetFocusState());
				}
			}
			catch (Exception ex)
			{
				Logging.Write(ex);
			}
		}, GCHandle.ToIntPtr(res.Handle));

		Logging.Write($"Hooked focus state change with cookie {res.Cookie}");
		return res;
	}

	class HookDisposeGuard : IDisposable
	{
		public IntPtr Cookie = default;
		public GCHandle Handle { get; private set; }

		public Action<AppletFocusState> FocusChange;
		public Action<AppletOperationMode> OperationModeChange;

		public HookDisposeGuard()
		{
			// Pin self so we can recall this callback from the unmanaged side.
			Handle = GCHandle.Alloc(this);
			// The cookie is allocated dynamically because it must be pinned and our gc handle can't be used for pinning because we hold managed references
			Cookie = Marshal.AllocHGlobal(Marshal.SizeOf<AppletHookCookie>());
		}

		public void Dispose()
		{
			appletUnhook(Cookie);
			Marshal.FreeHGlobal(Cookie);
			Handle.Free();
		}
	}
}