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
    private int motion=1,motionDraft=1;
    internal int MotionLevel=>Editing&&Face==ShellFace.Options&&Row==1?motionDraft:motion;
    // Older renderers and accessibility consumers still use the binary form.
    internal bool ReducedMotion {get=>MotionLevel==0;set{motion=value?0:1;motionDraft=motion;}}
    internal string MotionName=>MotionLevel==0?"Disabled":MotionLevel==1?"Subtle":"Pronounced";
    private int glassBlur,glassBlurDraft;
    internal int GlassBlur=>Editing&&Face==ShellFace.Options&&Row==2?glassBlurDraft:glassBlur;
    private readonly IIplPreferencesStore? preferences;
    private readonly IIplConsoleCalendar? calendar;
    private readonly IIplConsoleSound? soundService;
    internal bool SoundAvailable {get;private set;}=true;
    private float calendarWait;
    internal bool CalendarAvailable {get;private set;}=true;
    internal bool RealCalendar=>calendar!=null;
    internal string SystemStatus {get;private set;}="";
    private readonly float[] focus = new float[4];
    internal float Focus(int row) => focus[row];
    internal int Hour = 12, Minute, Day = 1, Month = 1, Year = 2026, Sound;
    private int hour, minute, day, month, year, sound;
    private int repeatX, repeatY;
    private float repeatTime;
    private int entryX,entryY;
    private bool returnLatch;
    private bool repeatStick;
    private bool fieldEditing,returnAfterRevert;
    internal IplShell(IVideoModeOutput output,IIplPreferencesStore? preferences=null,IIplConsoleCalendar? calendar=null,bool restoreVideo=true,IIplConsoleSound? soundService=null,IplBrowser? browser=null)
    {
        Browser=browser;this.preferences=preferences;this.calendar=calendar;this.soundService=soundService;bool loaded=false;
        if(preferences!=null&&preferences.Load(out var saved)){
            loaded=true;
            Sound=saved.Sound;motion=motionDraft=saved.MotionLevel;glassBlur=saved.GlassBlur;
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
        bool saved=preferences.Save(new IplPreferences(Video.Applied,Sound,motion,glassBlur));SystemStatus=preferences.Status;return saved;
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
#if IPL_HISTORICAL_RENDERER
    // The measured historical adapter retains only three panel rows.
    internal int Rows => Face == ShellFace.Video ? 3 : Face == ShellFace.Calendar ? 2 : Face == ShellFace.Options ? 3 : Face==ShellFace.Memory?2:1;
#else
    internal int Rows => Face == ShellFace.Video ? 3 : Face == ShellFace.Calendar ? 2 : Face == ShellFace.Options ? 3 : Face==ShellFace.Memory?2:1;
#endif
    internal bool Editing => Page == ShellPage.Detail&&fieldEditing;
    internal bool FlatMenu => Page == ShellPage.Detail&&!fieldEditing;
    internal string Title => Face == ShellFace.Video ? "Video" : Face == ShellFace.Calendar ? "Calendar" : Face == ShellFace.Options ? "Options" : Face == ShellFace.Memory ? "Storage" : "Game Play";
    internal string Label(int row) => Face == ShellFace.Video ? (row == 0 ? "Output" : row == 1 ? "Scan" : "Aspect Ratio") : Face == ShellFace.Calendar ? (row == 0 ? "Time" : "Date") : Face == ShellFace.Options ? (row == 0 ? "Sound" : row == 1 ? "Motion" : "Glass Blur") : Face == ShellFace.Memory ? (row==0?"Memory Card":"SD Storage") : "Game Library";
    internal string Value(int row)
    {
        var v = Editing ? Video.Selected : Video.Applied;
        if (Face == ShellFace.Video) return row == 0 ? (v.Resolution == 0 ? "240" : v.Resolution == 2 ? "576" : "480") : row == 1 ? (v.Scan == 0 ? "Auto" : v.Scan == 1 ? "Interlaced" : "Progressive") : v.Widescreen ? "16:9" : "4:3";
        if (Face == ShellFace.Calendar) return !CalendarAvailable?"Unavailable":row == 0 ? Two(Editing ? hour : Hour) + ":" + Two(Editing ? minute : Minute) : Two(Editing ? day : Day) + "/" + Two(Editing ? month : Month) + "/" + (Editing ? year : Year).ToString();
        if (Face == ShellFace.Options) return row == 0 ? !SoundAvailable?"Unavailable":((Editing ? sound : Sound) == 0 ? "Stereo" : "Mono") : row == 1 ? MotionName : GlassBlur==0?"Off":GlassBlur==1?"Low":GlassBlur==2?"Medium":"High";
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
        for(int i=0;i<focus.Length;i++)
        {
            float target=FlatMenu&&i==Row?1:0;
            if(ReducedMotion){focus[i]=target;continue;}
            float step=dt/.18f;
            focus[i]=target>focus[i]?Math.Min(target,focus[i]+step):Math.Max(target,focus[i]-step);
        }
        bool pending = Video.Pending; Video.Tick(dt);
        if(pending&&!Video.Pending){if(returnAfterRevert)ReturnToCube();else ShowFlatMenu();}
    }
    internal void TurnTo(ShellFace face)
    {
        fieldEditing=false;returnAfterRevert=false;
        PreviousFace = Face; Face = face; Row = Part = 0;
        for(int i=0;i<focus.Length;i++)focus[i]=0;
        Page = face == ShellFace.Menu ? ShellPage.Cube : ShellPage.Face;
        Turn = ReducedMotion ? 1 : 0;
        entryX=face==ShellFace.Video?1:face==ShellFace.Options?-1:0;
        entryY=face==ShellFace.Memory?1:face==ShellFace.Calendar?-1:0;
    }
    // A held direction repeats values, never confirmation or a change of screen.
    internal void Direction(int x, int y, float dt,bool stick=false)
    {
        if (x == 0 && y == 0) { repeatX = repeatY = 0; repeatTime = 0;returnLatch=false; return; }
        if(returnLatch)return;
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
            TurnTo(x > 0 ? ShellFace.Video : x < 0 ? ShellFace.Options : y > 0 ? ShellFace.Memory : ShellFace.Calendar);
            return;
        }
        if(Page==ShellPage.Face)return; // Side labels preview the options; A opens the flat list.
        if(FlatMenu){if(y!=0)Row=Wrap(Row-y,0,Rows-1);return;}
        // Horizontal movement selects a part; it never changes a value.
        if (Face == ShellFace.Calendar && x != 0) { Part = Wrap(Part + x, 0, Row == 0 ? 1 : 2); SelectorTime = 0; }
#if !IPL_HISTORICAL_RENDERER
        if (Face == ShellFace.Video && x != 0) { Row=Wrap(Row+x,0,2);Part=Row;Video.SelectRow(Row);SelectorTime=0; }
#endif
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
        else if (Face == ShellFace.Options && Row == 1) motionDraft=Wrap(motionDraft+y,0,2);
        else if (Face == ShellFace.Options && Row == 2) glassBlurDraft=Wrap(glassBlurDraft+y,0,3);
    }
    internal void Confirm()
    {
        if (Turn < 1) return;
        if(Video.Pending){Video.Keep();if(!Video.Pending){ShowFlatMenu();SavePreferences();}return;}
        if(Page==ShellPage.Browser){Browser?.Confirm(Video.Applied);return;}
        if (Page == ShellPage.Cube) return;
        if(Page==ShellPage.Face){
            if(Face==ShellFace.Play){OpenBrowser(IplBrowserKind.Games);return;}
            ShowFlatMenu();return;
        }
        if(FlatMenu)
        {
            if(Face==ShellFace.Memory){OpenBrowser(Row==0?IplBrowserKind.MemoryCards:IplBrowserKind.Sd);return;}
            if (Face == ShellFace.Calendar && !CalendarAvailable) {SystemStatus=calendar==null?"System clock unavailable.":calendar.Status;return;}
            if (Face == ShellFace.Options && Row==0 && !SoundAvailable) {SystemStatus=soundService==null?"Console sound unavailable.":soundService.Status;return;}
            Video.CancelSelection(); Video.SelectRow(Row);
            hour = Hour; minute = Minute; day = Day; month = Month; year = Year; sound = Sound;glassBlurDraft=glassBlur;motionDraft=motion;
            Part=0;SelectorTime=0;fieldEditing=true;Page=ShellPage.Detail;return;
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
        if(Face==ShellFace.Options&&Row==1)motion=motionDraft;
        if(Face==ShellFace.Options&&Row==2)glassBlur=glassBlurDraft;
        if(Face==ShellFace.Options&&!SavePreferences()&&Row==0&&soundService!=null)SystemStatus="Console sound saved; SD settings not saved.";
        ShowFlatMenu();
    }
    private void ShowFlatMenu()
    {
        fieldEditing=false;SelectorTime=0;Page=ShellPage.Detail;
#if !IPL_HISTORICAL_RENDERER
        // Video has three parts in one Calendar-style configuration page.
        // Keep/timeout synchronize every displayed part to the applied mode.
        if(Face==ShellFace.Video){Video.CancelSelection();Video.SelectRow(Row);Part=Row;fieldEditing=true;}
#endif
    }
    private void OpenBrowser(IplBrowserKind kind)
    {if(Browser==null){SystemStatus="Storage services unavailable in this build.";return;}Browser.Open(kind);fieldEditing=false;Page=ShellPage.Browser;}
    private void ReturnToCube(){Video.CancelSelection();fieldEditing=false;returnAfterRevert=false;Page=ShellPage.Face;Turn=1;SelectorTime=0;returnLatch=true;}
    internal void Back()
    {
        if(Turn<1||Page==ShellPage.Cube)return;
        if(Video.Pending){returnAfterRevert=true;Video.Revert();if(!Video.Pending)ReturnToCube();return;}
        if(Page==ShellPage.Face){TurnTo(ShellFace.Menu);returnLatch=true;return;}
        if(Page==ShellPage.Browser&&Browser!=null){string status=Browser.Exit();if(status.Length>0)SystemStatus=status;}
        ReturnToCube();
    }
}
