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
 public static class FrameFenceProbe {
  static void Write(StreamWriter w,string mode,double wall,RaceState race,Frame f){
   Vec p=f.Pose.Position,v=f.Velocity;w.WriteLine(string.Format(CultureInfo.InvariantCulture,"{0},{1:R},{2},{3},{4:R},{5:R},{6:R},{7:R},{8:R},{9:R},{10:R}",mode,wall,race.LiveTicks,f.Tick,p.X,p.Y,p.Z,f.Speed,v.X,v.Y,v.Z));
  }
  public static int Run(string[] args){
   int seconds=args.Length>1?int.Parse(args[1],CultureInfo.InvariantCulture):15;if(seconds<1||seconds>60)throw new ArgumentException("Probe seconds must be 1..60");
   string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"research"),stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(folder);
   var fence=new FrameFence();var watch=Stopwatch.StartNew();int raw=0,done=0;long prevRaw=-1;string error="";
   using(var resolution=new SamplingWait())using(var reader=new GameReader())using(var w=new StreamWriter(Path.Combine(folder,"frame-fence-"+stamp+".csv"),false,new UTF8Encoding(false))){
    w.WriteLine("mode,wall_s,race_us,simulation_us,x,y,z,speed,vx,vy,vz");
    while(watch.Elapsed.TotalSeconds<seconds){
     RaceState race;Frame frame;
     if(reader.TryReadSnapshot(out race,out frame)){
      double wall=watch.Elapsed.TotalSeconds;
      if(frame!=null&&race.Running)Write(w,"raw",wall,race,frame);
      if(frame!=null&&race.Running&&frame.Tick!=prevRaw){Write(w,"first",wall,race,frame);raw++;prevRaw=frame.Tick;}
      RaceState readyRace;Frame ready;
      if(fence.Observe(race,frame,wall,out readyRace,out ready)&&ready!=null&&readyRace.Running){Write(w,"fenced",wall,readyRace,ready);done++;}
     }else error=reader.SnapshotProblem;
     resolution.Wait();
    }
    File.WriteAllText(Path.Combine(folder,"frame-fence-"+stamp+".txt"),"First samples: "+raw+"\r\nFenced samples: "+done+"\r\nPublished: "+fence.Published+"\r\nDropped: "+fence.Dropped+"\r\nRaw retries: "+reader.SnapshotRetries+"\r\nLast error: "+error);return done>0?0:1;
   }
  }
 }
 public static class FramePumpCheck {
  public static int Run(){
   int batches=0,frames=0,empty=0;string error="";long last=-1;bool monotonic=true;var clock=Stopwatch.StartNew();
   using(var reader=new GameReader())using(var pump=new FramePump(reader)){
    while(clock.Elapsed.TotalSeconds<3){Thread.Sleep(16);var batch=pump.Take();if(batch.Count>0)batches++;foreach(var item in batch){if(item.Frame==null)empty++;else {frames++;if(item.Frame.Tick<=last)monotonic=false;last=item.Frame.Tick;}}error=pump.Error;}
   }
   string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"research");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"frame-pump-check-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".txt"),"Batches: "+batches+"\r\nFrames: "+frames+"\r\nMenu states: "+empty+"\r\nMonotonic simulation ticks: "+monotonic+"\r\nLast error: "+error+"\r\nShutdown completed: true");return batches>0&&monotonic?0:1;
  }
 }
 public static class FrameSamplingTests {
  static Frame F(long tick,float x){return new Frame{Tick=tick,VehicleObject=100,VehicleData=200,Pose=new Quat(0,0,0,1).Pose(new Vec(x,0,0)),Velocity=new Vec(60,0,0),Speed=60,Camera=new Camera{ViewProjection=new float[]{1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1}}};}
  static RaceState R(long tick){return new RaceState{Active=true,Running=true,EventId=1,LiveTicks=tick,RaceTicks=tick};}
  public static void Run(Action<bool,string> check){
   var fence=new FrameFence();RaceState r;Frame f;
   check(!fence.Observe(R(0),F(0,0),0,out r,out f),"fence does not publish a start-of-update observation");
   fence.Observe(R(0),F(0,0),.004,out r,out f);fence.Observe(R(0),F(0,0),.015,out r,out f);
   check(fence.Observe(R(16666),F(16666,0),.017,out r,out f)&&r.LiveTicks==0&&f.Pose.Position.X==0,"next tick publishes the preceding clock and pose together");
   fence.Observe(R(16666),F(16666,1),.018,out r,out f);fence.Observe(R(16666),F(16666,1),.021,out r,out f);fence.Observe(R(16666),F(16666,1),.032,out r,out f);
   check(fence.Observe(R(33332),F(33332,1),.034,out r,out f)&&r.LiveTicks==16666&&f.Pose.Position.X==1,"old then new pose within one clock retains the final original pose without fitting");
   check(!fence.Observe(R(49998),F(49998,3),.051,out r,out f)&&fence.Dropped==1,"missed polling interval is dropped instead of guessing a pose timestamp");
   fence.Observe(R(49998),F(49998,3),.064,out r,out f);fence.Observe(R(49998),F(49998,4),.065,out r,out f);
   check(!fence.Observe(R(66664),F(66664,4),.067,out r,out f)&&fence.Dropped==2,"candidate still changing near the clock boundary is dropped");
   check(!fence.Observe(R(0),F(0,0),.068,out r,out f),"race rewind clears pending frame ownership");
   check(fence.Observe(new RaceState(),null,.069,out r,out f)&&!r.Active&&f==null,"menu transition clears pending data immediately");
   fence.Observe(R(100000),F(100000,5),.1,out r,out f);fence.Observe(R(100000),F(100000,5),.104,out r,out f);fence.Observe(R(100000),F(100000,5),.115,out r,out f);
   check(fence.Observe(R(116666),F(116666,5),.117,out r,out f)&&f.Pose.Position.X==5,"stationary pose can advance race time without requiring camera motion");
   var clock=Stopwatch.StartNew();int queued=0;long lastTick=-1;bool order=true;string pumpError="";
   using(var pump=new FramePump(delegate {long tick=(long)(clock.Elapsed.TotalSeconds/.016666);return new FrameSnapshot{Race=R(tick*16666),Frame=F(tick*16666,(float)tick)};})){
    while(clock.Elapsed.TotalSeconds<.25){Thread.Sleep(10);foreach(var item in pump.Take()){queued++;if(item.Frame.Tick<=lastTick)order=false;lastTick=item.Frame.Tick;}}pumpError=pump.Error;
   }
   check(queued>=2&&order&&pumpError=="","background pump delivers ordered immutable snapshots and joins on disposal");
  }
 }

}
