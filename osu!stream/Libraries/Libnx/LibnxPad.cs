using System;
using System.Runtime.InteropServices;

namespace Libnx;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PadState
{
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct AnalogStickState
	{
		public int x;
		public int y;

		override public string ToString() =>
			$"x={x} y={y}";
	}

	public byte id_mask;
	public byte active_id_mask;
	public byte read_handheld;
	public byte active_handheld;
	public uint style_set;
	public uint attributes;
	public uint padding;
	public ulong buttons_cur;
	public ulong buttons_old;
	public AnalogStickState sticks_0;
	public AnalogStickState sticks_1;
	public uint gc_triggers_0;
	public uint gc_triggers_1;
}

public class Pad
{
	public PadState State;
	static bool InputConfigured = false;

	[DllImport("libnx")]
	static extern void padInitializeWithMask(ref PadState pad, ulong mask);

	[DllImport("libnx")]
	static extern void padConfigureInput(int max_players, int style_set);

	[DllImport("libnx")]
	static extern void padUpdate(ref PadState pad);

	[DllImport("libnx")]
	static extern uint extensionPadStateSize();

	public static void ConfigureInput(int max_players, HidNpadStyleTag style_set)
	{
		InputConfigured = true;
		padConfigureInput(max_players, (int)style_set);
	}

	public Pad(ReadOnlySpan<HidNpadIdType> ids)
	{
		if (!InputConfigured)
			ConfigureInput(1, HidNpadStyleTag.SetNpadStandard);

		if (extensionPadStateSize() != (uint)Marshal.SizeOf<PadState>())
			throw new InvalidOperationException("Invalid size of LibnxPadState");

		State = default;

		ulong mask = 0;
		foreach (var padId in ids)
			mask |= 1UL << (int)(padId);

		padInitializeWithMask(ref State, mask);
	}

	public Pad() :
		this([HidNpadIdType.No1, HidNpadIdType.Handheld])
	{

	}

	public void Update()
	{
		padUpdate(ref State);
	}

	public HidNpadButton Buttons => (HidNpadButton)State.buttons_cur;
	public HidNpadButton ButtonsDown => (HidNpadButton)(State.buttons_cur & ~State.buttons_old);
	public HidNpadButton ButtonsUp => (HidNpadButton)(State.buttons_old & ~State.buttons_cur);
}
