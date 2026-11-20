// Platform boundary only: snapshots are supplied explicitly by tests. All
// selection, hysteresis, hotplug handling and exit decisions run production code.
namespace Dolphin.Input;

[System.Flags]
public enum ControllerButton
{
    None = 0, All = 0x1f7f,
    DPadLeft = 1, DPadRight = 2, DPadDown = 4, DPadUp = 8,
    Z = 0x10, R = 0x20, L = 0x40, A = 0x100, B = 0x200,
    X = 0x400, Y = 0x800, Start = 0x1000
}

public readonly struct StickState
{
    public float X { get; }
    public float Y { get; }
    public StickState(float x, float y) { X = x; Y = y; }
}

public readonly struct ControllerState
{
    public bool IsConnected { get; }
    public bool WasConnected { get; }
    public bool WasDisconnected { get; }
    public ControllerButton HeldButtons { get; }
    public ControllerButton PressedButtons { get; }
    public ControllerButton ReleasedButtons { get; }
    public StickState Stick { get; }
    public StickState CStick { get; }
    public float LeftTrigger { get; }
    public float RightTrigger { get; }

    public ControllerState(bool connected, ControllerButton held = 0, ControllerButton pressed = 0,
        ControllerButton released = 0, float x = 0, float y = 0, float cx = 0, float cy = 0,
        float left = 0, float right = 0, bool justConnected = false, bool justDisconnected = false)
    {
        IsConnected = connected; WasConnected = justConnected; WasDisconnected = justDisconnected;
        HeldButtons = held; PressedButtons = pressed; ReleasedButtons = released;
        Stick = new StickState(x, y); CStick = new StickState(cx, cy);
        LeftTrigger = left; RightTrigger = right;
    }
}

public static class Controller
{
    public const int PortCount = 4;
    public static readonly ControllerState[] Frames = new ControllerState[PortCount];
    public static readonly int[] Reads = new int[PortCount];
    public static int Updates;
    public static void Update() { Updates++; }
    public static ControllerState GetState(int port) { Reads[port]++; return Frames[port]; }
    public static void Reset()
    {
        Updates = 0;
        for (int i = 0; i < PortCount; i++) { Frames[i] = default; Reads[i] = 0; }
    }
}
