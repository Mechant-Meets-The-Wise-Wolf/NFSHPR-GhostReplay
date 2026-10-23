using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
namespace HprGhost {
 // The simulation clock is a START-of-update stamp, not a publication fence.
 // Keep replacing each tick's candidate and publish it only at the next tick.
 // No averaging, prediction, or edits to a sampled position are performed.
 public sealed class FrameFence {
  Frame pending;RaceState pendingRace;double lastWall,unchangedSince;int repeats;
#if DEVELOPMENT
  public long Published,Dropped;
#endif
  public void Reset(){pending=null;pendingRace=null;repeats=0;}
  static bool Same(Vec a,Vec b){return a.X==b.X&&a.Y==b.Y&&a.Z==b.Z;}
  static bool SameFrame(Frame a,Frame b){
   if(a.VehicleObject!=b.VehicleObject||a.VehicleData!=b.VehicleData||!Same(a.Pose.Position,b.Pose.Position)||!Same(a.Pose.Right,b.Pose.Right)||!Same(a.Pose.Up,b.Pose.Up)||!Same(a.Pose.Forward,b.Pose.Forward)||!Same(a.Velocity,b.Velocity))return false;
   float[] x=a.Camera.ViewProjection,y=b.Camera.ViewProjection;
   if(x==null||y==null||x.Length!=16||y.Length!=16)return false;
   for(int i=0;i<16;i++)if(x[i]!=y[i])return false;return true;
  }
  public bool Observe(RaceState race,Frame frame,double wall,out RaceState readyRace,out Frame ready){
   readyRace=null;ready=null;
   if(frame==null||!race.Active){Reset();readyRace=race;ready=frame;return true;}
   if(pending!=null){
    bool sameRound=race.EventId==pendingRace.EventId&&frame.VehicleData==pending.VehicleData&&race.LiveTicks>=pendingRace.LiveTicks;
    if(sameRound&&frame.Tick>pending.Tick){
     // Reject a missed polling interval or a still-changing final observation.
     // These limits are deliberately observable in the probe, not silent guesses.
     if(wall>=lastWall&&wall-lastWall<=.004&&lastWall-unchangedSince>=.002&&repeats>=2&&frame.Tick-pending.Tick<=100000){readyRace=pendingRace;ready=pending;
#if DEVELOPMENT
Published++;
#endif
}
     else {
#if DEVELOPMENT
Dropped++;
#endif
}
    }else if(sameRound&&frame.Tick==pending.Tick){
     if(race.LiveTicks==pendingRace.LiveTicks&&race.Running==pendingRace.Running&&race.Finished==pendingRace.Finished&&SameFrame(pending,frame))repeats++;
     else {unchangedSince=wall;repeats=1;}
     pending=frame;pendingRace=race;lastWall=wall;return false;
    }
   }
   pending=frame;pendingRace=race;lastWall=unchangedSince=wall;repeats=1;return ready!=null;
  }
 }
 public sealed class SamplingWait : IDisposable {
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateWaitableTimerEx(IntPtr attributes,string name,uint flags,uint access);
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetWaitableTimer(IntPtr timer,ref long due,int period,IntPtr completion,IntPtr argument,bool resume);
  [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr handle,uint timeout);
  readonly IntPtr timer;
  public SamplingWait(){timer=CreateWaitableTimerEx(IntPtr.Zero,null,2,0x1f0003);if(timer==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"High resolution sampling timer unavailable");}
  public void Wait(){long due=-10000;if(!SetWaitableTimer(timer,ref due,0,IntPtr.Zero,IntPtr.Zero,false)||WaitForSingleObject(timer,1000)!=0)throw new IOException("Sampling timer failed");}
  public void Dispose(){Native.CloseHandle(timer);}
 }
 public sealed class FrameSnapshot {public RaceState Race;public Frame Frame;}
 public sealed class FramePump : IDisposable {
  readonly GameReader reader;
#if DEVELOPMENT
  readonly Func<FrameSnapshot> readOverride;
#endif
  readonly Thread worker;readonly object gate=new object();readonly Queue<FrameSnapshot> queue=new Queue<FrameSnapshot>();
  volatile bool stopping;string error="";bool overflow;
  public string Error {get{lock(gate)return error;}}
#if DEVELOPMENT
  public FramePump(GameReader source):this(source,null){}
  internal FramePump(Func<FrameSnapshot> source):this(null,source){}
  FramePump(GameReader source,Func<FrameSnapshot> read){readOverride=read;
#else
  public FramePump(GameReader source){
#endif
   reader=source;worker=new Thread(Run);worker.IsBackground=true;worker.Name="HPR completed-frame sampler";worker.Start();}

  void Run(){
   try{using(var wait=new SamplingWait()){
    var clock=Stopwatch.StartNew();var fence=new FrameFence();RaceState lastEmpty=null;
    while(!stopping){
     RaceState race=null;Frame frame=null;bool readOk;
#if DEVELOPMENT
     if(readOverride!=null){var snapshot=readOverride();readOk=snapshot!=null;if(readOk){race=snapshot.Race;frame=snapshot.Frame;}}
     else
#endif
     readOk=reader.TryReadSnapshot(out race,out frame);
     if(readOk){
      RaceState readyRace;Frame ready;
      if(fence.Observe(race,frame,clock.Elapsed.TotalSeconds,out readyRace,out ready)){
       bool duplicate=ready==null&&lastEmpty!=null&&lastEmpty.EventId==readyRace.EventId&&lastEmpty.Active==readyRace.Active&&lastEmpty.Running==readyRace.Running&&lastEmpty.Finished==readyRace.Finished;
       if(!duplicate){lock(gate){if(queue.Count>=240){overflow=true;queue.Clear();}queue.Enqueue(new FrameSnapshot{Race=readyRace,Frame=ready});error="";}}
       lastEmpty=ready==null?readyRace:null;
      }
     }else if(reader!=null){lock(gate)error=reader.SnapshotProblem;if(reader.Process.HasExited)break;}
     wait.Wait();
    }
   }}catch(Exception ex){lock(gate)error=ex.Message;}
  }
  public List<FrameSnapshot> Take(){lock(gate){if(overflow){overflow=false;queue.Clear();throw new IOException("采样队列中断，请重新进入赛事后录制。");}var result=new List<FrameSnapshot>(queue);queue.Clear();return result;}}
  public void Dispose(){stopping=true;worker.Join();}
 }
}
