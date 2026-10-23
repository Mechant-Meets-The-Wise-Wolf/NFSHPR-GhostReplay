using System;
using System.IO;
namespace HprGhost {
 public class WholeRaceCapture {
  public readonly Replay Replay;readonly long vehicle,pool;long lastTick;
  public WholeRaceCapture(Frame zero,RaceState state,string label,string author,string hash){
   vehicle=zero.VehicleObject;pool=zero.VehicleData;lastTick=0;
   Replay=new Replay{GameHash=hash,EventId=state.EventId,EventLabel=label,Author=author,CreatedUtc=DateTime.UtcNow.ToString("o"),VehicleResourceId=zero.VehicleResourceId,VehicleTag=zero.VehicleTag};
   Replay.Samples.Add(new Sample{Time=0,Pose=zero.Pose,Speed=zero.Speed});
  }
  public void Push(Frame f,RaceState s,bool final){
   if(s.RaceTicks<lastTick)throw new IOException("车辆或赛事已重置，丢弃未完成录制。");
   if(f.VehicleResourceId!=0&&Replay.VehicleResourceId!=0&&f.VehicleResourceId!=Replay.VehicleResourceId)throw new IOException("车型已更换，丢弃未完成录制。");
   if(s.EventId!=Replay.EventId)throw new IOException("赛事已切换。");
   if(s.RaceTicks==lastTick)return;
   if(s.RaceTicks-lastTick>500000)throw new IOException("轨迹采样中断，未保存为完整赛事。");
   if(!final&&s.RaceTicks-lastTick<30000)return;
   if(Replay.Samples.Count>=HprGhost.Replay.MaxSamples)throw new IOException("达到轨迹容量上限。");
   Replay.Samples.Add(new Sample{Time=s.RaceTicks*1e-6,Pose=f.Pose,Speed=f.Speed});lastTick=s.RaceTicks;
  }
  public Replay Finish(Frame f,RaceState s){
   if(s.EventId!=Replay.EventId)throw new IOException("终点数据属于另一场赛事。");
   // The clock table drives all trajectory samples and playback. The official
   // result can exclude a start offset or include adjustments; it is not a
   // timestamp in that clock. Never cut trajectory data at the result value.
   double end=s.LiveTicks*1e-6,result=s.RaceTicks*1e-6;
   if(end<=0||result<=0)throw new IOException("终点计时不可用。");
   Sample last=Replay.Samples[Replay.Samples.Count-1];
   if(end<last.Time||end-last.Time>.5)throw new IOException("终点轨迹采样中断。");
   if(end==last.Time)Replay.Samples[Replay.Samples.Count-1]=new Sample{Time=end,Pose=f.Pose,Speed=f.Speed};
   else Replay.Samples.Add(new Sample{Time=end,Pose=f.Pose,Speed=f.Speed});
   if(Replay.Samples.Count<2)throw new IOException("有效赛事轨迹不足。");
   Replay.Completed=true;Replay.FinalTime=result;Replay.Validate();return Replay;
  }
 }
 public class RaceLifecycle {
  public bool RecordEnabled=true;public Replay Selected;public string Author="LoughShinny302";public string Status="等待进入赛事。";
  public WholeRaceCapture Capture;public event Action<Replay> Completed;
  RaceState previous;Frame preroll;bool attempted;uint roundEvent;
  public void Observe(RaceState s,Frame f,string label,string hash){
   bool changed=previous==null||s.EventId!=roundEvent||s.Active!=previous.Active;
   bool rewind=previous!=null&&s.Active&&previous.Active&&previous.Running&&s.Running&&!s.Finished&&s.RaceTicks+1000<previous.RaceTicks;
   bool restarted=previous!=null&&previous.Active&&s.Active&&!s.Running&&!s.Finished&&(previous.Running||previous.Finished);
   // The verified launch sequence resets its timer from 283322 to 49998 us
   // while remaining in the running state (part 7 -> 6). This is not a retry.
   if(!changed&&rewind&&previous.RaceTicks<1000000&&s.RaceTicks<500000&&f!=null){
    Capture=RecordEnabled?new WholeRaceCapture(ZeroFrame(null,f,s),s,label,Author,hash):null;
    preroll=null;attempted=true;rewind=false;Status="发车计时校准完成，自动录制中："+label;
   }
   if(changed||rewind||restarted){Capture=null;preroll=null;attempted=false;roundEvent=s.EventId;}
   if(!s.Active){Capture=null;preroll=null;attempted=false;Status="等待进入赛事。";previous=s;return;}
   if(!s.Running&&!s.Finished){if(f!=null)preroll=f;Status="赛事已加载，等待发车。";previous=s;return;}
   if(s.Running&&!attempted&&f!=null){
    attempted=true;
    if(RecordEnabled&&f!=null&&s.RaceTicks<=150000&&(preroll!=null||s.RaceTicks==0))Capture=new WholeRaceCapture(ZeroFrame(preroll,f,s),s,label,Author,hash);
    else if(RecordEnabled)Status="未观察到本场发车，等待下一场完整录制。";
   }
   if(Capture!=null&&f!=null){
    try{
     if(s.Finished){Replay done=Capture.Finish(f,s);Capture=null;Status="整场录制完成，已自动保存。";if(Completed!=null)Completed(done);}
     else if(s.Running){Capture.Push(f,s,false);Status="自动录制中："+label;}
    }catch(Exception ex){Capture=null;Status=ex.Message;AppLog.Failure(ex);}
   }else if(s.Finished)Status="赛事结束，等待下一次发车。";
   else if(!RecordEnabled)Status=Selected!=null&&Selected.EventId==s.EventId?"自动回放中："+label:"等待进入所选幽灵的赛事。";
   previous=s;
  }
  static Frame ZeroFrame(Frame pre,Frame current,RaceState state){
   if(state.RaceTicks==0)return current;
   if(pre!=null&&pre.VehicleObject==current.VehicleObject&&pre.VehicleData==current.VehicleData)return pre;
   // The game may replace the intro vehicle at release. Estimate only the first
   // fraction of a physics frame from the new object's authoritative velocity.
   float dt=(float)(state.RaceTicks*1e-6);
   var p=current.Pose;
   return new Frame{VehicleObject=current.VehicleObject,VehicleData=current.VehicleData,VehicleResourceId=current.VehicleResourceId,VehicleTag=current.VehicleTag,Speed=current.Speed,Pose=new Pose{Right=p.Right,Up=p.Up,Forward=p.Forward,Position=p.Position-current.Velocity*dt}};
  }
  public void Discard(){Capture=null;preroll=null;attempted=true;Status="未完成录制已丢弃，等待下次发车。";}
  public Sample Ghost(RaceState s){if(Selected==null||!Selected.Completed||s==null||!s.Active||!s.Running||s.EventId!=Selected.EventId)return null;return Selected.At(s.RaceTicks*1e-6);}
 }
}