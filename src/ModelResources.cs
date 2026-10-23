using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace HprGhost {
 // PC HPR bnd2 layout checked against DGIorio's MIT bundle/model tools.
 public sealed class AssetBundle {
  readonly byte[] file;readonly uint[] starts;readonly uint flags;
  readonly Dictionary<ulong,Asset> assets=new Dictionary<ulong,Asset>();
  public sealed class Asset {
   public uint Id,Type;public int Entry;public byte[] Data,Geometry;
   public Dictionary<int,uint> Imports;
  }
  public AssetBundle(string path){
   file=File.ReadAllBytes(path);if(file.Length<48||Encoding.ASCII.GetString(file,0,4)!="bnd2"||U(file,4)!=3||U(file,8)!=1)throw new InvalidDataException("不支持的车型资源包。");
   int count=checked((int)U(file,16)),table=checked((int)U(file,20));if(count>100000||table<48||(long)table+count*80>file.Length)throw new InvalidDataException("Invalid bundle table");
   starts=new uint[]{U(file,24),U(file,28),U(file,32),U(file,36)};flags=U(file,40);
   for(int i=0;i<count;i++){int e=table+i*80;uint id=U(file,e),type=U(file,e+68);ulong key=Key(id,type);if(type!=1&&type!=2&&type!=3&&type!=5&&type!=0x51&&type!=0x106&&type!=0x53)continue;
    if(assets.ContainsKey(key))throw new InvalidDataException("Duplicate graphics resource");assets[key]=new Asset{Id=id,Type=type,Entry=e};}
  }
  static ulong Key(uint id,uint type){return ((ulong)type<<32)|id;}
  public Asset Get(uint id,uint type){Asset a;if(!assets.TryGetValue(Key(id,type),out a))throw new InvalidDataException("缺少模型资源 "+id+" / "+type);if(a.Data==null){a.Data=Stream(a.Entry,0);a.Imports=new Dictionary<int,uint>();int count=BitConverter.ToUInt16(file,a.Entry+72),offset=checked((int)U(file,a.Entry+64));Range(a.Data,offset,checked(count*16));for(int j=0;j<count;j++){int at=offset+j*16;int patch=(int)(U(a.Data,at+8)&0x7fffffff);a.Imports[patch]=U(a.Data,at);}}return a;}
  public byte[] Geometry(Asset a){if(a.Geometry==null)a.Geometry=Stream(a.Entry,1);return a.Geometry;}
  byte[] Stream(int e,int stream){int size=checked((int)(U(file,e+16+stream*4)&0xfffffff)),disk=checked((int)U(file,e+32+stream*4));long begin=(long)starts[stream]+U(file,e+48+stream*4);if(size>64*1024*1024||begin<0||begin+(flags==8?size:disk)>file.Length)throw new InvalidDataException("Invalid bundle stream");if(size==0)return new byte[0];byte[] result=new byte[size];if(flags==8){Buffer.BlockCopy(file,(int)begin,result,0,size);return result;}
   if(disk<6)throw new InvalidDataException("Invalid zlib stream");using(var input=new MemoryStream(file,(int)begin+2,disk-6))using(var z=new DeflateStream(input,CompressionMode.Decompress)){int n=0;while(n<size){int got=z.Read(result,n,size-n);if(got==0)throw new InvalidDataException("Truncated asset");n+=got;}if(z.ReadByte()!=-1)throw new InvalidDataException("Oversized asset");}return result;
  }
  public static void Range(byte[] d,int offset,int size){if(offset<0||size<0||(long)offset+size>d.Length)throw new InvalidDataException("Asset offset out of range");}
  public static uint U(byte[] d,int at){Range(d,at,4);return BitConverter.ToUInt32(d,at);}
  public static int P(byte[] d,int at){Range(d,at,8);long n=BitConverter.ToInt64(d,at);if(n<0||n>int.MaxValue)throw new InvalidDataException("Invalid asset pointer");return (int)n;}
  public static float F(byte[] d,int at){Range(d,at,4);float f=BitConverter.ToSingle(d,at);if(!MathUtil.Finite(f))throw new InvalidDataException("Invalid model coordinate");return f;}
  public static uint Import(Asset a,int patch){uint id;if(!a.Imports.TryGetValue(patch,out id))throw new InvalidDataException("Missing model dependency");return id;}
 }
 public sealed class VehicleModel {
  public Model3DGroup Geometry=new Model3DGroup();public uint ResourceId;public int Vertices,Triangles,Wheels;public Rect3D Bounds;
 }
 public sealed class VehicleLibrary {
  string root;public int Revision {get;private set;}AssetBundle shaders;readonly Dictionary<uint,VehicleModel> models=new Dictionary<uint,VehicleModel>();readonly Dictionary<uint,string> names=new Dictionary<uint,string>();
  public VehicleLibrary(string gameRoot){root=gameRoot;foreach(string line in AppPaths.NameLines("vehicles.tsv")){int t=line.IndexOf('\t');uint id;if(t>0&&uint.TryParse(line.Substring(0,t),out id)){string value=line.Substring(t+1).Trim();if(value!=""&&value!="Vehicle "+id&&value!="车型 "+id)names[id]=value;}}}
  public void SetGameRoot(string folder){if(!GameLocation.Valid(folder))throw new InvalidDataException("游戏资源位置不可用，请点击“选择游戏”。");string full=Path.GetFullPath(folder);if(!string.Equals(full,root,StringComparison.OrdinalIgnoreCase)){root=full;shaders=null;models.Clear();Revision++;}}
  public string Name(uint id){string name;return id==0?"未记录车型":names.TryGetValue(id,out name)?name:"未知车型";}
  public VehicleModel Load(uint id){if(!GameLocation.Valid(root))SetGameRoot(GameLocation.Resolve());VehicleModel result;if(models.TryGetValue(id,out result))return result;if(id==0)throw new InvalidDataException("旧录制未保存车型，请使用包含车型信息的新录像。");if(shaders==null)shaders=new AssetBundle(Path.Combine(root,"SHADERS.BNDL"));
   var bundle=new AssetBundle(Path.Combine(root,"VEHICLES","VEH_"+id+"_MS.BIN"));var spec=bundle.Get(id,0x106);byte[] d=spec.Data;int table=AssetBundle.P(d,0),partCount=BitConverter.ToUInt16(d,32);if(partCount>128)throw new InvalidDataException("Too many body models");result=new VehicleModel{ResourceId=id};
   for(int j=0;j<partCount;j++)AddModel(bundle,AssetBundle.Import(spec,table+j*8),Matrix3D.Identity,result);
   int wheelCount=d[35],wheels=AssetBundle.P(d,24);if(wheelCount>8)throw new InvalidDataException("Too many wheel groups");
   for(int j=0;j<wheelCount;j++){int at=wheels+j*96;AssetBundle.Range(d,at,96);int wp=AssetBundle.P(d,at),count=BitConverter.ToUInt16(d,at+12);if(count>32)throw new InvalidDataException("Too many wheel models");var transform=Matrix3D.Identity;transform.Scale(new Vector3D(AssetBundle.F(d,at+80),AssetBundle.F(d,at+84),AssetBundle.F(d,at+88)));var q=new Quaternion(AssetBundle.F(d,at+64),AssetBundle.F(d,at+68),AssetBundle.F(d,at+72),AssetBundle.F(d,at+76));if(q.X*q.X+q.Y*q.Y+q.Z*q.Z+q.W*q.W>0.01)transform.Rotate(q);transform.Translate(new Vector3D(AssetBundle.F(d,at+48),AssetBundle.F(d,at+52),AssetBundle.F(d,at+56)));
    for(int k=0;k<count;k++)AddModel(bundle,AssetBundle.Import(spec,wp+k*8),transform,result);result.Wheels++;}
   if(result.Triangles<10)throw new InvalidDataException("车型没有可显示网格。");result.Geometry.Freeze();result.Bounds=result.Geometry.Bounds;if(result.Bounds.SizeX>12||result.Bounds.SizeY>8||result.Bounds.SizeZ>20)throw new InvalidDataException("Invalid vehicle model dimensions");models[id]=result;return result;
  }
  void AddModel(AssetBundle bundle,uint id,Matrix3D transform,VehicleModel result){var model=bundle.Get(id,0x51);byte[] d=model.Data;int table=AssetBundle.P(d,0),state=AssetBundle.P(d,8),count=d[32];AssetBundle.Range(d,state,1);int index=d[state];if(index==255)return;if(index>=count)throw new InvalidDataException("Invalid model state");var render=bundle.Get(AssetBundle.Import(model,table+index*8),5);byte[] h=render.Data,g=bundle.Geometry(render);int meshes=BitConverter.ToUInt16(h,18),meshTable=AssetBundle.P(h,24);if(meshes>128)throw new InvalidDataException("Too many meshes");
   for(int j=0;j<meshes;j++){int at=AssetBundle.P(h,meshTable+j*8);var mat=bundle.Get(AssetBundle.Import(render,at+32),2);var shader=shaders.Get(AssetBundle.Import(mat,8),0x53);var descriptor=shaders.Get(AssetBundle.Import(shader,0x130),3);byte[] desc=descriptor.Data;int attributes=desc[12],stride=0,posOffset=-1,posFormat=0;for(int k=0;k<attributes;k++){int a=16+k*20;AssetBundle.Range(desc,a,20);if(desc[a]==1&&desc[a+1]==0){posFormat=(int)AssetBundle.U(desc,a+4);posOffset=(int)AssetBundle.U(desc,a+8);stride=(int)AssetBundle.U(desc,a+16);}}if(posOffset<0||stride<8||stride>256)throw new InvalidDataException("Unsupported vertex layout");
    int ip=AssetBundle.P(h,at+48),vp=AssetBundle.P(h,at+56);int io=AssetBundle.P(h,ip+16),vo=AssetBundle.P(h,vp+16),indexSize=(int)AssetBundle.U(h,ip+28),indexCount=(int)AssetBundle.U(h,at+28),vertexBytes=(int)AssetBundle.U(h,vp+24);if(indexCount>1000000||(indexSize!=2&&indexSize!=4))throw new InvalidDataException("Invalid mesh buffers: "+indexCount+" / "+vertexBytes+" / "+stride+" / "+indexSize);int vertexCount=vertexBytes/stride;AssetBundle.Range(g,io,checked(indexCount*indexSize));AssetBundle.Range(g,vo,vertexBytes);
    var mesh=new MeshGeometry3D();for(int k=0;k<vertexCount;k++){int v=vo+k*stride+posOffset;Point3D p;
     if(posFormat==13){AssetBundle.Range(g,v,8);p=new Point3D(BitConverter.ToInt16(g,v)*(8.0/32767),BitConverter.ToInt16(g,v+2)*(8.0/32767),BitConverter.ToInt16(g,v+4)*(8.0/32767));}
     else if(posFormat==6||posFormat==2)p=new Point3D(AssetBundle.F(g,v),AssetBundle.F(g,v+4),AssetBundle.F(g,v+8));else throw new InvalidDataException("Unsupported position format "+posFormat);
     if(Math.Abs(p.X)>20||Math.Abs(p.Y)>20||Math.Abs(p.Z)>20)throw new InvalidDataException("Invalid vehicle vertex");mesh.Positions.Add(p);}
    int topology=(int)AssetBundle.U(h,at+16),run=0,a0=0,b0=0;uint restart=indexSize==2?65535:uint.MaxValue;for(int k=0;k<indexCount;k++){uint raw=indexSize==2?BitConverter.ToUInt16(g,io+k*2):AssetBundle.U(g,io+k*4);if(raw==restart){run=0;continue;}if(raw>=vertexCount)throw new InvalidDataException("Index outside mesh");int c=(int)raw;if(topology==5){if(run>=2){if((run&1)==0)Triangle(mesh,a0,b0,c);else Triangle(mesh,b0,a0,c);}a0=b0;b0=c;run++;}else if(topology==4){if(run%3==0)a0=c;else if(run%3==1)b0=c;else Triangle(mesh,a0,b0,c);run++;}else throw new InvalidDataException("Unsupported mesh topology");}
    mesh.Freeze();string kind=ShaderName(shader.Data).ToLowerInvariant();Color color=kind.Contains("tyre")?Color.FromRgb(35,39,42):kind.Contains("glass")?Color.FromRgb(65,98,112):kind.Contains("interior")||kind.Contains("chassis")||kind.Contains("carbon")?Color.FromRgb(52,59,64):kind.Contains("rim")||kind.Contains("chrome")||kind.Contains("wheel")?Color.FromRgb(164,172,176):Color.FromRgb(164,202,211);
    var material=new MaterialGroup();material.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));material.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromRgb(210,225,230)),35));material.Freeze();var geometry=new GeometryModel3D(mesh,material){BackMaterial=material,Transform=new MatrixTransform3D(transform)};geometry.Freeze();result.Geometry.Children.Add(geometry);result.Vertices+=vertexCount;result.Triangles+=mesh.TriangleIndices.Count/3;
   }
  }
  static string ShaderName(byte[] d){int at=AssetBundle.P(d,16),end=at;while(end<d.Length&&d[end]!=0)end++;return Encoding.ASCII.GetString(d,at,end-at);}
  static void Triangle(MeshGeometry3D mesh,int a,int b,int c){if(a==b||b==c||a==c)return;mesh.TriangleIndices.Add(a);mesh.TriangleIndices.Add(b);mesh.TriangleIndices.Add(c);}
 }
}