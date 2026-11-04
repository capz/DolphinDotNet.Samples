using System;

internal enum ShellPage { Cube, Face, Detail }
internal enum ShellFace { Menu, Video, Calendar, Options, Play, Memory }

// UI state is independent of the device. Only VideoModeMenu may change VI timing.
internal sealed class IplShell
{
    internal readonly VideoModeMenu Video;
    internal ShellPage Page;
    internal ShellFace Face, PreviousFace;
    internal int Row, Part;
    internal float Time, Turn = 1;
    internal bool ReducedMotion;
    private readonly float[] focus = new float[3];
    internal float Focus(int row) => focus[row];
    internal int Hour = 12, Minute, Day = 1, Month = 1, Year = 2026, Sound;
    private int hour, minute, day, month, year, sound;
    private int repeatX, repeatY;
    private float repeatTime;
    internal IplShell(IVideoModeOutput output) { Video = new VideoModeMenu(output); }
    internal int Rows => Face == ShellFace.Video ? 3 : Face == ShellFace.Calendar ? 2 : Face == ShellFace.Options ? 3 : 1;
    internal bool Editing => Page == ShellPage.Detail;
    internal string Title => Face == ShellFace.Video ? "Video" : Face == ShellFace.Calendar ? "Calendar" : Face == ShellFace.Options ? "Options" : Face == ShellFace.Memory ? "Memory Card" : "Game Play";
    internal string Label(int row) => Face == ShellFace.Video ? (row == 0 ? "Output" : row == 1 ? "Scan" : "Aspect Ratio") : Face == ShellFace.Calendar ? (row == 0 ? "Time" : "Date") : Face == ShellFace.Options ? (row == 0 ? "Sound" : row == 1 ? "Motion" : "Memory Card") : Face == ShellFace.Memory ? "Slot A / Slot B" : "Disc";
    internal string Value(int row)
    {
        var v = Editing ? Video.Selected : Video.Applied;
        if (Face == ShellFace.Video) return row == 0 ? (v.Resolution == 0 ? "240" : v.Resolution == 2 ? "576" : "480") : row == 1 ? (v.Scan == 0 ? "Auto" : v.Scan == 1 ? "Interlaced" : "Progressive") : v.Widescreen ? "16:9" : "4:3";
        if (Face == ShellFace.Calendar) return row == 0 ? Two(Editing ? hour : Hour) + ":" + Two(Editing ? minute : Minute) : Two(Editing ? day : Day) + "/" + Two(Editing ? month : Month) + "/" + (Editing ? year : Year).ToString();
        if (Face == ShellFace.Options) return row == 0 ? ((Editing ? sound : Sound) == 0 ? "Stereo" : "Mono") : row == 1 ? (ReducedMotion ? "Reduced" : "Full") : "Open";
        return Face == ShellFace.Memory ? "Not mounted" : "No disc";
    }
    internal string Segment(int part) => Row == 0 ? Two(part == 0 ? hour : minute) : part == 0 ? Two(day) : part == 1 ? Two(month) : year.ToString();
    private static string Two(int v) => v < 10 ? "0" + v.ToString() : v.ToString();
    private static int Wrap(int v, int min, int max) => v < min ? max : v > max ? min : v;
    private static int Days(int year,int month) => month==2 ? (year%4==0&&(year%100!=0||year%400==0)?29:28) : month==4||month==6||month==9||month==11?30:31;
    internal void Tick(float dt)
    {
        Time += dt; Turn = Math.Min(1, Turn + dt / .64f);
        // Advance both the incoming and outgoing item, independent of frame rate.
        for(int i=0;i<3;i++)
        {
            float target=Page==ShellPage.Face&&i==Row?1:0;
            if(ReducedMotion){focus[i]=target;continue;}
            float step=dt/.18f;
            focus[i]=target>focus[i]?Math.Min(target,focus[i]+step):Math.Max(target,focus[i]-step);
        }
        bool pending = Video.Pending; Video.Tick(dt);
        if (pending && !Video.Pending) Page = ShellPage.Face;
    }
    internal void TurnTo(ShellFace face)
    {
        PreviousFace = Face; Face = face; Row = Part = 0;
        for(int i=0;i<3;i++)focus[i]=0;
        Page = face == ShellFace.Menu ? ShellPage.Cube : ShellPage.Face;
        Turn = ReducedMotion ? 1 : 0;
    }
    // A held direction repeats values, never confirmation or a change of screen.
    internal void Direction(int x, int y, float dt)
    {
        if (x == 0 && y == 0) { repeatX = repeatY = 0; repeatTime = 0; return; }
        bool fresh = x != repeatX || y != repeatY;
        repeatX = x; repeatY = y; repeatTime -= dt;
        if (!fresh && repeatTime > 0) return;
        repeatTime = fresh ? .38f : .13f;
        if (Video.Pending || Turn < 1) return;
        if (Page == ShellPage.Cube)
        {
            TurnTo(x > 0 ? ShellFace.Video : x < 0 ? ShellFace.Options : y > 0 ? ShellFace.Play : ShellFace.Calendar);
            return;
        }
        if (Page == ShellPage.Face) { if (y != 0) Row = Wrap(Row - y, 0, Rows - 1); return; }
        // Horizontal movement selects a part; it never changes a value.
        if (Face == ShellFace.Calendar && x != 0) Part = Wrap(Part + x, 0, Row == 0 ? 1 : 2);
        if (y == 0) return;
        if (Face == ShellFace.Video) { Video.SelectRow(Row); Video.Edit(y); }
        else if (Face == ShellFace.Calendar)
        {
            if (Row == 0) { if (Part == 0) hour = Wrap(hour + y, 0, 23); else minute = Wrap(minute + y, 0, 59); }
            else
            {
                if (Part == 0) day = Wrap(day + y, 1, Days(year, month));
                if (Part == 1) month = Wrap(month + y, 1, 12);
                if (Part == 2) year = Wrap(year + y, 2000, 2099);
                day = Math.Min(day, Days(year, month));
            }
        }
        else if (Face == ShellFace.Options && Row == 0) sound = 1 - sound;
    }
    internal void Confirm()
    {
        if (Turn < 1) return;
        if (Video.Pending) { Video.Keep(); if(!Video.Pending) Page = ShellPage.Face; return; }
        if (Page == ShellPage.Cube) return;
        if (Page == ShellPage.Face)
        {
            if (Face == ShellFace.Options && Row == 2) { TurnTo(ShellFace.Memory); return; }
            if (Face == ShellFace.Play || Face == ShellFace.Memory) return;
            if (Face == ShellFace.Options && Row == 1) { ReducedMotion = !ReducedMotion; return; }
            Video.CancelSelection(); Video.SelectRow(Row);
            hour = Hour; minute = Minute; day = Day; month = Month; year = Year; sound = Sound;
            Part = 0; Page = ShellPage.Detail; return;
        }
        if (Face == ShellFace.Video)
        {
            Video.Apply(); // Failure leaves the editor and current mode intact.
            return;
        }
        Hour = hour; Minute = minute; Day = day; Month = month; Year = year; Sound = sound;
        Page = ShellPage.Face;
    }
    internal void Back()
    {
        if(Turn<1)return;
        if (Video.Pending) { Video.Revert(); if (!Video.Pending) Page = ShellPage.Face; return; }
        if (Page == ShellPage.Detail) { Video.CancelSelection(); Page = ShellPage.Face; return; }
        if (Face == ShellFace.Memory) TurnTo(ShellFace.Options); else TurnTo(ShellFace.Menu);
    }
}
