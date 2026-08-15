using System;
using System.Runtime.InteropServices;

namespace Libnx;

[Flags]
public enum HidNpadStyleTag : int
{
	NpadFullKey = 1 << (0),       ///< Pro Controller
	NpadHandheld = 1 << (1),       ///< Joy-Con controller in handheld mode
	NpadJoyDual = 1 << (2),       ///< Joy-Con controller in dual mode
	NpadJoyLeft = 1 << (3),       ///< Joy-Con left controller in single mode
	NpadJoyRight = 1 << (4),       ///< Joy-Con right controller in single mode
	NpadGc = 1 << (5),       ///< GameCube controller
	NpadPalma = 1 << (6),       ///< Poké Ball Plus controller
	NpadLark = 1 << (7),       ///< NES/Famicom controller
	NpadHandheldLark = 1 << (8),       ///< NES/Famicom controller in handheld mode
	NpadLucia = 1 << (9),       ///< SNES controller
	NpadLagon = 1 << (10),      ///< N64 controller
	NpadLager = 1 << (11),      ///< Sega Genesis controller
	NpadSystemExt = 1 << (29),      ///< Generic external controller
	NpadSystem = 1 << (30),      ///< Generic controller

	SetNpadFullCtrl = NpadFullKey | NpadHandheld | NpadJoyDual,  ///< Style set comprising Npad styles containing the full set of controls {FullKey, Handheld, JoyDual}
	SetNpadStandard = SetNpadFullCtrl | NpadJoyLeft | NpadJoyRight, ///< Style set comprising all standard Npad styles {FullKey, Handheld, JoyDual, JoyLeft, JoyRight}
};

[Flags]
public enum HidNpadButton : ulong
{
	A = 1UL << (0),  ///< A button / Right face button
	B = 1UL << (1),  ///< B button / Down face button
	X = 1UL << (2),  ///< X button / Up face button
	Y = 1UL << (3),  ///< Y button / Left face button
	StickL = 1UL << (4),  ///< Left Stick button
	StickR = 1UL << (5),  ///< Right Stick button
	L = 1UL << (6),  ///< L button
	R = 1UL << (7),  ///< R button
	ZL = 1UL << (8),  ///< ZL button
	ZR = 1UL << (9),  ///< ZR button
	Plus = 1UL << (10), ///< Plus button
	Minus = 1UL << (11), ///< Minus button
	Left = 1UL << (12), ///< D-Pad Left button
	Up = 1UL << (13), ///< D-Pad Up button
	Right = 1UL << (14), ///< D-Pad Right button
	Down = 1UL << (15), ///< D-Pad Down button
	StickLLeft = 1UL << (16), ///< Left Stick pseudo-button when moved Left
	StickLUp = 1UL << (17), ///< Left Stick pseudo-button when moved Up
	StickLRight = 1UL << (18), ///< Left Stick pseudo-button when moved Right
	StickLDown = 1UL << (19), ///< Left Stick pseudo-button when moved Down
	StickRLeft = 1UL << (20), ///< Right Stick pseudo-button when moved Left
	StickRUp = 1UL << (21), ///< Right Stick pseudo-button when moved Up
	StickRRight = 1UL << (22), ///< Right Stick pseudo-button when moved Right
	StickRDown = 1UL << (23), ///< Right Stick pseudo-button when moved Left
	LeftSL = 1UL << (24), ///< SL button on Left Joy-Con
	LeftSR = 1UL << (25), ///< SR button on Left Joy-Con
	RightSL = 1UL << (26), ///< SL button on Right Joy-Con
	RightSR = 1UL << (27), ///< SR button on Right Joy-Con
	Palma = 1UL << (28), ///< Top button on Poké Ball Plus (Palma) controller
	Verification = 1UL << (29), ///< Verification
	HandheldLeftB = 1UL << (30), ///< B button on Left NES/HVC controller in Handheld mode
	LagonCLeft = 1UL << (31), ///< Left C button in N64 controller
	LagonCUp = 1UL << (32), ///< Up C button in N64 controller
	LagonCRight = 1UL << (33), ///< Right C button in N64 controller
	LagonCDown = 1UL << (34), ///< Down C button in N64 controller

	AnyLeft = Left | StickLLeft | StickRLeft,  ///< 1 << mask containing all buttons that are considered Left (D-Pad, Sticks)
	AnyUp = Up | StickLUp | StickRUp,    ///< 1 << mask containing all buttons that are considered Up (D-Pad, Sticks)
	AnyRight = Right | StickLRight | StickRRight, ///< 1 << mask containing all buttons that are considered Right (D-Pad, Sticks)
	AnyDown = Down | StickLDown | StickRDown,  ///< 1 << mask containing all buttons that are considered Down (D-Pad, Sticks)
	AnySL = LeftSL | RightSL,                                 ///< 1 << mask containing SL buttons on both Joy-Cons (Left/Right)
	AnySR = LeftSR | RightSR,                                 ///< 1 << mask containing SR buttons on both Joy-Cons (Left/Right)
}

public enum HidNpadIdType
{
	No1 = 0,    ///< Player 1 controller
	No2 = 1,    ///< Player 2 controller
	No3 = 2,    ///< Player 3 controller
	No4 = 3,    ///< Player 4 controller
	No5 = 4,    ///< Player 5 controller
	No6 = 5,    ///< Player 6 controller
	No7 = 6,    ///< Player 7 controller
	No8 = 7,    ///< Player 8 controller
	Other = 0x10, ///< Other controller
	Handheld = 0x20, ///< Handheld mode controls
}

[Flags]
public enum HidTouchAttribute : uint
{
	HidTouchAttribute_Start = 1,
	HidTouchAttribute_End = 2,
};

public struct HidTouchState
{
	public ulong delta_time;
	public HidTouchAttribute attributes;
	public int finger_id; // Actually uint32_t, but we don't need to worry about that here
	public uint x;
	public uint y;
	public uint diameter_x;
	public uint diameter_y;
	public uint rotation_angle;
	public uint reserved;
}

[System.Runtime.CompilerServices.InlineArray(HidTouchScreenState.FingerCountMax)]
public struct TouchStateInlineArray
{
     HidTouchState touch;
}

public struct HidTouchScreenState
{
	public const int FingerCountMax = 16;

	public ulong sampling_number;
	public int count;
	public uint reserved;
	public TouchStateInlineArray touches;
}

public class TouchScreen
{
	[DllImport("libnx")]
	static extern void hidInitializeTouchScreen();

	[DllImport("libnx")]
	static extern uint hidGetTouchScreenStates(ref HidTouchScreenState states, uint count);

	[DllImport("libnx")]
	static extern uint extensionHidTouchScreenStateSize();

	public HidTouchScreenState _State;

	public ref HidTouchScreenState State => ref _State;

	public TouchScreen()
	{
		if (extensionHidTouchScreenStateSize() != (uint)Marshal.SizeOf<HidTouchScreenState>())
			throw new Exception("HidTouchScreenState size mismatch");

		hidInitializeTouchScreen();		
	}

	public bool Update()
	{
		return hidGetTouchScreenStates(ref _State, 1) > 0;
	}
}