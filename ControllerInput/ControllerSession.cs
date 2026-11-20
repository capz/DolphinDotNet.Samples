using System;
using Dolphin.Input;

// The only hardware sampler for this scene. Rendering consumes these retained
// snapshots, so every part of the controller sees the same input update.
internal sealed class ControllerSession
{
    internal const string ExitHint = "Hold START + Z for 2 seconds to exit";
    private const float ExitHoldSeconds = 2f;
    private const float StickEngageSquared = .35f * .35f;
    private const float StickReleaseSquared = .18f * .18f;
    private const float TriggerEngage = .25f;
    private const float TriggerRelease = .10f;
    private const ControllerButton ExitButtons = ControllerButton.Start | ControllerButton.Z;
    private static readonly string[] PortNames = { "PORT 1", "PORT 2", "PORT 3", "PORT 4" };
    private static readonly string[] ConnectedText = {
        "PORT 1 / CONNECTED", "PORT 2 / CONNECTED", "PORT 3 / CONNECTED", "PORT 4 / CONNECTED"
    };
    private static readonly ControllerButton[] Buttons = {
        ControllerButton.A, ControllerButton.B, ControllerButton.X, ControllerButton.Y,
        ControllerButton.Z, ControllerButton.Start, ControllerButton.L, ControllerButton.R,
        ControllerButton.DPadUp, ControllerButton.DPadDown, ControllerButton.DPadLeft, ControllerButton.DPadRight
    };
    private static readonly string[] ButtonNames = {
        "A", "B", "X", "Y", "Z", "START", "L", "R", "UP", "DOWN", "LEFT", "RIGHT"
    };
    private readonly ControllerState[] states = new ControllerState[Controller.PortCount];
    // Independent hysteresis for the two sticks and two triggers on every port.
    // A held axis cannot repeatedly steal selection from another controller.
    private readonly bool[] analogActive = new bool[Controller.PortCount * 4];
    private readonly bool[] exitArmed = new bool[Controller.PortCount];
    private ControllerButton displayedButtons;
    private float exitHold;
    private int exitPort = -1;

    internal int SelectedPort { get; private set; } = -1;
    internal ControllerState State => SelectedPort < 0 ? default : states[SelectedPort];
    internal string StatusText { get; private set; } = "NO CONTROLLER";
    internal string ButtonsText { get; private set; } = "NONE";
    internal string LastEventText { get; private set; } = "Connect a controller to any port";
    internal bool ExitRequested { get; private set; }
    internal float ExitHoldProgress => exitHold / ExitHoldSeconds;

    internal bool IsConnected(int port) => GetState(port).IsConnected;

    internal ControllerState GetState(int port)
    {
        if (port < 0 || port >= Controller.PortCount) throw new ArgumentOutOfRangeException(nameof(port));
        return states[port];
    }

    internal void Poll(float deltaSeconds)
    {
        Controller.Update();
        int previousSelection = SelectedPort;
        int candidate = -1, candidateRank = 0;
        int hotplugPort = -1;
        bool hotplugConnected = false;

        for (int port = 0; port < Controller.PortCount; port++)
        {
            bool wasConnected = states[port].IsConnected;
            ControllerState state = Controller.GetState(port);
            states[port] = state;
            if (state.IsConnected != wasConnected)
            {
                hotplugPort = port;
                hotplugConnected = state.IsConnected;
                exitArmed[port] = state.IsConnected && (state.HeldButtons & ExitButtons) == 0;
            }
            if (!state.IsConnected)
            {
                SetAnalogBaseline(port, default);
                continue;
            }
            if (!wasConnected)
            {
                // The SDK treats held-on-connect as a baseline, not a press.
                // Seed analog hysteresis too, rather than inventing an action.
                SetAnalogBaseline(port, state);
                continue;
            }

            if ((state.HeldButtons & ExitButtons) == 0) exitArmed[port] = true;
            bool analog = UpdateAnalog(port, state);
            int rank = state.PressedButtons != ControllerButton.None ? 2 : analog ? 1 : 0;
            // Simultaneous actions have no temporal ordering in one snapshot:
            // prefer a button over an axis, then the active port, then port order.
            if (rank > candidateRank || (rank != 0 && rank == candidateRank && port == previousSelection))
            {
                candidate = port;
                candidateRank = rank;
            }
        }

        if (candidate >= 0) SelectedPort = candidate;
        else if (SelectedPort < 0 || !states[SelectedPort].IsConnected)
        {
            SelectedPort = -1;
            for (int port = 0; port < Controller.PortCount; port++)
                if (states[port].IsConnected) { SelectedPort = port; break; }
        }

        if (hotplugPort >= 0)
            LastEventText = PortNames[hotplugPort] + (hotplugConnected ? " connected" : " disconnected");
        if (candidate >= 0)
            LastEventText = PortNames[candidate] + (candidateRank == 2
                ? " pressed " + DescribeButtons(states[candidate].PressedButtons)
                : " analog input");
        else if (SelectedPort >= 0 && states[SelectedPort].ReleasedButtons != ControllerButton.None)
            LastEventText = PortNames[SelectedPort] + " released " + DescribeButtons(states[SelectedPort].ReleasedButtons);

        StatusText = SelectedPort < 0 ? "NO CONTROLLER" : ConnectedText[SelectedPort];
        ControllerButton held = State.HeldButtons;
        if (held != displayedButtons)
        {
            displayedButtons = held;
            ButtonsText = DescribeButtons(held);
        }
        UpdateExit(deltaSeconds);
    }

    private void SetAnalogBaseline(int port, ControllerState state)
    {
        int offset = port * 4;
        analogActive[offset] = MagnitudeSquared(state.Stick) >= StickEngageSquared;
        analogActive[offset + 1] = MagnitudeSquared(state.CStick) >= StickEngageSquared;
        analogActive[offset + 2] = state.LeftTrigger >= TriggerEngage;
        analogActive[offset + 3] = state.RightTrigger >= TriggerEngage;
    }

    private bool UpdateAnalog(int port, ControllerState state)
    {
        int offset = port * 4;
        bool changed = UpdateAxis(offset, MagnitudeSquared(state.Stick), StickEngageSquared, StickReleaseSquared);
        changed |= UpdateAxis(offset + 1, MagnitudeSquared(state.CStick), StickEngageSquared, StickReleaseSquared);
        changed |= UpdateAxis(offset + 2, state.LeftTrigger, TriggerEngage, TriggerRelease);
        changed |= UpdateAxis(offset + 3, state.RightTrigger, TriggerEngage, TriggerRelease);
        return changed;
    }

    private bool UpdateAxis(int index, float value, float engage, float release)
    {
        if (analogActive[index])
        {
            if (value <= release) analogActive[index] = false;
            return false;
        }
        if (value < engage) return false;
        analogActive[index] = true;
        return true;
    }

    private static float MagnitudeSquared(StickState stick) => stick.X * stick.X + stick.Y * stick.Y;

    private void UpdateExit(float deltaSeconds)
    {
        if (ExitRequested) return;
        if (SelectedPort < 0 || !exitArmed[SelectedPort] || (State.HeldButtons & ExitButtons) != ExitButtons)
        {
            exitHold = 0;
            exitPort = -1;
            return;
        }
        if (exitPort != SelectedPort) { exitHold = 0; exitPort = SelectedPort; }
        // Do not count a suspended frame or an invalid clock value as a hold.
        if (!(deltaSeconds > 0)) return;
        if (deltaSeconds > .1f) deltaSeconds = .1f;
        exitHold += deltaSeconds;
        if (exitHold >= ExitHoldSeconds)
        {
            exitHold = ExitHoldSeconds;
            ExitRequested = true;
        }
    }

    private static string DescribeButtons(ControllerButton buttons)
    {
        if (buttons == ControllerButton.None) return "NONE";
        string text = "";
        for (int i = 0; i < Buttons.Length; i++)
            if ((buttons & Buttons[i]) != 0)
                text = text.Length == 0 ? ButtonNames[i] : text + " + " + ButtonNames[i];
        return text;
    }
}
