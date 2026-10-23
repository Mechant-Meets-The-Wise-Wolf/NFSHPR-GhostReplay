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
 public static class TimingProbe {
  public static int Run(string[] args){
   int seconds=args.Length>1?int.Parse(args[1],CultureInfo.InvariantCulture):25;if(seconds<1||seconds>60)throw new ArgumentException("Probe seconds must be 1..60");
   string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"research");Directory.CreateDirectory(folder);var watch=Stopwatch.StartNew();int samples=0;
   using(var reader=new GameReader())using(var output=new StreamWriter(Path.Combine(folder,"timing-live.csv"),false,new UTF8Encoding(false))){
    output.WriteLine("mode,wall_s,race_us,simulation_us,x,y,z,speed,vx,vy,vz");
    while(watch.Elapsed.TotalSeconds<seconds){try{
     RaceState legacy=reader.ReadRace();if(legacy.Active&&legacy.Running){Frame f=reader.ReadFrame();Write(output,"legacy",watch.Elapsed.TotalSeconds,legacy,f);}
     RaceState state;Frame next;if(reader.TryReadSnapshot(out state,out next)&&next!=null&&state.Running){Write(output,"coherent",watch.Elapsed.TotalSeconds,state,next);samples++;}
    }catch(IOException){}catch(InvalidOperationException){}catch(InvalidDataException){}Thread.Sleep(2);}
    File.WriteAllText(Path.Combine(folder,"timing-live-summary.txt"),"Accepted: "+reader.SnapshotAccepted+"\r\nRetries: "+reader.SnapshotRetries+"\r\nDeferred: "+reader.SnapshotDeferred+"\r\nRunning samples: "+samples);return samples>0?0:1;
   }
  }
  static void Write(StreamWriter w,string mode,double wall,RaceState s,Frame f){w.WriteLine(string.Format(CultureInfo.InvariantCulture,"{0},{1:R},{2},{3},{4:R},{5:R},{6:R},{7:R},{8:R},{9:R},{10:R}",mode,wall,s.RaceTicks,f.Tick,f.Pose.Position.X,f.Pose.Position.Y,f.Pose.Position.Z,f.Speed,f.Velocity.X,f.Velocity.Y,f.Velocity.Z));}
 }
}
