using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace osum.Debugging
{
	public static class Logging
	{
		public static void Write(string message,
			[CallerFilePath] string filePath = "",
			[CallerLineNumber] int lineNumber = 0,
			[CallerMemberName] string memberName = "")
		{
			Console.WriteLine($"[{Path.GetFileName(filePath)}:{lineNumber}][{memberName}] {message}");
		}
		
		public static void Write(Exception ex,
			[CallerFilePath] string filePath = "",
			[CallerLineNumber] int lineNumber = 0,
			[CallerMemberName] string memberName = "")
		{
			Console.WriteLine($"[{Path.GetFileName(filePath)}:{lineNumber}][{memberName}] {ex}");
		}
	}
}