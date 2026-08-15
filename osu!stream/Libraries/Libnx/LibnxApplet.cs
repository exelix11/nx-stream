using System.Runtime.InteropServices;

namespace Libnx;

public static class Applet
{
	[DllImport("libnx")]
	static extern AppletOperationMode appletGetOperationMode();

	public enum AppletOperationMode : int
	{
    	Handheld = 0,
    	Console = 1,
	}

	public static bool GetDockStatus()
	{
		var isDocked = appletGetOperationMode() == AppletOperationMode.Console;
		Logging.Write($"Console is docked? {isDocked}");
		return isDocked;
	}
}