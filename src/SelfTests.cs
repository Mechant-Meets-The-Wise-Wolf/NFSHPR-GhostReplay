using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace HprGhost {
 public static class SelfTests {
  static List<string> results=new List<string>();
  static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);results.Add("PASS: "+name);}
  static bool Rejected(Action action){try{action();return false;}catch(InvalidDataException){return true;}catch(EndOfStreamException){return true;}}
  static Pose PoseAt(float yaw,Vec pos){double a=yaw*Math.PI/180;return new Quat(0,Math.Sin(a/2),0,Math.Cos(a/2)).Pose(pos);}
  static Frame FrameAt(long tick,Vec position){return new Frame{Tick=tick,VehicleObject=123456,VehicleData=654321,VehicleTag=0x5918,Pose=PoseAt(0,position),Speed=10};}
  public static int Run(){
   string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"research");Directory.CreateDirectory(folder);string tmp=Path.Combine(folder,"selftest-"+Guid.NewGuid().ToString("N")+".hprghost");
   try{
    NativeTests.Run(Check,folder);
    ReleaseLogTests.Run(Check,folder);
    LogTests.Run(Check,folder);
    FrameSamplingTests.Run(Check);
    UiTests.Run(Check,folder);
    Pose p=PoseAt(90,new Vec(10,20,30));Check(p.Valid&&Math.Abs(p.Forward.X-1)<.001,"rotation basis from quaternion");
    Pose wrap=Pose.Blend(PoseAt(179,new Vec()),PoseAt(-179,new Vec()),.5f);Check(wrap.Valid&&wrap.Forward.Z<-.999f,"shortest rotation across 180 degrees");
    Pose restored=Quat.FromPose(p).Pose(p.Position);Check((restored.Forward-p.Forward).Length<.001&&(restored.Right-p.Right).Length<.001,"matrix and quaternion roundtrip");
    var camera=new Camera{Right=new Vec(1,0,0),Up=new Vec(0,1,0),Forward=new Vec(0,0,1),HorizontalScale=1,VerticalScale=2};PointF pixel,a,b;
    Check(camera.Valid&&camera.Project(new Vec(0,0,10),1000,500,out pixel)&&pixel.X==500&&pixel.Y==250,"camera projection at screen centre");
    Check(!camera.Project(new Vec(0,0,-1),1000,500,out pixel),"reject points behind camera");
    Check(camera.Segment(new Vec(0,0,-1),new Vec(0,0,5),1000,500,out a,out b),"clip a segment at near plane");
    Check(camera.Segment(new Vec(-100,0,1),new Vec(100,0,1),1000,500,out a,out b)&&a.X>=0&&b.X<=1000,"clip extreme projection to viewport");
    var recorder=new Recorder(FrameAt(1000000,new Vec()),"测试赛道","高手",GameReader.SupportedHash);
    recorder.Push(FrameAt(1000000,new Vec()));Check(recorder.Replay.Samples.Count==1,"paused clock produces no duplicate samples");
    recorder.Push(FrameAt(1033333,new Vec(0,0,.33333f)));recorder.Push(FrameAt(1066666,new Vec(0,0,.66666f)));
    recorder.Replay.VehicleResourceId=329012;recorder.Replay.Save(tmp);Replay loaded=Replay.Load(tmp);
    Check(loaded.Samples.Count==3&&loaded.Author=="高手"&&loaded.EventLabel=="测试赛道"&&Math.Abs(loaded.Duration-.066666)<1e-7,"binary replay roundtrip with Unicode metadata");
    Check(loaded.VehicleResourceId==329012,"vehicle resource ID survives current-format file sharing");
    Sample mid=loaded.At(.05);Check(mid!=null&&Math.Abs(mid.Pose.Position.Z-.5f)<.001,"time-indexed interpolation");
    Check(loaded.At(-.01)==null&&loaded.At(.07)==null,"playback range and finish");
    Frame wrong=FrameAt(1100000,new Vec());wrong.VehicleObject=999999;bool stopped=false;try{recorder.Push(wrong);}catch(IOException){stopped=true;}Check(stopped,"stop recording on vehicle transition");
    stopped=false;try{recorder.Push(FrameAt(100000,new Vec()));}catch(IOException){stopped=true;}Check(stopped,"stop recording on clock rewind");
    byte[] valid=File.ReadAllBytes(tmp);
    int carOffset=valid.Length-3*60-21;byte[] legacy=new byte[valid.Length-4];Array.Copy(valid,0,legacy,0,carOffset);Array.Copy(valid,carOffset+4,legacy,carOffset,valid.Length-carOffset-4);Array.Copy(BitConverter.GetBytes(2),0,legacy,8,4);File.WriteAllBytes(tmp,legacy);Replay legacyReplay=Replay.Load(tmp);Check(legacyReplay.VehicleResourceId==0&&legacyReplay.Samples.Count==3&&legacyReplay.Author=="高手","version 2 files remain readable without guessing vehicle identity");
    File.WriteAllBytes(tmp,new byte[]{0x48});Check(Rejected(delegate{Replay.Load(tmp);}),"reject truncated replay");
    byte[] nan=(byte[])valid.Clone();byte[] nanBytes=BitConverter.GetBytes(double.NaN);int start=valid.Length-3*60;Array.Copy(nanBytes,0,nan,start,8);File.WriteAllBytes(tmp,nan);Check(Rejected(delegate{Replay.Load(tmp);}),"reject nonfinite sample time");
    byte[] count=(byte[])valid.Clone();Array.Copy(BitConverter.GetBytes(int.MaxValue),0,count,start-4,4);File.WriteAllBytes(tmp,count);Check(Rejected(delegate{Replay.Load(tmp);}),"reject malicious sample count before allocation");
    byte[] junk=new byte[valid.Length+1];Array.Copy(valid,junk,valid.Length);File.WriteAllBytes(tmp,junk);Check(Rejected(delegate{Replay.Load(tmp);}),"reject appended data and inconsistent length");
    loaded.Samples[1].Time=loaded.Samples[0].Time;Check(Rejected(delegate{loaded.Validate();}),"reject nonmonotonic time");
    Replay gap=new Replay{GameHash=GameReader.SupportedHash};gap.Samples.Add(new Sample{Time=0,Pose=PoseAt(0,new Vec()),Speed=1});gap.Samples.Add(new Sample{Time=2,Pose=PoseAt(0,new Vec(0,0,2)),Speed=1});Check(gap.At(1)==null,"do not bridge missing simulation intervals");
    var lifecycle=new RaceLifecycle();Replay completed=null;int completions=0;lifecycle.Completed+=delegate(Replay r){completed=r;completions++;};
    var before=new RaceState{Active=true,EventId=321154};var running=new RaceState{Active=true,Running=true,EventId=321154,RaceTicks=0,LiveTicks=0};
    lifecycle.Observe(before,FrameAt(0,new Vec()),"自动赛事","test");lifecycle.Observe(running,FrameAt(0,new Vec()),"自动赛事",GameReader.SupportedHash);
    Check(lifecycle.Capture!=null,"automatic recording starts at race timer transition");
    lifecycle.Observe(new RaceState{Active=true,Running=true,EventId=321154,RaceTicks=33333,LiveTicks=33333},FrameAt(33333,new Vec(0,0,.33333f)),"自动赛事",GameReader.SupportedHash);
    lifecycle.Observe(new RaceState{Active=true,Running=true,EventId=321154,RaceTicks=66666,LiveTicks=66666},FrameAt(66666,new Vec(0,0,.66666f)),"自动赛事",GameReader.SupportedHash);
    lifecycle.Observe(new RaceState{Active=true,Running=true,EventId=321154,RaceTicks=100000,LiveTicks=100000},FrameAt(100000,new Vec(0,0,1)),"自动赛事",GameReader.SupportedHash);
    var finish=new RaceState{Active=true,Finished=true,EventId=321154,RaceTicks=90000,LiveTicks=110000};
    lifecycle.Observe(finish,FrameAt(110000,new Vec(0,0,1.1f)),"自动赛事",GameReader.SupportedHash);
    Check(completions==1&&completed.Completed&&Math.Abs(completed.Duration-.11)<1e-7&&Math.Abs(completed.FinalTime-.09)<1e-7&&Math.Abs(completed.Samples[completed.Samples.Count-1].Pose.Position.Z-1.1)<.001,"finish keeps the trajectory clock separate from official result");
    lifecycle.Observe(finish,FrameAt(120000,new Vec(0,0,1.2f)),"自动赛事",GameReader.SupportedHash);Check(completions==1,"finish is saved once despite repeated results frames");
    completed.Save(tmp);var round=Replay.Load(tmp);Check(round.Completed&&round.EventId==321154&&Math.Abs(round.FinalTime-.09)<1e-7,"complete event identity survives file sharing");
    lifecycle.Selected=round;Check(lifecycle.Ghost(new RaceState{Active=true,Running=true,EventId=123,RaceTicks=30000})==null,"ghost is not shown in a different event");
    Check(lifecycle.Ghost(new RaceState{Active=true,Running=true,EventId=321154,RaceTicks=30000})!=null,"ghost follows current race timer without a manual start");
    lifecycle.Observe(before,FrameAt(0,new Vec()),"自动赛事",GameReader.SupportedHash);lifecycle.Observe(running,FrameAt(0,new Vec()),"自动赛事",GameReader.SupportedHash);Check(lifecycle.Capture!=null,"same event restart rearms automatic recording");
    lifecycle.Observe(new RaceState(),null,"menu",GameReader.SupportedHash);Check(lifecycle.Capture==null&&completions==1,"quit discards incomplete race without saving");
    var late=new RaceLifecycle();late.Observe(new RaceState{Active=true,Running=true,EventId=321154,RaceTicks=2000000},FrameAt(2000000,new Vec()),"late",GameReader.SupportedHash);Check(late.Capture==null,"joining midway is not mislabeled as a complete race");
    var replaced=new RaceLifecycle();replaced.Observe(before,FrameAt(0,new Vec()),"replace",GameReader.SupportedHash);
    Frame replacement=FrameAt(16000,new Vec(0,0,.16f));replacement.VehicleObject=999999;replacement.VehicleData=777777;replacement.Velocity=new Vec(0,0,10);replacement.VehicleResourceId=329012;
    replaced.Observe(new RaceState{Active=true,Running=true,EventId=321154,RaceTicks=16000,LiveTicks=16000},replacement,"replace",GameReader.SupportedHash);
    Check(replaced.Capture!=null&&replaced.Capture.Replay.Samples[0].Pose.Position.Length<.001,"intro vehicle replacement preserves automatic start");
    Check(replaced.Capture.Replay.VehicleResourceId==329012,"launch extrapolation preserves recorded vehicle identity");
    Frame replacement2=FrameAt(50000,new Vec(0,0,.5f));replacement2.VehicleObject=888888;
    replaced.Observe(new RaceState{Active=true,Running=true,EventId=321154,RaceTicks=50000,LiveTicks=50000},replacement2,"replace",GameReader.SupportedHash);
    Check(replaced.Capture!=null,"physical object replacement alone is not treated as race restart");
    var bundleChange=new RaceLifecycle();bundleChange.Observe(before,FrameAt(0,new Vec()),"bundle",GameReader.SupportedHash);bundleChange.Observe(running,FrameAt(0,new Vec()),"bundle",GameReader.SupportedHash);
    bundleChange.Observe(new RaceState{Active=true,Running=true,EventId=321154,RaceTicks=33333,LiveTicks=33333},FrameAt(33333,new Vec()),"bundle",GameReader.SupportedHash);
    Check(bundleChange.Capture!=null,"resource handle replacement alone does not restart a race");
    var staleTimer=new RaceLifecycle();staleTimer.Observe(new RaceState{Active=true,EventId=321154,RaceTicks=90000000},FrameAt(0,new Vec()),"stale",GameReader.SupportedHash);
    staleTimer.Observe(new RaceState{Active=true,Running=true,EventId=321154,RaceTicks=16000,LiveTicks=16000},FrameAt(16000,new Vec()),"stale",GameReader.SupportedHash);
    Check(staleTimer.Capture!=null,"timer reset at release preserves observed pre-start pose");
    var calibration=new RaceLifecycle();Replay calibrated=null;calibration.Completed+=delegate(Replay r){calibrated=r;};
    calibration.Observe(before,FrameAt(0,new Vec()),"calibration",GameReader.SupportedHash);calibration.Observe(running,FrameAt(0,new Vec()),"calibration",GameReader.SupportedHash);
    calibration.Observe(new RaceState{Active=true,Running=true,EventId=321154,RaceTicks=283322,LiveTicks=283322},FrameAt(283322,new Vec(0,0,2.83322f)),"calibration",GameReader.SupportedHash);
    Frame syncFrame=FrameAt(49998,new Vec(0,0,.49998f));syncFrame.Velocity=new Vec(0,0,10);
    calibration.Observe(new RaceState{Active=true,Running=true,EventId=321154,RaceTicks=49998,LiveTicks=49998},syncFrame,"calibration",GameReader.SupportedHash);
    Check(calibration.Capture!=null&&calibration.Capture.Replay.Samples[0].Pose.Position.Length<.001,"observed 283ms to 50ms launch calibration keeps full recording armed");
    calibration.Observe(new RaceState{Active=true,Running=true,EventId=321154,RaceTicks=100000,LiveTicks=100000},FrameAt(100000,new Vec(0,0,1)),"calibration",GameReader.SupportedHash);
    calibration.Observe(finish,FrameAt(110000,new Vec(0,0,1.1f)),"calibration",GameReader.SupportedHash);
    Check(calibrated!=null&&calibrated.Completed&&Math.Abs(calibrated.Duration-.11)<1e-7,"calibrated launch can finish and save a complete race");
    var offsetLife=new RaceLifecycle();Replay offsetDone=null;offsetLife.Completed+=delegate(Replay r){offsetDone=r;};
    offsetLife.Observe(new RaceState{Active=true,EventId=4000008},FrameAt(0,new Vec()),"CHARGED ATTACK",GameReader.SupportedHash);
    offsetLife.Observe(new RaceState{Active=true,Running=true,EventId=4000008,RaceTicks=0,LiveTicks=0},FrameAt(0,new Vec()),"CHARGED ATTACK",GameReader.SupportedHash);
    for(int i=1;i<=541;i++){long tick=i*500000L;offsetLife.Observe(new RaceState{Active=true,Running=true,EventId=4000008,RaceTicks=tick,LiveTicks=tick},FrameAt(tick,new Vec(0,0,i*5)),"CHARGED ATTACK",GameReader.SupportedHash);}
    offsetLife.Observe(new RaceState{Active=true,Finished=true,EventId=4000008,RaceTicks=263806114,LiveTicks=270672506},FrameAt(270672506,new Vec(0,0,2706.725f)),"CHARGED ATTACK",GameReader.SupportedHash);
    Check(offsetDone!=null&&Math.Abs(offsetDone.Duration-270.672506)<1e-7&&Math.Abs(offsetDone.FinalTime-263.806114)<1e-7,"CHARGED ATTACK result offset cannot delete the final trajectory seconds");
    Check(offsetDone.At(269)!=null&&offsetDone.At(269).Pose.Position.Z>2689,"ghost remains playable after the official result time");
    offsetDone.Save(tmp);Replay offsetFile=Replay.Load(tmp);Check(offsetFile.FormatVersion==4&&Math.Abs(offsetFile.Duration-270.672506)<1e-7&&Math.Abs(offsetFile.FinalTime-263.806114)<1e-7,"version 4 preserves separate trajectory duration and official result");
    var nativeCamera=new Camera{Right=new Vec(1,0,0),Up=new Vec(0,1,0),Forward=new Vec(0,0,1),HorizontalScale=1,VerticalScale=1,ViewProjection=new float[]{-1,0,0,0,0,1,0,0,0,0,1,1,0,0,-.15f,0}};
    Check(nativeCamera.Project(new Vec(2,0,10),1000,500,out pixel)&&Math.Abs(pixel.X-400)<.001,"native view-projection preserves the game horizontal convention");
    var matrix=ModelOverlay.Projection(nativeCamera.ViewProjection);var projected=matrix.Transform(new System.Windows.Media.Media3D.Point3D(2,0,10));Check(Math.Abs(500*(1+projected.X)-400)<.001,"model renderer uses the same native projection convention");
    Check(!nativeCamera.Project(new Vec(2,0,-10),1000,500,out pixel),"native matrix rejects geometry behind camera");
    Check(nativeCamera.Segment(new Vec(-100,0,1),new Vec(100,0,1),1000,500,out a,out b)&&a.X>=0&&b.X<=1000,"native matrix clips projected geometry to viewport");
    string cameraFile=Path.Combine(folder,"camera-data.bin");if(File.Exists(cameraFile)){
     byte[] cb=File.ReadAllBytes(cameraFile);Func<int,Vec> rv=delegate(int o){return new Vec(BitConverter.ToSingle(cb,o),BitConverter.ToSingle(cb,o+4),BitConverter.ToSingle(cb,o+8));};float[] vp=new float[16];for(int i=0;i<16;i++)vp[i]=BitConverter.ToSingle(cb,0x90+4*i);
     var actualCamera=new Camera{Right=rv(0x110),Up=rv(0x120),Forward=rv(0x130),Position=rv(0x140),HorizontalScale=BitConverter.ToSingle(cb,0x154),VerticalScale=BitConverter.ToSingle(cb,0x160),ViewProjection=vp};
     Vec offsetPoint=actualCamera.Position+actualCamera.Forward*10+actualCamera.Right*2;float expectedX=500-100*actualCamera.HorizontalScale;
     Check(actualCamera.Valid&&actualCamera.Project(offsetPoint,1000,500,out pixel)&&Math.Abs(pixel.X-expectedX)<1,"actual game matrix regression: camera basis previously mirrored the shadow");
    }
    string alternateHash="677288db0b867c512a821436158867662302b3c9e235b98a1c3ea90b48436270";
    Func<long,int,byte[]> matchingCode=delegate(long rva,int byteCount){foreach(var anchor in GameLayout.Anchors)if(anchor.Rva==rva)return (byte[])anchor.Bytes.Clone();throw new Exception("unexpected runtime read");};
    GameLayout.Validate(GameReader.SupportedHash,matchingCode);GameLayout.Validate(alternateHash,matchingCode);
    Check(true,"different EXE hashes are accepted when required runtime signatures match");
    bool layoutRejected=false;try{GameLayout.Validate(alternateHash,delegate(long rva,int byteCount){byte[] bytes=matchingCode(rva,byteCount);if(rva==0x29c077)bytes[0]^=1;return bytes;});}catch(InvalidOperationException){layoutRejected=true;}
    Check(layoutRejected,"unknown EXE with incompatible camera code remains rejected");
    Check(Rejected(delegate{GameLayout.Validate(alternateHash,delegate(long rva,int byteCount){return new byte[byteCount-1];});}),"incomplete runtime signature read remains rejected");
    offsetFile.GameHash=alternateHash;offsetFile.Save(tmp);var alternateReplay=Replay.Load(tmp);
    Check(GameLayout.CanPlay(alternateReplay)&&alternateReplay.GameHash==alternateHash&&alternateReplay.At(269)!=null,"replays from another EXE hash remain listed and importable");
    Check(GameLayout.CanPlay(round)&&!GameLayout.CanPlay(new Replay{GameHash=alternateHash,EventId=1,Completed=false}),"source hash flexibility preserves complete-race requirement");
    Check(!GameLayout.HashValid(new string('z',64)),"invalid provenance metadata is rejected without a hash whitelist");
    string moduleDump=Path.Combine(folder,"module.bin");if(File.Exists(moduleDump)){byte[] moduleBytes=File.ReadAllBytes(moduleDump);GameLayout.Validate(alternateHash,delegate(long rva,int byteCount){byte[] bytes=new byte[byteCount];Array.Copy(moduleBytes,rva,bytes,0,byteCount);return bytes;});Check(true,"saved actual game code passes runtime matching with alternate provenance hash");}
    float[] stableView=new float[]{1,0,0,0,0,1,0,0,0,0,1,1,0,0,0,1};
    Check(FrameConsistency.Stable(100,100,100,50,50,stableView,(float[])stableView.Clone()),"coherent clock and camera snapshot is accepted");
    Check(!FrameConsistency.Stable(100,101,101,50,50,stableView,stableView),"cross-physics-update pose timestamp is rejected");
    Check(!FrameConsistency.Stable(100,100,100,50,51,stableView,stableView),"independently advanced race clock is rejected");
    float[] movingView=(float[])stableView.Clone();movingView[12]=2;
    Check(!FrameConsistency.Stable(100,100,100,50,50,stableView,movingView),"camera updated during snapshot is rejected");
    float[] invalidView=(float[])stableView.Clone();invalidView[0]=float.NaN;
    Check(!FrameConsistency.Stable(100,100,100,50,50,invalidView,invalidView),"nonfinite snapshot matrix is rejected");
    var truth=new Replay{GameHash=GameReader.SupportedHash};
    truth.Samples.Add(new Sample{Time=0,Pose=PoseAt(0,new Vec()),Speed=100});truth.Samples.Add(new Sample{Time=.033333,Pose=PoseAt(0,new Vec(0,0,3.3333f)),Speed=100});truth.Samples.Add(new Sample{Time=.083333,Pose=PoseAt(0,new Vec(0,0,8.3333f)),Speed=100});
    double maxError=0;for(int j=0;j<=19;j++){double t=j/240.0;maxError=Math.Max(maxError,Math.Abs(truth.At(t).Pose.Position.Z-100*t));}
    Check(maxError<.001,"render-rate interpolation follows a coherent constant-speed trajectory without position jumps");
    // Exercise the same renderer offscreen, using a controlled synthetic camera.
    var drawReplay=new Replay{GameHash=GameReader.SupportedHash,Author="TEST"};drawReplay.Samples.Add(new Sample{Time=0,Pose=PoseAt(0,new Vec(0,0,15)),Speed=10});drawReplay.Samples.Add(new Sample{Time=.1,Pose=PoseAt(0,new Vec(0,0,16)),Speed=10});
    using(var overlay=new Overlay()){overlay.Size=new Size(1000,600);overlay.Frame=new Frame{Camera=camera};overlay.Replay=drawReplay;overlay.Time=.05;overlay.DrawGhost=true;using(var bitmap=new Bitmap(1000,600)){overlay.DrawToBitmap(bitmap,new Rectangle(0,0,1000,600));int cyan=0;for(int y=100;y<600;y++)for(int x=0;x<1000;x++){Color color=bitmap.GetPixel(x,y);if(color.G>200&&color.B>200&&color.R<100)cyan++;}Check(cyan>20,"renderer draws projected car geometry");bitmap.Save(Path.Combine(folder,"synthetic-render-test.png"));}}
    File.WriteAllLines(Path.Combine(folder,"selftest-results.txt"),results);return 0;
   }catch(Exception ex){results.Add(ex.ToString());File.WriteAllLines(Path.Combine(folder,"selftest-results.txt"),results);return 1;}finally{if(File.Exists(tmp))File.Delete(tmp);}
  }
 }
}
