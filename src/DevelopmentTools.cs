using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
namespace HprGhost {
 public class Recorder {
  public Replay Replay;public readonly long StartTick,Object,Pool;long lastTick;
  public Recorder(Frame f,string label,string author,string hash){
   StartTick=lastTick=f.Tick;Object=f.VehicleObject;Pool=f.VehicleData;
   Replay=new Replay{GameHash=hash,EventLabel=label,Author=author,CreatedUtc=DateTime.UtcNow.ToString("o"),VehicleResourceId=f.VehicleResourceId,VehicleTag=f.VehicleTag};
   Replay.Samples.Add(new Sample{Time=0,Pose=f.Pose,Speed=f.Speed});
  }
  public void Push(Frame f){
   if(f.VehicleObject!=Object||f.VehicleData!=Pool||f.VehicleTag!=Replay.VehicleTag||f.Tick<lastTick)throw new IOException("赛事或车辆已切换，录制结束。");
   if(f.Tick-lastTick<30000)return; // simulation clock: 1 tick = 1 microsecond
   double t=(f.Tick-StartTick)*1e-6;
   if(t>21600||Replay.Samples.Count>=HprGhost.Replay.MaxSamples)throw new IOException("达到录制时长上限。");
   Sample prev=Replay.Samples[Replay.Samples.Count-1];double dt=t-prev.Time;
   if((f.Pose.Position-prev.Pose.Position).Length>Math.Max(100,250*dt))throw new IOException("检测到车辆重置，录制结束。");
   Replay.Samples.Add(new Sample{Time=t,Pose=f.Pose,Speed=f.Speed});lastTick=f.Tick;
  }
 }
 public class Overlay : Form {
  public Frame Frame;public Replay Replay;public double Time;public bool DrawGhost;
  public Overlay(){FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;BackColor=Color.Magenta;TransparencyKey=Color.Magenta;DoubleBuffered=true;}
  protected override bool ShowWithoutActivation {get{return true;}}
  protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x80000|0x20|0x08000000|0x80;return p;}}
  public void Place(GameReader reader){
   IntPtr window=reader.Window;Native.Rect rect;Native.Point origin=new Native.Point();
   if(window==IntPtr.Zero||Native.IsIconic(window)||!Native.GetClientRect(window,out rect)||!Native.ClientToScreen(window,ref origin)){Hide();return;}
   int w=rect.Right-rect.Left,h=rect.Bottom-rect.Top;if(w<=0||h<=0){Hide();return;}
   if(!Visible)Show();Native.SetWindowPos(Handle,new IntPtr(-1),origin.X,origin.Y,w,h,0x10|0x40);Invalidate();
  }
  void Line(Graphics g,Pen pen,Camera camera,Vec a,Vec b){PointF x,y;if(camera.Segment(a,b,ClientSize.Width,ClientSize.Height,out x,out y))g.DrawLine(pen,x,y);}
  protected override void OnPaint(PaintEventArgs e){
   base.OnPaint(e);if(Frame==null)return;Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.None;
   if(!DrawGhost||Replay==null)return;Sample ghost=Replay.At(Time);if(ghost==null)return;
   Camera cam=Frame.Camera;Pose pose=ghost.Pose;
   // Generic car silhouette: body, cabin, and four wheels.
   Vec[] body=new Vec[8];for(int i=0;i<8;i++)body[i]=pose.Local((i&1)==0?-.95f:.95f,(i&2)==0?.22f:.72f,(i&4)==0?-2.2f:2.2f);
   Vec[] roof=new Vec[4];for(int i=0;i<4;i++)roof[i]=pose.Local((i&1)==0?-.7f:.7f,1.28f,(i&2)==0?-1.2f:.65f);
   using(var pen=new Pen(Color.Cyan,2)){
    for(int i=0;i<8;i++)for(int bit=1;bit<=4;bit*=2)if((i&bit)==0)Line(g,pen,cam,body[i],body[i|bit]);
    for(int i=0;i<4;i++)for(int bit=1;bit<=2;bit*=2)if((i&bit)==0)Line(g,pen,cam,roof[i],roof[i|bit]);
    for(int i=0;i<4;i++)Line(g,pen,cam,roof[i],body[(i&1)|2|((i&2)*2)]);
    for(int side=-1;side<=1;side+=2)for(int axle=-1;axle<=1;axle+=2){
     Vec previous=pose.Local(side, .35f,axle*1.4f+.32f);
     for(int j=1;j<=12;j++){double a=j*Math.PI/6;Vec next=pose.Local(side,.35f+.32f*(float)Math.Sin(a),axle*1.4f+.32f*(float)Math.Cos(a));Line(g,pen,cam,previous,next);previous=next;}
    }
   }
  }
 }
 public static class DevelopmentCommands {
  public static bool TryRun(string[] args,out int result){result=0;if(args.Length==0)return false;switch(args[0]){case "--native-integration-ui":result=NativeUiCheck.Run(args);return true;case "--self-test":result=SelfTests.Run();return true;case "--frame-pump-check":result=FramePumpCheck.Run();return true;case "--frame-fence-probe":result=FrameFenceProbe.Run(args);return true;case "--timing-probe":result=TimingProbe.Run(args);return true;case "--model-check":result=ModelChecks.Run(args);return true;case "--diagnose":result=Diagnose(args);return true;default:return false;}}
  static int Diagnose(string[] args){
   int seconds=args.Length>1?int.Parse(args[1],CultureInfo.InvariantCulture):3;if(seconds<1||seconds>60)throw new ArgumentException("诊断时长必须为 1–60 秒。");
   string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"research");Directory.CreateDirectory(folder);
   using(var reader=new GameReader())using(var report=new StreamWriter(Path.Combine(folder,"live-samples.csv"),false,new UTF8Encoding(false))){
    report.WriteLine("wall_ms,game_tick,x,y,z,speed_mps,camera_x,camera_y,camera_z,projection_x,projection_y,vehicle_index");
    var watch=Stopwatch.StartNew();Recorder recorder=null;Frame first=null,last=null;int valid=0,errors=0;string lastError="";
    while(watch.Elapsed.TotalSeconds<seconds){
     try{Frame f=reader.ReadFrame();if(first==null)first=f;last=f;valid++;if(recorder==null)recorder=new Recorder(f,"diagnostic capture","diagnostic",reader.Hash);recorder.Push(f);
      PointF projected;f.Camera.Project(f.Pose.Position,1000,1000,out projected);
      report.WriteLine(string.Join(",",new string[]{watch.ElapsedMilliseconds.ToString(),f.Tick.ToString(),f.Pose.Position.X.ToString("R",CultureInfo.InvariantCulture),f.Pose.Position.Y.ToString("R",CultureInfo.InvariantCulture),f.Pose.Position.Z.ToString("R",CultureInfo.InvariantCulture),f.Speed.ToString("R",CultureInfo.InvariantCulture),f.Camera.Position.X.ToString("R",CultureInfo.InvariantCulture),f.Camera.Position.Y.ToString("R",CultureInfo.InvariantCulture),f.Camera.Position.Z.ToString("R",CultureInfo.InvariantCulture),projected.X.ToString("R",CultureInfo.InvariantCulture),projected.Y.ToString("R",CultureInfo.InvariantCulture),f.VehicleIndex.ToString()}));
     }catch(Exception ex){errors++;lastError=ex.Message;}Thread.Sleep(30);
    }
    if(recorder!=null&&recorder.Replay.Samples.Count>=2)recorder.Replay.Save(Path.Combine(folder,"diagnostic.hprghost"));
    File.WriteAllText(Path.Combine(folder,"live-validation.txt"),"Game SHA256: "+reader.Hash+"\r\nValid samples: "+valid+"\r\nRead errors: "+errors+"\r\nLast error: "+lastError+"\r\nClock advance seconds: "+(first==null?0:(last.Tick-first.Tick)*1e-6).ToString("F6",CultureInfo.InvariantCulture)+"\r\nEndpoint displacement metres: "+(first==null?0:(last.Pose.Position-first.Pose.Position).Length).ToString("F3",CultureInfo.InvariantCulture)+"\r\nRecorded points: "+(recorder==null?0:recorder.Replay.Samples.Count));return valid==0?1:0;
   }
  }
 }
}
