using System;

internal enum ShellPage { Cube, Face, Detail, Browser }
internal enum ShellFace { Menu, Video, Calendar, Options, Play, Memory }

// UI state is independent of the device. Only VideoModeMenu may change VI timing.
internal sealed class IplShell
{
    internal readonly VideoModeMenu Video;
    internal readonly IplBrowser? Browser;
    internal ShellPage Page;
    internal ShellFace Face, PreviousFace;
    internal int Row, Part;
    internal float Time, Turn = 1;
    internal float SelectorTime { get; private set; }
    internal bool ReducedMotion;
    private readonly IIplPreferencesStore? preferences;
    private readonly IIplConsoleCalendar? calendar;
    private readonly IIplConsoleSound? soundService;
    internal bool SoundAvailable {get;private set;}=true;
    private float calendarWait;
    internal bool CalendarAvailable {get;private set;}=true;
    internal bool RealCalendar=>calendar!=null;
    internal string SystemStatus {get;private set;}="";
    private readonly float[] focus = new float[3];
    internal float Focus(int row) => focus[row];
    internal int Hour = 12, Minute, Day = 1, Month = 1, Year = 2026, Sound;
    private int hour, minute, day, month, year, sound;
    private int repeatX, repeatY;
    private float repeatTime;
    private int entryX,entryY;
    private bool returnLatch;
    private bool repeatStick;
    private bool storageFromCube;
    internal IplShell(IVideoModeOutput output,IIplPreferencesStore? preferences=null,IIplConsoleCalendar? calendar=null,bool restoreVideo=true,IIplConsoleSound? soundService=null,IplBrowser? browser=null)
    {
        Browser=browser;this.preferences=preferences;this.calendar=calendar;this.soundService=soundService;bool loaded=false;
        if(preferences!=null&&preferences.Load(out var saved)){
            loaded=true;
            Sound=saved.Sound;ReducedMotion=saved.ReducedMotion;
            if(restoreVideo){
                try{output.Configure(saved.Video);SystemStatus="Saved settings restored.";}
                catch(InvalidOperationException){SystemStatus="Saved video unavailable. Using boot output.";}
                catch(OutOfMemoryException){SystemStatus="Saved video needs more memory. Using boot output.";}
            }else SystemStatus="Startup video restore bypassed.";
        }else if(preferences!=null)SystemStatus=preferences.Status;
        if(soundService!=null){
            if(loaded){SoundAvailable=soundService.Write(Sound);if(!SoundAvailable){string error=soundService.Status;if(soundService.Read(out int actual)){Sound=actual;SoundAvailable=true;}SystemStatus=error;}}
            else{SoundAvailable=soundService.Read(out int mode);if(SoundAvailable)Sound=mode;}
        }
        Video=new VideoModeMenu(output);RefreshCalendar();
    }
    private bool SavePreferences()
    {
        if(preferences==null)return true;
        bool saved=preferences.Save(new IplPreferences(Video.Applied,Sound,ReducedMotion));SystemStatus=preferences.Status;return saved;
    }
    private bool RefreshCalendar()
    {
        if(calendar==null)return true;
        CalendarAvailable=calendar.Read(out var now);
        if(CalendarAvailable){Hour=now.Hour;Minute=now.Minute;Year=now.Year;Month=now.Month;Day=now.Day;}
        return CalendarAvailable;
    }
    internal void SafeMode()
    {if(Page==ShellPage.Browser)Browser?.Dispose();Video.SafeMode();if(!Video.Pending&&Video.Status=="SAFE MODE - 640X480I 4:3"){TurnTo(ShellFace.Video);SavePreferences();}else SystemStatus=Video.Status;}
    internal int Rows => Face == ShellFace.Video ? 3 : Face == ShellFace.Calendar ? 2 : Face == ShellFace.Options ? 3 : Face==ShellFace.Memory?2:1;
    internal bool Editing => Page == ShellPage.Detail;
    internal string Title => Face == ShellFace.Video ? "Video" : Face == ShellFace.Calendar ? "Calendar" : Face == ShellFace.Options ? "Options" : Face == ShellFace.Memory ? "Storage" : "Game Play";
    internal string Label(int row) => Face == ShellFace.Video ? (row == 0 ? "Output" : row == 1 ? "Scan" : "Aspect Ratio") : Face == ShellFace.Calendar ? (row == 0 ? "Time" : "Date") : Face == ShellFace.Options ? (row == 0 ? "Sound" : row == 1 ? "Motion" : "Storage") : Face == ShellFace.Memory ? (row==0?"Memory Card":"SD Storage") : "Game Library";
    internal string Value(int row)
    {
        var v = Editing ? Video.Selected : Video.Applied;
        if (Face == ShellFace.Video) return row == 0 ? (v.Resolution == 0 ? "240" : v.Resolution == 2 ? "576" : "480") : row == 1 ? (v.Scan == 0 ? "Auto" : v.Scan == 1 ? "Interlaced" : "Progressive") : v.Widescreen ? "16:9" : "4:3";
        if (Face == ShellFace.Calendar) return !CalendarAvailable?"Unavailable":row == 0 ? Two(Editing ? hour : Hour) + ":" + Two(Editing ? minute : Minute) : Two(Editing ? day : Day) + "/" + Two(Editing ? month : Month) + "/" + (Editing ? year : Year).ToString();
        if (Face == ShellFace.Options) return row == 0 ? !SoundAvailable?"Unavailable":((Editing ? sound : Sound) == 0 ? "Stereo" : "Mono") : row == 1 ? (ReducedMotion ? "Reduced" : "Full") : "Open";
        return "Open";
    }
    internal string Segment(int part) => Row == 0 ? Two(part == 0 ? hour : minute) : part == 0 ? Two(day) : part == 1 ? Two(month) : year.ToString();
    private static string Two(int v) => v < 10 ? "0" + v.ToString() : v.ToString();
    private static int Wrap(int v, int min, int max) => v < min ? max : v > max ? min : v;
    private static int Days(int year,int month) => month==2 ? (year%4==0&&(year%100!=0||year%400==0)?29:28) : month==4||month==6||month==9||month==11?30:31;
    internal void Tick(float dt)
    {
        if(Page==ShellPage.Browser)Browser?.Tick(dt,ReducedMotion);
        Time += dt; Turn = Math.Min(1, Turn + dt / .64f);
        SelectorTime = Editing ? Math.Min(.18f, SelectorTime + dt) : 0;
        calendarWait-=dt;if(calendarWait<=0){calendarWait=1;RefreshCalendar();}
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
        if(face==ShellFace.Memory)storageFromCube=Face==ShellFace.Menu;
        PreviousFace = Face; Face = face; Row = Part = 0;
        for(int i=0;i<3;i++)focus[i]=0;
        Page = face == ShellFace.Menu ? ShellPage.Cube : ShellPage.Face;
        Turn = ReducedMotion ? 1 : 0;
        entryX=face==ShellFace.Video?1:face==ShellFace.Options?-1:0;
        entryY=face==ShellFace.Play?1:face==ShellFace.Calendar?-1:0;
    }
    // A held direction repeats values, never confirmation or a change of screen.
    internal void Direction(int x, int y, float dt,bool stick=false)
    {
        if (x == 0 && y == 0) { repeatX = repeatY = 0; repeatTime = 0;returnLatch=false; return; }
        if(stick&&returnLatch)return;
        bool fresh = x != repeatX || y != repeatY || stick!=repeatStick;
        repeatX = x; repeatY = y; repeatStick=stick;repeatTime -= dt;
        if (!fresh && repeatTime > 0) return;
        repeatTime = fresh ? .38f : .13f;
        if (Video.Pending || Turn < 1) return;
        if(stick&&fresh&&Page==ShellPage.Face&&((entryX!=0&&x==-entryX)||(entryY!=0&&y==-entryY)))
        {Back();returnLatch=true;return;}
        if(Page==ShellPage.Browser){Browser?.Direction(x,y);return;}
        if (Page == ShellPage.Cube)
        {
            TurnTo(x > 0 ? ShellFace.Video : x < 0 ? ShellFace.Options : y > 0 ? ShellFace.Play : ShellFace.Calendar);
            return;
        }
        if (Page == ShellPage.Face) { if (!stick && y != 0) Row = Wrap(Row - y, 0, Rows - 1); return; }
        // Horizontal movement selects a part; it never changes a value.
        if (Face == ShellFace.Calendar && x != 0) { Part = Wrap(Part + x, 0, Row == 0 ? 1 : 2); SelectorTime = 0; }
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
        if (Video.Pending) { Video.Keep(); if(!Video.Pending) {Page = ShellPage.Face;SavePreferences();} return; }
        if(Page==ShellPage.Browser){Browser?.Confirm(Video.Applied);return;}
        if (Page == ShellPage.Cube) return;
        if (Page == ShellPage.Face)
        {
            if (Face == ShellFace.Options && Row == 2) { TurnTo(ShellFace.Memory); return; }
            if(Face==ShellFace.Play){if(Browser==null){SystemStatus="Library unavailable in this build.";return;}Browser.Open(IplBrowserKind.Games);Page=ShellPage.Browser;return;}
            if(Face==ShellFace.Memory){if(Browser==null){SystemStatus="Storage services unavailable in this build.";return;}Browser.Open(Row==0?IplBrowserKind.MemoryCards:IplBrowserKind.Sd);Page=ShellPage.Browser;return;}
            if (Face == ShellFace.Calendar && !CalendarAvailable) {SystemStatus=calendar==null?"System clock unavailable.":calendar.Status;return;}
            if (Face == ShellFace.Options && Row==0 && !SoundAvailable) {SystemStatus=soundService==null?"Console sound unavailable.":soundService.Status;return;}
            if (Face == ShellFace.Options && Row == 1) { ReducedMotion = !ReducedMotion;SavePreferences(); return; }
            Video.CancelSelection(); Video.SelectRow(Row);
            hour = Hour; minute = Minute; day = Day; month = Month; year = Year; sound = Sound;
            Part = 0; SelectorTime = 0; Page = ShellPage.Detail; return;
        }
        if (Face == ShellFace.Video)
        {
            Video.Apply(); // Failure leaves the editor and current mode intact.
            return;
        }
        if(Face==ShellFace.Calendar&&calendar!=null){
            if(!calendar.Read(out var now)){SystemStatus=calendar.Status;return;}
            var desired=Row==0?new IplCalendarValue(now.Year,now.Month,now.Day,hour,minute,now.Second):new IplCalendarValue(year,month,day,now.Hour,now.Minute,now.Second);
            bool written=calendar.Write(desired);string status=calendar.Status;RefreshCalendar();SystemStatus=status;
            if(!written)return;
        }else if(Face==ShellFace.Calendar){Hour = hour; Minute = minute; Day = day; Month = month; Year = year;}
        if(Face==ShellFace.Options&&Row==0&&soundService!=null&&!soundService.Write(sound)){
            SystemStatus=soundService.Status;if(soundService.Read(out int actual))Sound=actual;return;
        }
        Sound = sound;
        if(Face==ShellFace.Options&&!SavePreferences()&&Row==0&&soundService!=null)SystemStatus="Console sound saved; SD settings not saved.";
        Page = ShellPage.Face;
    }
    internal void Back()
    {
        if(Turn<1)return;
        if (Video.Pending) { Video.Revert(); if (!Video.Pending) Page = ShellPage.Face; return; }
        if(Page==ShellPage.Browser){if(Browser==null||!Browser.Back())Page=ShellPage.Face;return;}
        if (Page == ShellPage.Detail) { Video.CancelSelection(); Page = ShellPage.Face; return; }
        if (Face == ShellFace.Memory) TurnTo(storageFromCube?ShellFace.Menu:ShellFace.Options); else TurnTo(ShellFace.Menu);
    }
}
