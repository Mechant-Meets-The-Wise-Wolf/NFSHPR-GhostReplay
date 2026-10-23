using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace HprGhost {
 public struct Vec {
  public float X,Y,Z;
  public Vec(float x,float y,float z){X=x;Y=y;Z=z;}
  public static Vec operator +(Vec a,Vec b){return new Vec(a.X+b.X,a.Y+b.Y,a.Z+b.Z);}
  public static Vec operator -(Vec a,Vec b){return new Vec(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}
  public static Vec operator *(Vec a,float s){return new Vec(a.X*s,a.Y*s,a.Z*s);}
  public float Length { get {return (float)Math.Sqrt(Dot(this,this));} }
  public Vec Unit { get {float n=Length; if(n<1e-6f) throw new InvalidDataException("Zero axis");return this*(1/n);} }
  public static float Dot(Vec a,Vec b){return a.X*b.X+a.Y*b.Y+a.Z*b.Z;}
  public static Vec Cross(Vec a,Vec b){return new Vec(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);}
  public bool Finite { get {return MathUtil.Finite(X)&&MathUtil.Finite(Y)&&MathUtil.Finite(Z);} }
 }
 public static class MathUtil {
  public static bool Finite(double x){return !double.IsNaN(x)&&!double.IsInfinity(x);}
 }
 public struct Quat {
  public double X,Y,Z,W;
  public Quat(double x,double y,double z,double w){X=x;Y=y;Z=z;W=w;}
  public Quat Unit {get {double d=Math.Sqrt(X*X+Y*Y+Z*Z+W*W);return new Quat(X/d,Y/d,Z/d,W/d);}}
  // Pose stores the three world-space basis columns of a proper rotation.
  public static Quat FromPose(Pose p){
   double a=p.Right.X,b=p.Up.X,c=p.Forward.X,d=p.Right.Y,e=p.Up.Y,f=p.Forward.Y,g=p.Right.Z,h=p.Up.Z,i=p.Forward.Z;
   double t=a+e+i,s; Quat q;
   if(t>0){s=Math.Sqrt(t+1)*2;q=new Quat((h-f)/s,(c-g)/s,(d-b)/s,s/4);}
   else if(a>e&&a>i){s=Math.Sqrt(1+a-e-i)*2;q=new Quat(s/4,(b+d)/s,(c+g)/s,(h-f)/s);}
   else if(e>i){s=Math.Sqrt(1+e-a-i)*2;q=new Quat((b+d)/s,s/4,(f+h)/s,(c-g)/s);}
   else{s=Math.Sqrt(1+i-a-e)*2;q=new Quat((c+g)/s,(f+h)/s,s/4,(d-b)/s);}
   return q.Unit;
  }
  public static Quat Slerp(Quat a,Quat b,double t){
   double d=a.X*b.X+a.Y*b.Y+a.Z*b.Z+a.W*b.W;
   if(d<0){b=new Quat(-b.X,-b.Y,-b.Z,-b.W);d=-d;}
   d=Math.Min(1,d);double u,v;
   if(d>.9995){u=1-t;v=t;}
   else{double theta=Math.Acos(d);u=Math.Sin((1-t)*theta)/Math.Sin(theta);v=Math.Sin(t*theta)/Math.Sin(theta);}
   return new Quat(a.X*u+b.X*v,a.Y*u+b.Y*v,a.Z*u+b.Z*v,a.W*u+b.W*v).Unit;
  }
  public Pose Pose(Vec pos){
   Quat q=Unit;double x=q.X,y=q.Y,z=q.Z,w=q.W;
   return new Pose {
    Right=new Vec((float)(1-2*(y*y+z*z)),(float)(2*(x*y+z*w)),(float)(2*(x*z-y*w))),
    Up=new Vec((float)(2*(x*y-z*w)),(float)(1-2*(x*x+z*z)),(float)(2*(y*z+x*w))),
    Forward=new Vec((float)(2*(x*z+y*w)),(float)(2*(y*z-x*w)),(float)(1-2*(x*x+y*y))),Position=pos};
  }
 }
 public class Pose {
  public Vec Right,Up,Forward,Position;
  public Vec Local(float x,float y,float z){return Position+Right*x+Up*y+Forward*z;}
  public bool Valid {
   get { return Position.Finite&&Position.Length<1000000&&Right.Finite&&Up.Finite&&Forward.Finite&&
    Math.Abs(Right.Length-1)<.1&&Math.Abs(Up.Length-1)<.1&&Math.Abs(Forward.Length-1)<.1&&
    Math.Abs(Vec.Dot(Right,Up))<.1&&Math.Abs(Vec.Dot(Right,Forward))<.1&&Math.Abs(Vec.Dot(Up,Forward))<.1&&Vec.Dot(Vec.Cross(Right,Up),Forward)>.8; }
  }
  public static Pose Blend(Pose a,Pose b,float t){return Quat.Slerp(Quat.FromPose(a),Quat.FromPose(b),t).Pose(a.Position*(1-t)+b.Position*t);}
 }
 public class Sample {public double Time;public Pose Pose;public float Speed;}
 public class Camera {
  public Vec Right,Up,Forward,Position;
  public float HorizontalScale,VerticalScale;public float[] ViewProjection;
  public bool Valid {get {return new Pose {Right=Right,Up=Up,Forward=Forward,Position=Position}.Valid&&
    MathUtil.Finite(HorizontalScale)&&MathUtil.Finite(VerticalScale)&&HorizontalScale>.05&&HorizontalScale<20&&VerticalScale>.05&&VerticalScale<20&&MatrixValid;}}
  bool MatrixValid {get{if(ViewProjection==null)return true;if(ViewProjection.Length!=16)return false;foreach(float v in ViewProjection)if(!MathUtil.Finite(v))return false;return true;}}
  public Vec View(Vec world){
   if(ViewProjection!=null){float[] m=ViewProjection;return new Vec(world.X*m[0]+world.Y*m[4]+world.Z*m[8]+m[12],world.X*m[1]+world.Y*m[5]+world.Z*m[9]+m[13],world.X*m[3]+world.Y*m[7]+world.Z*m[11]+m[15]);}
   Vec d=world-Position;return new Vec(Vec.Dot(d,Right),Vec.Dot(d,Up),Vec.Dot(d,Forward));
  }
  PointF Pixel(Vec v,int w,int h){return new PointF(w*(.5f+.5f*v.X*(ViewProjection==null?HorizontalScale:1)/v.Z),h*(.5f-.5f*v.Y*(ViewProjection==null?VerticalScale:1)/v.Z));}
  public bool Project(Vec world,int w,int h,out PointF result){Vec v=View(world);result=PointF.Empty;if(v.Z<.15f)return false;result=Pixel(v,w,h);return MathUtil.Finite(result.X)&&MathUtil.Finite(result.Y);}
  public bool Segment(Vec a,Vec b,int w,int h,out PointF pa,out PointF pb){
   Vec x=View(a),y=View(b);pa=pb=PointF.Empty;const float near=.15f;
   if(x.Z<near&&y.Z<near)return false;
   if(x.Z<near)x=x+(y-x)*((near-x.Z)/(y.Z-x.Z));
   if(y.Z<near)y=y+(x-y)*((near-y.Z)/(x.Z-y.Z));
   pa=Pixel(x,w,h);pb=Pixel(y,w,h);
   // Clip the 2D segment before GDI to prevent huge coordinates near the camera.
   float dx=pb.X-pa.X,dy=pb.Y-pa.Y,t0=0,t1=1;
   float[] p={-dx,dx,-dy,dy},q={pa.X,w-pa.X,pa.Y,h-pa.Y};
   for(int n=0;n<4;n++){if(p[n]==0){if(q[n]<0)return false;}else{float t=q[n]/p[n];if(p[n]<0)t0=Math.Max(t0,t);else t1=Math.Min(t1,t);if(t0>t1)return false;}}
   PointF origin=pa;pa=new PointF(origin.X+t0*dx,origin.Y+t0*dy);pb=new PointF(origin.X+t1*dx,origin.Y+t1*dy);return true;
  }
 }
 public class Replay {
  public const int MaxSamples=1000000;
  public string GameHash,EventLabel,Author,CreatedUtc;
  public uint VehicleTag,VehicleResourceId,EventId;public int FormatVersion=4;public bool Completed;public double FinalTime;
  public List<Sample> Samples=new List<Sample>();
  public double Duration {get{return Samples.Count==0?0:Samples[Samples.Count-1].Time;}}
  public Sample At(double t){
   if(Samples.Count<2||t<0||t>Duration)return null;
   int lo=0,hi=Samples.Count-1;
   while(hi-lo>1){int mid=(lo+hi)/2;if(Samples[mid].Time<=t)lo=mid;else hi=mid;}
   Sample a=Samples[lo],b=Samples[hi];float f=(float)((t-a.Time)/(b.Time-a.Time));
   // Do not draw a smooth shortcut across resets, teleports, or missing intervals.
   if(b.Time-a.Time>.5||(b.Pose.Position-a.Pose.Position).Length>Math.Max(30,220*(b.Time-a.Time)))return null;
   return new Sample{Time=t,Pose=Pose.Blend(a.Pose,b.Pose,f),Speed=a.Speed*(1-f)+b.Speed*f};
  }
  static void Text(BinaryWriter w,string value){byte[] b=Encoding.UTF8.GetBytes(value??"");if(b.Length>1024)throw new InvalidDataException("Metadata too long");w.Write(b.Length);w.Write(b);}
  static string Text(BinaryReader r){int n=r.ReadInt32();if(n<0||n>1024)throw new InvalidDataException("Bad metadata length");byte[] b=r.ReadBytes(n);if(b.Length!=n)throw new EndOfStreamException();return new UTF8Encoding(false,true).GetString(b);}
  static void VecWrite(BinaryWriter w,Vec v){w.Write(v.X);w.Write(v.Y);w.Write(v.Z);}
  static Vec VecRead(BinaryReader r){return new Vec(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());}
  public void Validate(){
   if(Samples.Count<2||Samples.Count>MaxSamples)throw new InvalidDataException("Replay needs 2 to 1000000 samples");
   if(!GameLayout.HashValid(GameHash))throw new InvalidDataException("Missing game identity");
   double last=-1;
   foreach(Sample s in Samples){if(!MathUtil.Finite(s.Time)||s.Time<0||s.Time>21600||s.Time<=last||s.Pose==null||!s.Pose.Valid||!MathUtil.Finite(s.Speed)||s.Speed<0||s.Speed>500)throw new InvalidDataException("Invalid replay sample");last=s.Time;}
   if(Completed&&(EventId==0||!MathUtil.Finite(FinalTime)||FinalTime<=0||FinalTime>21600))throw new InvalidDataException("Invalid complete-race metadata");
   if(Samples[0].Time!=0)throw new InvalidDataException("First time must be zero");
  }
  public void Save(string file){
   Validate();string tmp=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(file)),".hprghost-"+Guid.NewGuid().ToString("N")+".tmp");
   try{using(var w=new BinaryWriter(File.Create(tmp))){w.Write(Encoding.ASCII.GetBytes("HPRGHOST"));w.Write(4);Text(w,GameHash);Text(w,EventLabel);Text(w,Author);Text(w,CreatedUtc);w.Write(VehicleTag);w.Write(VehicleResourceId);w.Write(EventId);w.Write(Completed);w.Write(FinalTime);w.Write(Samples.Count);
     foreach(Sample s in Samples){w.Write(s.Time);VecWrite(w,s.Pose.Right);VecWrite(w,s.Pose.Up);VecWrite(w,s.Pose.Forward);VecWrite(w,s.Pose.Position);w.Write(s.Speed);}}
    if(File.Exists(file))File.Replace(tmp,file,null);else File.Move(tmp,file);
   }finally{if(File.Exists(tmp))File.Delete(tmp);}
  }
  public static Replay Load(string file){
   if(new FileInfo(file).Length>64*1024*1024)throw new InvalidDataException("Replay too large");
   Replay replay=new Replay();
   using(var r=new BinaryReader(File.OpenRead(file))){
    if(Encoding.ASCII.GetString(r.ReadBytes(8))!="HPRGHOST")throw new InvalidDataException("Unknown replay format");int version=r.ReadInt32();if(version!=1&&version!=2&&version!=3&&version!=4)throw new InvalidDataException("Unknown replay version");
    replay.FormatVersion=version;replay.GameHash=Text(r);replay.EventLabel=Text(r);replay.Author=Text(r);replay.CreatedUtc=Text(r);replay.VehicleTag=r.ReadUInt32();if(version>=3)replay.VehicleResourceId=r.ReadUInt32();if(version>=2){replay.EventId=r.ReadUInt32();replay.Completed=r.ReadBoolean();replay.FinalTime=r.ReadDouble();}int n=r.ReadInt32();
    if(n<2||n>MaxSamples||r.BaseStream.Length-r.BaseStream.Position!=(long)n*60)throw new InvalidDataException("Invalid or truncated replay length");
    for(int i=0;i<n;i++)replay.Samples.Add(new Sample{Time=r.ReadDouble(),Pose=new Pose{Right=VecRead(r),Up=VecRead(r),Forward=VecRead(r),Position=VecRead(r)},Speed=r.ReadSingle()});
   }replay.Validate();if(replay.Completed&&replay.FormatVersion<4&&Math.Abs(replay.FinalTime-replay.Duration)>0.000001)throw new InvalidDataException("Invalid legacy complete-race metadata");return replay;
  }
 }
}
