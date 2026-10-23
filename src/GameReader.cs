using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace HprGhost {
 public static class Native {
  [DllImport("kernel32.dll",SetLastError=true)] public static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
  [DllImport("kernel32.dll",SetLastError=true)] public static extern bool ReadProcessMemory(IntPtr process,IntPtr address,byte[] buffer,UIntPtr size,out UIntPtr count);
  [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr handle);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr window,out Rect rect);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr window,ref Point point);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr window);
  [DllImport("user32.dll",SetLastError=true)] public static extern bool RegisterHotKey(IntPtr window,int id,uint modifiers,uint key);
  [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr window,int id);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int w,int h,uint flags);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [StructLayout(LayoutKind.Sequential)] public struct Rect {public int Left,Top,Right,Bottom;}
  [StructLayout(LayoutKind.Sequential)] public struct Point {public int X,Y;}
 }
 public class RaceState { public uint EventId;public long RaceTicks,LiveTicks;public bool Active,Running,Finished;public long ClockAddress; }
 public class Frame {
  public long Tick,VehicleObject,VehicleData,CameraAddress;public uint VehicleTag,VehicleResourceId;
#if DEVELOPMENT
  public int CompetitorIndex,VehicleIndex;
#endif
  public Pose Pose;public Camera Camera;public float Speed;public Vec Velocity;
 }
 public sealed class GameReader : IDisposable {
  public const string SupportedHash="fab844539ad04f18b8c920615e27812fb22612d42c537417964a502d9862a017";
  public readonly Process Process;public readonly long Base;public readonly string Hash;
  IntPtr handle,cachedWindow;readonly string vehicleFolder;uint checkedVehicleId;bool checkedVehicleExists;
  static readonly Dictionary<string,string> hashCache=new Dictionary<string,string>();
  public GameReader(){
   Process[] matches=System.Diagnostics.Process.GetProcessesByName("NFS11Remastered");
   if(matches.Length==0)throw new GameAbsentException();if(matches.Length>1)throw new InvalidOperationException("检测到多个游戏进程，请只运行一个。");
   Process=matches[0];Base=Process.MainModule.BaseAddress.ToInt64();
   string exe=Process.MainModule.FileName;
   vehicleFolder=Path.Combine(Path.GetDirectoryName(exe),"VEHICLES");
   try{
    var info=new FileInfo(exe);string key=exe+"|"+info.Length+"|"+info.LastWriteTimeUtc.Ticks;
    string hash;if(!hashCache.TryGetValue(key,out hash)){using(var sha=SHA256.Create())using(var stream=File.OpenRead(exe))hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();hashCache[key]=hash;}Hash=hash;
    // PROCESS_QUERY_INFORMATION | PROCESS_VM_READ. Never requests write access.
    handle=Native.OpenProcess(0x410,false,Process.Id);
    if(handle==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
    GameLayout.Validate(Hash,delegate(long rva,int count){return Read(Base+rva,count);});
    AppLog.Write(LogCode.GAME_CONNECTED);
   }catch(Exception ex){AppLog.Failure(ex);Dispose();throw;}
  }
  byte[] Read(long address,int size){
   if(address<65536||address>0x00007fffffffffff-size)throw new InvalidDataException("Invalid game pointer");
   byte[] b=new byte[size];UIntPtr n;
   if(!Native.ReadProcessMemory(handle,new IntPtr(address),b,(UIntPtr)size,out n)||n.ToUInt64()!=(ulong)size)throw new IOException("游戏数据暂不可读: "+address.ToString("X"));
   return b;
  }
  long Pointer(long address){long p=BitConverter.ToInt64(Read(address,8),0);if(p<65536||p>0x00007fffffffffff)throw new InvalidDataException("Game object unavailable");return p;}
  int I32(long address){return BitConverter.ToInt32(Read(address,4),0);}
  static Vec Vector(byte[] b,int o){return new Vec(BitConverter.ToSingle(b,o),BitConverter.ToSingle(b,o+4),BitConverter.ToSingle(b,o+8));}
  
#if DEVELOPMENT
  public long SnapshotAccepted,SnapshotRetries,SnapshotDeferred;
#endif
  public string SnapshotProblem="";
  public long ReadSimulationTick(){long manager=Pointer(Base+0x129c498);return BitConverter.ToInt64(Read(Pointer(manager+8)+0x20,8),0);}
  public bool TryReadSnapshot(out RaceState race,out Frame frame){
   race=null;frame=null;
   for(int attempt=0;attempt<3;attempt++){
    try{
     RaceState state=ReadRace();
     if(!state.Active||(!state.Running&&!state.Finished&&state.ClockAddress==0)){race=state;SnapshotProblem="";return true;}
     long before=ReadSimulationTick();Frame next=ReadFrame();long after=ReadSimulationTick();
     long raceAfter=BitConverter.ToInt64(Read(state.ClockAddress,8),0);byte[] vp=Read(next.CameraAddress,64);float[] matrix=new float[16];for(int j=0;j<16;j++)matrix[j]=BitConverter.ToSingle(vp,j*4);
     if(!FrameConsistency.Stable(before,next.Tick,after,state.LiveTicks,raceAfter,next.Camera.ViewProjection,matrix)){
#if DEVELOPMENT
SnapshotRetries++;
#endif
SnapshotProblem="正在等待同一游戏更新中的时钟、车辆和相机";continue;}
     race=state;frame=next;
#if DEVELOPMENT
SnapshotAccepted++;
#endif
SnapshotProblem="";return true;
    }catch(IOException ex){
#if DEVELOPMENT
SnapshotRetries++;
#endif
SnapshotProblem=ex.Message;}catch(InvalidDataException ex){
#if DEVELOPMENT
SnapshotRetries++;
#endif
SnapshotProblem=ex.Message;}catch(InvalidOperationException ex){
#if DEVELOPMENT
SnapshotRetries++;
#endif
SnapshotProblem=ex.Message;}
   }
   
#if DEVELOPMENT
SnapshotDeferred++;
#endif
return false;
  }
  public uint SelectedVehicleId(){
   // Game helper 0x228f20 returns config+0x1d34 for the local competitor.
   long race=Pointer(Base+0x1279218);int competitor=I32(race+0x6c04);if(competitor<0||competitor>=8)return 0;
   uint id=BitConverter.ToUInt32(Read(race+0x31b20+0x1d34+competitor*4,4),0);
   if(id==0)return 0;
   // The resource ID remains a live memory read. Only its local-file check is
   // cached, and a changed ID is validated before it can reach a recording.
   if(id!=checkedVehicleId){checkedVehicleExists=File.Exists(Path.Combine(vehicleFolder,"VEH_"+id+"_MS.BIN"));checkedVehicleId=id;}
   return checkedVehicleExists?id:0;
  }
  public RaceState ReadRace(){
   if(Process.HasExited)throw new IOException("游戏已退出。");
   long race=Pointer(Base+0x1279218),manager=Pointer(Base+0x1279220);
   long package=BitConverter.ToInt64(Read(manager+0x4990,8),0);
   long empty=BitConverter.ToInt64(Read(Base+0x12795c8,8),0);
   if(package<65536||package==empty)return new RaceState();
   int resourceOffset=I32(manager+0x4998);if(resourceOffset<0||resourceOffset>0x10000000)throw new InvalidDataException("Invalid event resource offset");
   long resource=Pointer(package+0x28);uint eventId=BitConverter.ToUInt32(Read(resource+resourceOffset+0x1c,4),0);
   if(eventId==0)throw new InvalidDataException("Missing event ID");
   int competitor=I32(race+0x6c04);if(competitor<0||competitor>=8)return new RaceState{EventId=eventId,Active=true};
   byte[] r=Read(race+0x1180+competitor*0xb50,0x70);
   long definition=BitConverter.ToInt64(r,0x10);
   if(definition<65536)return new RaceState{EventId=eventId,Active=true};
   int part=BitConverter.ToInt32(r,0x18),parts=BitConverter.ToUInt16(Read(definition+0x28,2),0);
   int finishPart=BitConverter.ToUInt16(r,0x60),timerPart=BitConverter.ToUInt16(r,0x62),primitive=BitConverter.ToInt32(r,0x64);
   if(parts>4096||finishPart>4096||timerPart>4096)throw new InvalidDataException("Invalid challenge part metadata");
   long finalValue=0;bool finished=false;if(finishPart>0){long ends=BitConverter.ToInt64(r,0x38);long value=BitConverter.ToInt64(Read(ends+finishPart*8L,8),0);finished=value!=long.MinValue;if(finished)finalValue=value;}
   bool startFlag=false;if(primitive>=0&&primitive<65536){long flags=BitConverter.ToInt64(r,0x28);startFlag=Read(flags+primitive,1)[0]!=0;}
   bool running=part>=0&&part<parts&&!finished&&(timerPart<=part||startFlag);
   // Same table used by the game's challenge time virtual getter at RVA 0x2A7EF0.
   long state=Pointer(race+0x239c8),vtable=Pointer(state+0x18),getter=Pointer(vtable+0x28);
   if(getter!=Base+0x2a7ef0)throw new InvalidDataException("Unverified race timer getter");
   byte[] table=Read(state+0x18+0x2d8,0x150);int count=BitConverter.ToInt32(table,0x140);
   if(count<0||count>8)throw new InvalidDataException("Invalid race clock table");
   long elapsed=0,clockAddress=0;bool found=false;
   for(int i=0;i<count;i++)if(BitConverter.ToInt32(table,i*0x28)==competitor){elapsed=BitConverter.ToInt64(table,i*0x28+8);clockAddress=state+0x18+0x2d8+i*0x28+8;found=true;break;}
   if((running||finished)&&!found)throw new IOException("等待赛事计时器就绪。");
   if(elapsed<0||elapsed>21600000000L)throw new InvalidDataException("Invalid race time");
   if(BitConverter.ToInt64(Read(manager+0x4990,8),0)!=package)throw new IOException("赛事正在切换。");
   return new RaceState{EventId=eventId,Active=true,Running=running,Finished=finished,RaceTicks=finished?finalValue:elapsed,LiveTicks=elapsed,ClockAddress=clockAddress};
  }
  public Frame ReadFrame(){
   if(Process.HasExited)throw new IOException("游戏已退出。");
   long race=Pointer(Base+0x1279218);
   int competitor=I32(race+0x6c04);
   if(competitor<0||competitor>=8)throw new InvalidOperationException("等待进入单人赛事。");
   byte[] record=Read(race+0x1180+competitor*0xb50,0xa00);
   if(record[0x991]==0)throw new InvalidOperationException("玩家赛事车辆尚未激活。");
   int index=BitConverter.ToInt32(record,0x924);
   if(index<0||index>=8)throw new InvalidOperationException("玩家车辆索引不可用。");
   long vm=Pointer(Base+0x129c4c8),vd=Pointer(vm+8);int offset=I32(vd+0x3108);
   if(offset<0||offset>65536)throw new InvalidDataException("Invalid vehicle pool");
   long vehicle=Pointer(Base+0x1288a80+8L*(offset+index));
   long vtable=Pointer(vehicle),getter=Pointer(vtable+0x20);
   byte[] fn=Read(getter,8);
   if(fn[0]!=0x48||fn[1]!=0x8d||fn[2]!=0x81||BitConverter.ToInt32(fn,3)!=0xa0||fn[7]!=0xc3)throw new InvalidDataException("Unverified vehicle transform getter");
   byte[] objectData=Read(vehicle+0x90,0x60);
   Pose pose=new Pose{Right=Vector(objectData,0x10),Up=Vector(objectData,0x20),Forward=Vector(objectData,0x30),Position=Vector(objectData,0x40)};
   // GetSignedVehicleSpeed uses the physics object's velocity at +0xE0.
   Vec velocity=Vector(objectData,0x50);float speed=velocity.Length;
   if(!pose.Valid||!velocity.Finite||speed>500)throw new InvalidDataException("Invalid vehicle pose");
   long tm=Pointer(Base+0x129c498),td=Pointer(tm+8);long tick=BitConverter.ToInt64(Read(td+0x20,8),0);
   long cm=Pointer(Base+0x129c4e8),cd=Pointer(cm+8);byte[] cam=Read(cd+0x90,0xe4);float[] viewProjection=new float[16];for(int n=0;n<16;n++)viewProjection[n]=BitConverter.ToSingle(cam,n*4);
   Camera camera=new Camera{Right=Vector(cam,0x80),Up=Vector(cam,0x90),Forward=Vector(cam,0xa0),Position=Vector(cam,0xb0),HorizontalScale=BitConverter.ToSingle(cam,0xc4),VerticalScale=BitConverter.ToSingle(cam,0xd0),ViewProjection=viewProjection};
   if(!camera.Valid||tick<0)throw new InvalidDataException("Invalid camera or simulation clock");
   // Detect world/pool transition during the read; never keep a stale vehicle pointer.
   if(Pointer(Base+0x1279218)!=race||Pointer(vm+8)!=vd||I32(race+0x6c04)!=competitor||I32(race+0x1aa4+competitor*0xb50)!=index)throw new IOException("赛事正在切换。");
   var frame=new Frame{CameraAddress=cd+0x90,Tick=tick,VehicleObject=vehicle,VehicleData=vd,VehicleResourceId=SelectedVehicleId(),VehicleTag=BitConverter.ToUInt32(objectData,0),Pose=pose,Camera=camera,Speed=speed,Velocity=velocity};
#if DEVELOPMENT
   frame.CompetitorIndex=competitor;frame.VehicleIndex=index;
#endif
   return frame;
  }
  public bool Foreground {get{uint pid;Native.GetWindowThreadProcessId(Native.GetForegroundWindow(),out pid);return pid==Process.Id;}}
  public IntPtr Window {get{
   uint owner;
   // A native ownership check detects destroyed/replaced handles without
   // refreshing and enumerating the process's windows on every render.
   if(cachedWindow!=IntPtr.Zero&&Native.GetWindowThreadProcessId(cachedWindow,out owner)!=0&&owner==Process.Id)return cachedWindow;
   Process.Refresh();cachedWindow=Process.MainWindowHandle;return cachedWindow;
  }}
  public void Dispose(){if(handle!=IntPtr.Zero){Native.CloseHandle(handle);handle=IntPtr.Zero;}Process.Dispose();}
 }
}
