using System;
using System.IO;
namespace HprGhost {
 public sealed class TestPlaybackRenderer:IPlaybackRenderer {public bool Ready{get{return true;}}public bool IsDrawing{get{return true;}}public string Status{get{return "";}}public void SetGameRoot(string root){}public void Load(Replay replay){}public void Update(bool enabled,int opacity,int pid){}public void Stop(){}public void Dispose(){}}
 public static class UiTests {
  static void PortableChecks(Action<bool,string> check,string folder){
   bool eventsFound=false;foreach(string line in AppPaths.NameLines("events.tsv"))if(line.StartsWith("572226\t"))eventsFound=true;check(eventsFound&&new VehicleLibrary("").Name(329012).Contains("SL 65"),"portable EXE includes event and vehicle names without external TSV files");
   string test=Path.Combine(folder,"portable-"+Guid.NewGuid().ToString("N")),saved=Path.Combine(test,"已保存游戏"),running=Path.Combine(test,"正在运行的游戏"),app=Path.Combine(test,"独立软件","子目录");
   try{Directory.CreateDirectory(app);foreach(string game in new string[]{saved,running}){Directory.CreateDirectory(Path.Combine(game,"VEHICLES"));File.WriteAllBytes(Path.Combine(game,"NFS11Remastered.exe"),new byte[]{0});File.WriteAllBytes(Path.Combine(game,"SHADERS.BNDL"),new byte[]{0});}
    check(GameLocation.Find(app,saved,running)==running,"actual running game location takes priority over a saved installation");
    check(GameLocation.Find(app,saved,"")==saved,"portable location accepts a remembered game directory with Unicode characters");
    check(GameLocation.Find(app,Path.Combine(test,"missing"),"")=="","missing game does not guess the portable app folder as game resources");
    check(!GameLocation.Valid(app)&&GameLocation.Find(Path.Combine(saved,"tool"),"","")==saved,"game detection validates assets and preserves the older inside-game layout");
    var models=new VehicleLibrary(saved);models.SetGameRoot(running);int generation=models.Revision;models.SetGameRoot(saved);check(models.Revision>generation,"switching game folders invalidates cached model resources");
   }finally{string full=Path.GetFullPath(test),parent=Path.GetFullPath(folder)+Path.DirectorySeparatorChar;if(!full.StartsWith(parent,StringComparison.OrdinalIgnoreCase))throw new IOException("Unsafe portable test cleanup");if(Directory.Exists(full))Directory.Delete(full,true);}
  }

  public static void Run(Action<bool,string> check,string folder){
   PortableChecks(check,folder);VehicleNameTests.Run(check);
   check(RaceTime.Format(191.609002)=="03:11.60"&&RaceTime.Format(59.999)=="00:59.99"&&RaceTime.Format(60)=="01:00.00","race time uses minutes seconds and centiseconds without premature rounding");
   string test=Path.Combine(folder,"lib-"+Guid.NewGuid().ToString("N").Substring(0,8)),storeFolder=Path.Combine(test,"recordings");Directory.CreateDirectory(storeFolder);
   try{
    var library=new ReplayLibrary(storeFolder,delegate(uint id){return "SL65";});var replay=new Replay{GameHash=GameReader.SupportedHash,EventLabel="SSAS",Author="Player",CreatedUtc=DateTime.UtcNow.ToString("o"),VehicleResourceId=329012,EventId=572226,Completed=true,FinalTime=191.609002};
    replay.Samples.Add(new Sample{Time=0,Pose=new Quat(0,0,0,1).Pose(new Vec()),Speed=1});replay.Samples.Add(new Sample{Time=.05,Pose=new Quat(0,0,0,1).Pose(new Vec(0,0,.05f)),Speed=1});
    string first=library.Save(replay);check(Path.GetFileName(first)=="SSAS-SL65-00分00秒05-Player.hprghost","default recording name uses actual trajectory duration instead of an unverified finish-part value");string second=library.Save(replay);check(first!=second&&File.Exists(first)&&File.Exists(second),"equal recording names never overwrite an existing replay");
    string bytes=Convert.ToBase64String(File.ReadAllBytes(first)),renamed=library.Rename(first,"我的最快圈");check(!File.Exists(first)&&File.Exists(renamed)&&Convert.ToBase64String(File.ReadAllBytes(renamed))==bytes,"rename preserves every original replay byte");
    bool collision=false;try{library.Rename(renamed,Path.GetFileNameWithoutExtension(second));}catch(IOException){collision=true;}check(collision&&File.Exists(renamed)&&File.Exists(second),"rename collision leaves both records intact");
    string external=Path.Combine(test,"friend.hprghost");replay.Save(external);string imported=library.Import(external);check(File.Exists(external)&&imported!=external&&Convert.ToBase64String(File.ReadAllBytes(imported))==bytes,"import copies replay into managed library and preserves the source");
    bool blocked=false;try{library.Delete(external);}catch(IOException){blocked=true;}check(blocked&&File.Exists(external),"library deletion cannot delete an external source file");library.Delete(renamed);check(!File.Exists(renamed)&&File.Exists(second),"delete removes only the selected library record");
    bool invalid=false;try{library.Rename(second,"..\\escape");}catch(InvalidDataException){invalid=true;}check(invalid&&File.Exists(second),"record rename rejects path traversal");
   }finally{string verified=Path.GetFullPath(test),parent=Path.GetFullPath(folder)+Path.DirectorySeparatorChar;if(!verified.StartsWith(parent,StringComparison.OrdinalIgnoreCase))throw new IOException("Unsafe test cleanup");foreach(string f in Directory.GetFiles(storeFolder))File.Delete(f);Directory.Delete(storeFolder);foreach(string f in Directory.GetFiles(test))File.Delete(f);Directory.Delete(test);}
   using(var dialog=new System.Windows.Forms.Form())using(var button=new ActionButton{DialogResult=System.Windows.Forms.DialogResult.OK}){int clicks=0;dialog.Controls.Add(button);button.Click+=delegate{clicks++;};((System.Windows.Forms.IButtonControl)button).PerformClick();check(clicks==1&&dialog.DialogResult==System.Windows.Forms.DialogResult.OK,"custom buttons preserve dialog accept actions");button.Enabled=false;((System.Windows.Forms.IButtonControl)button).PerformClick();check(clicks==1,"disabled custom buttons cannot invoke actions");}
   using(var window=new ControlWindow(new TestPlaybackRenderer()))window.CheckModuleStates(check,folder);
  }
 }
 public sealed partial class ControlWindow {
  internal void CheckModuleStates(Action<bool,string> check,string folder){
   RefreshStatus();check(!recording&&!playing&&!recordCard.IsLit&&!playCard.IsLit&&recordCard.Toggle.Text=="开启录制","both modules start disabled with dim cards");check(lifecycle.Selected==null&&playCard.State.Text=="未导入"&&playCard.Toggle.Text=="未导入"&&!playCard.Toggle.Enabled,"unloaded playback has an explicit empty state and no enabled stop button");
   check(loadButton.Top<files.Top&&loadButton.Top>recordCard.Bottom,"load playback action is in the library header toolbar");
   check(playCard.State.Right<playCard.Caption.Left&&recordCard.State.Right<recordCard.Caption.Left,"activity status and timer captions never overlap");
   check(author.Text=="LoughShinny302","new recordings default to the requested author without rewriting existing records");
   check(openFolderButton.Enabled&&openFolderButton.Top==loadButton.Top&&Path.GetFullPath(library.Folder)==Path.GetFullPath(AppPaths.Recordings),"recordings folder button targets this app library and shares the library toolbar");
   var p=new Quat(0,0,0,1).Pose(new Vec());var f=new Frame{Tick=1000000,Pose=p,Speed=10,VehicleObject=12345,VehicleData=23456,VehicleResourceId=329012};var r=new RaceState{Active=true,Running=true,EventId=572226,RaceTicks=1000000,LiveTicks=1000000};
   race=r;frame=f;lifecycle.Capture=new WholeRaceCapture(f,r,"SSAS","Player",GameReader.SupportedHash);var replay=new Replay{Completed=true,EventId=572226,FinalTime=2,VehicleResourceId=329012,EventLabel="SSAS"};replay.Samples.Add(new Sample{Time=0,Pose=p,Speed=10});replay.Samples.Add(new Sample{Time=.4,Pose=p,Speed=10});replay.Samples.Add(new Sample{Time=.8,Pose=p,Speed=10});replay.Samples.Add(new Sample{Time=1.2,Pose=p,Speed=10});lifecycle.Selected=replay;
   recording=playing=true;RefreshStatus();check(recordCard.State.Text=="正在录制"&&playCard.State.Text=="正在回放","recording and playback present independent simultaneous active states");check(recordCard.IsRunning&&playCard.IsRunning&&recordCard.SurfaceColor.R>UiTheme.Surface.R&&playCard.SurfaceColor.R>UiTheme.Surface.R,"both running cards brighten their entire surfaces independently");
   var savedCapture=lifecycle.Capture;var savedRace=race;lifecycle.Capture=null;race=new RaceState{Active=true,Running=false,EventId=572226};RefreshStatus();check(recordCard.IsLit&&playCard.IsLit&&!recordCard.IsRunning&&!playCard.IsRunning,"both enabled cards stay bright while ready and waiting for the start");
   Show();timer.Stop();using(var shot=new System.Drawing.Bitmap(Width,Height)){DrawToBitmap(shot,new System.Drawing.Rectangle(0,0,Width,Height));shot.Save(Path.Combine(folder,"ui-ready-bright.png"),System.Drawing.Imaging.ImageFormat.Png);}Hide();
   recording=false;RefreshStatus();check(!recordCard.IsLit&&playCard.IsLit,"turning off recording dims only its own ready card");recording=true;playing=false;RefreshStatus();check(recordCard.IsLit&&!playCard.IsLit,"turning off playback dims only its own ready card");recording=playing=true;lifecycle.Selected=null;RefreshStatus();check(!playCard.IsLit,"playback remains dim without an imported recording");lifecycle.Selected=replay;race=savedRace;lifecycle.Capture=savedCapture;RefreshStatus();
   Show();timer.Stop();CheckLanguages(check,folder);connection.Text="运行状态示例（模拟）";using(var shot=new System.Drawing.Bitmap(Width,Height)){DrawToBitmap(shot,new System.Drawing.Rectangle(0,0,Width,Height));shot.Save(Path.Combine(folder,"ui-active-state-example.png"),System.Drawing.Imaging.ImageFormat.Png);}Hide();
   recording=false;RefreshStatus();check(!recordCard.IsRunning&&recordCard.SurfaceColor==UiTheme.Surface&&playCard.IsRunning,"stopping recording restores only its own gray card");check(recordCard.State.Text=="录制已关闭"&&playCard.State.Text=="正在回放","disabling recording leaves playback active");recording=true;playing=false;RefreshStatus();check(recordCard.State.Text=="正在录制"&&playCard.State.Text=="回放已关闭","disabling playback leaves recording active");check(recordCard.IsRunning&&!playCard.IsRunning&&playCard.SurfaceColor==UiTheme.Surface,"stopping playback restores only its own gray card");playback.Dispose();
  }
 }
}

