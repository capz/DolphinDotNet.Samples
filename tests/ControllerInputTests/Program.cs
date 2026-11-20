using System;
using Dolphin.Input;

internal static class Program
{
    private const ControllerButton Chord = ControllerButton.Start | ControllerButton.Z;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("Controller session: " + message);
    }
    private static ControllerSession NewSession()
    {
        Controller.Reset();
        return new ControllerSession();
    }
    private static void Hold(ControllerSession session, int frames)
    {
        for (int i = 0; i < frames; i++) session.Poll(.1f);
    }

    private static void ConnectionAndSnapshots()
    {
        var session = NewSession();
        session.Poll(1f / 60);
        Check(session.SelectedPort == -1 && !session.State.IsConnected && session.StatusText == "NO CONTROLLER",
            "no controller is neutral without inventing an active port");
        Controller.Frames[2] = new ControllerState(true, ControllerButton.A, justConnected: true);
        session.Poll(1f / 60);
        Check(session.SelectedPort == 2 && session.State.HeldButtons == ControllerButton.A,
            "the first connected controller can be on any of four ports");
        Check(session.State.PressedButtons == 0 && session.LastEventText == "PORT 3 connected",
            "held-on-connect is visible without a synthetic press");
        Check(session.StatusText == "PORT 3 / CONNECTED" && session.ButtonsText == "A", "cached status describes selection");
        int updates = Controller.Updates;
        for (int i = 0; i < 4; i++)
        {
            Check(Controller.Reads[i] == updates, "each poll reads every port exactly once");
            Check(session.IsConnected(i) == (i == 2), "connection cards use the same four-port snapshot");
            _ = session.GetState(i);
        }
        _ = session.State; _ = session.State;
        Check(Controller.Updates == updates && Controller.Reads[2] == updates, "render reads never resample hardware");
        bool rejected = false;
        try { session.GetState(4); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected, "invalid port reads fail instead of selecting another port");

        Controller.Frames[0] = new ControllerState(true, justConnected: true);
        session.Poll(.016f);
        Check(session.SelectedPort == 2 && session.IsConnected(0) && session.LastEventText == "PORT 1 connected",
            "passive hotplug does not steal an active controller");
        Controller.Frames[2] = new ControllerState(false, justDisconnected: true);
        session.Poll(.016f);
        Check(session.SelectedPort == 0 && session.State.HeldButtons == 0 && session.ButtonsText == "NONE",
            "active unplug immediately selects a connected fallback with neutral buttons");
        Check(session.LastEventText == "PORT 3 disconnected", "unplug is reported separately from button releases");
        Controller.Frames[0] = new ControllerState(false, justDisconnected: true);
        session.Poll(.016f);
        Check(session.SelectedPort == -1 && session.State.HeldButtons == 0 && session.State.Stick.X == 0,
            "last unplug clears the visible controller");
    }

    private static void SelectionAndHysteresis()
    {
        var session = NewSession();
        for (int i = 0; i < 4; i++) Controller.Frames[i] = new ControllerState(true);
        session.Poll(.016f);
        Check(session.SelectedPort == 0, "simultaneous first connection defaults to lowest port");
        Controller.Frames[3] = new ControllerState(true, ControllerButton.X, ControllerButton.X);
        session.Poll(.016f);
        Check(session.SelectedPort == 3 && session.ButtonsText == "X" && session.LastEventText == "PORT 4 pressed X",
            "a real press selects port four and preserves its snapshot");
        Controller.Frames[3] = new ControllerState(true, ControllerButton.X);
        Controller.Frames[0] = new ControllerState(true, x: .12f, y: -.1f);
        Hold(session, 30);
        Check(session.SelectedPort == 3, "small center drift and a held button do not steal selection");
        Controller.Frames[1] = new ControllerState(true, x: .5f);
        session.Poll(.016f);
        Check(session.SelectedPort == 1 && session.LastEventText == "PORT 2 analog input", "deliberate stick movement selects its port");
        Controller.Frames[3] = new ControllerState(true, ControllerButton.Y, ControllerButton.Y);
        session.Poll(.016f);
        Check(session.SelectedPort == 3, "a new press wins over another port's held stick");
        Controller.Frames[3] = new ControllerState(true, ControllerButton.Y);
        Controller.Frames[1] = new ControllerState(true, x: .31f);
        session.Poll(.016f);
        Controller.Frames[1] = new ControllerState(true, x: .39f);
        session.Poll(.016f);
        Check(session.SelectedPort == 3, "hysteresis prevents threshold chatter");
        Controller.Frames[1] = new ControllerState(true, x: .1f);
        session.Poll(.016f);
        Controller.Frames[1] = new ControllerState(true, x: -.4f);
        session.Poll(.016f);
        Check(session.SelectedPort == 1, "returning near center rearms an analog action in either direction");

        Controller.Frames[0] = new ControllerState(true, ControllerButton.A, ControllerButton.A);
        Controller.Frames[1] = new ControllerState(true, ControllerButton.B, ControllerButton.B);
        session.Poll(.016f);
        Check(session.SelectedPort == 1, "simultaneous equal actions favor the already active port");
        Controller.Frames[0] = new ControllerState(true, ControllerButton.A);
        Controller.Frames[1] = new ControllerState(true, ControllerButton.B);
        Controller.Frames[2] = new ControllerState(true, ControllerButton.L, ControllerButton.L);
        Controller.Frames[3] = new ControllerState(true, ControllerButton.R, ControllerButton.R);
        session.Poll(.016f);
        Check(session.SelectedPort == 2, "simultaneous actions from inactive ports have stable lowest-port ordering");
        Controller.Frames[0] = new ControllerState(true, left: .8f);
        Controller.Frames[2] = new ControllerState(true, ControllerButton.L);
        Controller.Frames[3] = new ControllerState(true, ControllerButton.Z, ControllerButton.Z);
        session.Poll(.016f);
        Check(session.SelectedPort == 3, "a button press outranks simultaneous new analog travel");
        Controller.Frames[3] = new ControllerState(true, ControllerButton.Z);
        Controller.Frames[2] = new ControllerState(true, cx: -.7f);
        session.Poll(.016f);
        Check(session.SelectedPort == 2 && session.State.CStick.X == -.7f, "C-stick is independently selectable");
        Controller.Frames[1] = new ControllerState(true, right: .5f);
        session.Poll(.016f);
        Check(session.SelectedPort == 1 && session.State.RightTrigger == .5f && session.State.HeldButtons == 0,
            "analog trigger selection does not invent a digital shoulder click");
        Controller.Frames[0] = new ControllerState(true, left: .8f, right: .7f);
        session.Poll(.016f);
        Check(session.SelectedPort == 0, "a new right trigger is detected while the left is still held");
    }

    private static void ReconnectAndButtons()
    {
        var session = NewSession();
        Controller.Frames[0] = new ControllerState(true);
        session.Poll(.016f);
        Controller.Frames[1] = new ControllerState(true, ControllerButton.All, x: .8f, left: 1);
        session.Poll(.016f);
        Check(session.SelectedPort == 0 && session.LastEventText == "PORT 2 connected",
            "held buttons and axes on a new controller are a baseline");
        Controller.Frames[0] = default;
        session.Poll(.016f);
        Check(session.SelectedPort == 1 && session.State.HeldButtons == ControllerButton.All,
            "fallback displays the actual held baseline rather than suppressing test controls");
        Check(session.ButtonsText == "A + B + X + Y + Z + START + L + R + UP + DOWN + LEFT + RIGHT",
            "all twelve physical controls, including Z and Start, remain visible");
        Controller.Frames[1] = new ControllerState(true, released: ControllerButton.All);
        session.Poll(.016f);
        Check(session.ButtonsText == "NONE" && session.LastEventText.StartsWith("PORT 2 released A + B"),
            "release events report the original physical controls");
        Controller.Frames[1] = default;
        session.Poll(.016f);
        Controller.Frames[1] = new ControllerState(true, Chord, x: .8f);
        Hold(session, 30);
        Check(!session.ExitRequested && session.ExitHoldProgress == 0, "reconnection resets exit arming and analog baseline");
    }

    private static void ExitGesture()
    {
        var session = NewSession();
        Controller.Frames[0] = new ControllerState(true, Chord);
        Hold(session, 30);
        Check(!session.ExitRequested && session.ExitHoldProgress == 0, "held-on-connect exit chord is never armed");
        Controller.Frames[0] = new ControllerState(true, ControllerButton.Start);
        Hold(session, 30);
        Controller.Frames[0] = new ControllerState(true, Chord, ControllerButton.Z);
        Hold(session, 30);
        Check(!session.ExitRequested, "both chord buttons must have been observed released");
        Controller.Frames[0] = new ControllerState(true);
        session.Poll(.016f);
        Controller.Frames[0] = new ControllerState(true, Chord, Chord);
        session.Poll(float.NaN);
        Check(session.ExitHoldProgress == 0, "invalid elapsed time cannot advance exit");
        session.Poll(10f);
        Check(session.ExitHoldProgress <= .051f && !session.ExitRequested, "a suspended frame cannot trigger exit");
        Controller.Frames[0] = new ControllerState(true, Chord);
        Hold(session, 9);
        Check(!session.ExitRequested && session.ExitHoldProgress > .49f, "one second of a real held chord only shows progress");
        Controller.Frames[0] = new ControllerState(true, ControllerButton.Z);
        session.Poll(.016f);
        Check(session.ExitHoldProgress == 0, "releasing either button resets the hold duration");
        Controller.Frames[0] = new ControllerState(true, Chord, ControllerButton.Start);
        Hold(session, 19);
        Check(!session.ExitRequested, "exit cannot fire before two seconds");
        session.Poll(.1f);
        Check(session.ExitRequested && session.ExitHoldProgress == 1, "two seconds requests exit once");
        Controller.Frames[0] = default;
        session.Poll(.016f);
        Check(session.ExitRequested, "the exit request is latched for the application");

        session = NewSession();
        Controller.Frames[0] = new ControllerState(true);
        Controller.Frames[1] = new ControllerState(true);
        session.Poll(.016f);
        Controller.Frames[0] = new ControllerState(true, Chord, Chord);
        Hold(session, 15);
        Controller.Frames[0] = new ControllerState(true, Chord);
        Controller.Frames[1] = new ControllerState(true, ControllerButton.A, ControllerButton.A);
        session.Poll(.1f);
        Check(session.SelectedPort == 1 && session.ExitHoldProgress == 0, "selection change cancels a different controller's hold");
        Controller.Frames[1] = new ControllerState(true, Chord, Chord);
        Hold(session, 10);
        Check(!session.ExitRequested && session.ExitHoldProgress < .51f, "chord hold time is never combined across ports");
        Controller.Frames[1] = default;
        session.Poll(.1f);
        Check(session.SelectedPort == 0 && session.ExitHoldProgress <= .051f,
            "unplug cancels the removed controller's timer even when fallback already holds the chord");
    }

    private static void UnchangedPollDoesNotAllocate()
    {
        var session = NewSession();
        Controller.Frames[0] = new ControllerState(true, ControllerButton.A, x: .5f);
        session.Poll(.016f);
        Hold(session, 100);
        string status = session.StatusText, buttons = session.ButtonsText, lastEvent = session.LastEventText;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) session.Poll(.016f);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0, "ordinary unchanged polling allocates zero managed bytes");
        Check(ReferenceEquals(status, session.StatusText) && ReferenceEquals(buttons, session.ButtonsText)
            && ReferenceEquals(lastEvent, session.LastEventText), "ordinary unchanged polling reuses all cached text");
    }

    private static void Main()
    {
        ConnectionAndSnapshots();
        SelectionAndHysteresis();
        ReconnectAndButtons();
        ExitGesture();
        UnchangedPollDoesNotAllocate();
        Console.WriteLine("Controller session passed: four-port snapshots, hotplug baselines, neutral fallback, deterministic selection, analog hysteresis/priority, all controls, release-armed exit and allocation-free unchanged polling.");
    }
}
