using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Media3D;
namespace HprGhost {
 public interface IPlaybackRenderer:IDisposable {
  bool Ready{get;}bool IsDrawing{get;}string Status{get;}
  void SetGameRoot(string root);void Load(Replay replay);void Update(bool enabled,int opacity,int gamePid);void Stop();
 }
 public sealed class NativeMeshData {public float[] Vertices;public int[] Indices;}
 public static class NativePayload {
  sealed class Vertex {public Point3D P;public Vector3D N;public Color C;}
  static Color Colour(Material m){var d=m as DiffuseMaterial;if(d!=null&&d.Brush is SolidColorBrush)return ((SolidColorBrush)d.Brush).Color;var group=m as MaterialGroup;if(group!=null)foreach(var child in group.Children){Color c=Colour(child);if(c.A!=0)return c;}return Colors.Transparent;}
  static void Add(Model3D node,Matrix3D parent,List<Vertex> vertices,List<int> indices){var transform=node.Transform.Value;transform.Append(parent);var group=node as Model3DGroup;if(group!=null){foreach(var child in group.Children)Add(child,transform,vertices,indices);return;}var geometry=node as GeometryModel3D;if(geometry==null)return;var mesh=geometry.Geometry as MeshGeometry3D;if(mesh==null)return;int start=vertices.Count;Color colour=Colour(geometry.Material);if(colour.A==0)colour=Colors.LightCyan;foreach(var p in mesh.Positions)vertices.Add(new Vertex{P=transform.Transform(p),C=colour});for(int j=0;j<mesh.TriangleIndices.Count;j+=3){int a=start+mesh.TriangleIndices[j],b=start+mesh.TriangleIndices[j+1],c=start+mesh.TriangleIndices[j+2];indices.Add(a);indices.Add(b);indices.Add(c);Vector3D normal=Vector3D.CrossProduct(vertices[b].P-vertices[a].P,vertices[c].P-vertices[a].P);vertices[a].N+=normal;vertices[b].N+=normal;vertices[c].N+=normal;}}
  public static NativeMeshData Mesh(VehicleModel model){var vertices=new List<Vertex>();var indices=new List<int>();Add(model.Geometry,Matrix3D.Identity,vertices,indices);var data=new float[checked(vertices.Count*10)];for(int j=0;j<vertices.Count;j++){var v=vertices[j];Vector3D n=v.N;if(n.LengthSquared>1e-12)n.Normalize();else n=new Vector3D(0,1,0);int at=j*10;data[at]=(float)v.P.X;data[at+1]=(float)v.P.Y;data[at+2]=(float)v.P.Z;data[at+3]=(float)n.X;data[at+4]=(float)n.Y;data[at+5]=(float)n.Z;data[at+6]=v.C.R/255f;data[at+7]=v.C.G/255f;data[at+8]=v.C.B/255f;data[at+9]=1;}return new NativeMeshData{Vertices=data,Indices=indices.ToArray()};}
  public static byte[] Create(Replay replay,NativeMeshData mesh){replay.Validate();if(!GameLayout.CanPlay(replay)||replay.VehicleResourceId==0)throw new InvalidDataException("这份录像缺少完整赛事或车型信息，无法回放。");long length=32L+mesh.Vertices.LongLength*4+mesh.Indices.LongLength*4+replay.Samples.Count*60L;if(length>128*1024*1024)throw new InvalidDataException("录像与车型数据过大。");using(var stream=new MemoryStream((int)length))using(var w=new BinaryWriter(stream)){w.Write(Encoding.ASCII.GetBytes("HPRNAT03"));w.Write((uint)length);w.Write(replay.VehicleResourceId);w.Write(replay.EventId);w.Write(mesh.Vertices.Length/10);w.Write(mesh.Indices.Length);w.Write(replay.Samples.Count);byte[] data=new byte[Math.Max(mesh.Vertices.Length,mesh.Indices.Length)*4];Buffer.BlockCopy(mesh.Vertices,0,data,0,mesh.Vertices.Length*4);w.Write(data,0,mesh.Vertices.Length*4);Buffer.BlockCopy(mesh.Indices,0,data,0,mesh.Indices.Length*4);w.Write(data,0,mesh.Indices.Length*4);foreach(var s in replay.Samples){w.Write(s.Time);Write(w,s.Pose.Right);Write(w,s.Pose.Up);Write(w,s.Pose.Forward);Write(w,s.Pose.Position);w.Write(s.Speed);}return stream.ToArray();}}
  static void Write(BinaryWriter w,Vec v){w.Write(v.X);w.Write(v.Y);w.Write(v.Z);}
 }
 public static class NativeComponent {
  public const string FileName="HPRGhostNative.asi";
  static byte[] bytes;static string digest;public static byte[] Bytes{get{if(bytes==null)using(var input=typeof(NativeComponent).Assembly.GetManifestResourceStream("HprGhost.Native.asi")){if(input==null)throw new InvalidOperationException("缺少回放组件，请重新解压软件。");using(var output=new MemoryStream()){input.CopyTo(output);bytes=output.ToArray();}}return bytes;}}
  static string Hash(byte[] data){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","");}
  public static string Digest{get{return digest??(digest=Hash(Bytes));}}
  public static bool GameRunning(){var processes=Process.GetProcessesByName("NFS11Remastered");bool running=processes.Length!=0;foreach(var process in processes)process.Dispose();return running;}
  public static bool HasLoader(string root){return AsiLoader.HasLoader(root);}
  public static bool Install(string root){return Install(root,GameRunning());}
  internal static bool Install(string root,bool gameRunning){string target=Path.Combine(root,FileName);bool exists=File.Exists(target);if(exists){byte[] old=File.ReadAllBytes(target);string existing=Hash(old);if(existing==Digest)return true;if(gameRunning)return false;bool owned=Encoding.ASCII.GetString(old).Contains("HPRGHOST_NATIVE_COMPONENT_V3");if(!owned&&existing!="1362A91D0419E4BA1E05C8B9EDE270AAD4417FDAD40A94D8F6DBF5D5934DB866"&&existing!="F437E89422713C77E1C6436FB5C56A0DDEF92042D448055E9EB7DE4992BD3FF6")throw new IOException("同名回放组件与此版本不匹配，请先备份并移走旧组件。");}
   string temp=Path.Combine(root,".hprghost-native-"+Guid.NewGuid().ToString("N")+".tmp");try{File.WriteAllBytes(temp,Bytes);if(exists)File.Replace(temp,target,null);else File.Move(temp,target);}finally{if(File.Exists(temp))File.Delete(temp);}return true;}
 }
 public sealed class NativePlayback:IPlaybackRenderer {
  public const string ChannelName="Local\\HPRGhostReplayNative_v3";
  readonly Func<bool> isGameRunning;readonly string disabledFile;bool suspended,needsRestart;public int CompletedInstalls{get;private set;}public bool RestartRequired{get{return needsRestart;}}
  readonly VehicleLibrary models;readonly string channel;readonly bool install;readonly int owner=Process.GetCurrentProcess().Id,token=Guid.NewGuid().GetHashCode()|1;
  MemoryMappedFile map,payload;MemoryMappedViewAccessor view;int revision,gamePid,lastError,meshRevision=-1;uint meshId;NativeMeshData mesh;string root="",installStatus="";bool pending;DateTime retry=DateTime.MinValue;
  public NativePlayback(VehicleLibrary library):this(library,ChannelName,true){}
  internal NativePlayback(VehicleLibrary library,string name,bool allowInstall):this(library,name,allowInstall,NativeComponent.GameRunning,Path.Combine(AppPaths.BaseFolder,"PlaybackDisabled.txt")){}
  internal NativePlayback(VehicleLibrary library,string name,bool allowInstall,Func<bool> running,string preferenceFile){models=library;channel=name;install=allowInstall;isGameRunning=running;disabledFile=preferenceFile;suspended=File.Exists(disabledFile);}
  bool Valid{get{return view!=null&&view.ReadInt32(0)==0x334e4748&&view.ReadInt32(4)==512&&view.ReadInt32(8)==3;}}
  bool Connected{get{return Valid&&view.ReadInt32(40)==1&&(gamePid==0||view.ReadInt32(32)==gamePid)&&unchecked((uint)Environment.TickCount-(uint)view.ReadInt32(36))<3000;}}
  public bool Ready{get{return revision>0&&Connected&&view.ReadInt32(44)==0&&view.ReadInt32(52)==revision&&view.ReadInt32(340)==token;}}
  public bool IsDrawing{get{return Ready&&unchecked((uint)Environment.TickCount-(uint)view.ReadInt32(72))<350;}}
  public string Status{get{if(Valid&&gamePid!=0&&view.ReadInt32(32)==gamePid&&view.ReadInt32(44)!=0){int error=view.ReadInt32(44);return error==1?"当前游戏布局不兼容，回放暂不可用。":error==8?"录像或车型载入失败，请重新载入录像。":"回放组件初始化失败，请重新载入录像或重启游戏。";}if(Ready)return "";if(pending)return "请退出游戏，等待组件更新后重新启动游戏。";if(installStatus!=""&&!Connected)return installStatus;return Connected?"正在准备回放…":"重启游戏后启用原生回放。";}}
  void Open(){if(view!=null)return;map=MemoryMappedFile.CreateOrOpen(channel,512);view=map.CreateViewAccessor();if(view.ReadInt32(0)==0){view.Write(4,512);view.Write(8,3);Thread.MemoryBarrier();view.Write(0,0x334e4748);}if(!Valid)throw new IOException("回放组件通信版本不匹配，请重启游戏。");}
  public string SetupStatus{get{if(suspended)return "回放组件已卸载，载入录像可重新启用。";if(pending)return "请退出游戏，等待组件更新后重新启动游戏。";if(installStatus!="")return installStatus;if(needsRestart)return "回放组件已安装，请重启游戏后使用。";return root!=""?"回放组件已就绪。":"";}}
  public void SetGameRoot(string folder){if(!install)return;if(!GameLocation.Valid(folder))throw new InvalidDataException("游戏资源位置不可用，请点击“选择游戏”。");bool changed=!string.Equals(root,folder,StringComparison.OrdinalIgnoreCase);root=folder;if(!suspended&&changed)TryInstall();}
  void TryInstall(){retry=DateTime.UtcNow.AddSeconds(1);if(suspended)return;bool running=isGameRunning();if(pending&&running)return;try{bool changed=!AsiLoader.HasLoader(root);string plugin=Path.Combine(root,NativeComponent.FileName);changed=changed||!File.Exists(plugin)||AsiLoader.Hash(File.ReadAllBytes(plugin))!=NativeComponent.Digest;AsiLoader.Install(root,running);pending=!NativeComponent.Install(root,running);needsRestart=running&&(needsRestart||changed);installStatus="";if(changed&&!pending)CompletedInstalls++;}catch(IOException ex){installStatus=ex.Message;pending=false;}catch(UnauthorizedAccessException){installStatus="无法安装回放组件，请检查游戏目录的写入权限。";pending=false;}}
  public void Load(Replay replay){Stop();var model=models.Load(replay.VehicleResourceId);if(mesh==null||meshId!=replay.VehicleResourceId||meshRevision!=models.Revision){mesh=NativePayload.Mesh(model);meshId=replay.VehicleResourceId;meshRevision=models.Revision;}byte[] bytes=NativePayload.Create(replay,mesh);if(install&&root!="")ResumeInstallation();Publish(bytes);}
  internal void Publish(byte[] bytes){Stop();string name="Local\\HPRGhostReplayAsset_"+owner+"_"+Guid.NewGuid().ToString("N");MemoryMappedFile next=MemoryMappedFile.CreateNew(name,bytes.Length);try{using(var output=next.CreateViewAccessor())output.WriteArray(0,bytes,0,bytes.Length);Open();int seq=view.ReadInt32(12);seq=(seq&~1)+1;view.Write(12,seq);view.Write(16,0);view.Write(24,owner);view.Write(28,Environment.TickCount);view.Write(336,token);view.Write(48,0);view.Write(76,name.Length);byte[] encoded=Encoding.Unicode.GetBytes(name);view.WriteArray(80,encoded,0,encoded.Length);revision=revision==int.MaxValue?1:revision+1;view.Write(48,revision);Thread.MemoryBarrier();view.Write(12,seq+1);var previous=payload;payload=next;next=null;if(previous!=null)previous.Dispose();}finally{if(next!=null)next.Dispose();}}
  public void Update(bool enabled,int opacity,int processId){gamePid=processId;if(needsRestart&&processId==0&&!isGameRunning())needsRestart=false;if(pending&&root!=""&&DateTime.UtcNow>=retry)TryInstall();if(view==null)return;if(!Valid||view.ReadInt32(336)!=token)return;view.Write(28,Environment.TickCount);view.Write(20,Math.Max(10,Math.Min(85,opacity)));view.Write(16,enabled&&revision>0?1:0);int error=view.ReadInt32(44);if(gamePid!=0&&view.ReadInt32(32)==gamePid&&error!=0&&error!=lastError)AppLog.Failure(new InvalidOperationException());lastError=error;}
  internal void ResumeInstallation(){if(suspended){File.Delete(disabledFile);suspended=false;}TryInstall();}
  public void Unload(){Stop();pending=false;revision=0;installStatus="";needsRestart=false;suspended=true;if(install)File.WriteAllText(disabledFile,"Playback auto-install disabled by uninstall. Load a replay to enable.\r\n");}
  public void Stop(){if(Valid&&view.ReadInt32(336)==token)view.Write(16,0);}
  public void Dispose(){Stop();if(Valid&&view.ReadInt32(336)==token){view.Write(28,0);view.Write(24,0);}if(payload!=null){payload.Dispose();payload=null;}if(view!=null){view.Dispose();view=null;}if(map!=null){map.Dispose();map=null;}}
 }
}




