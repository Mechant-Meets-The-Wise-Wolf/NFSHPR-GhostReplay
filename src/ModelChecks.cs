using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Media.Media3D;
namespace HprGhost {
 public static class ModelChecks {
  public static int Run(string[] args){
   string root=args.Length>2?Path.GetFullPath(args[2]):GameLocation.Resolve();var library=new VehicleLibrary(root);string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"research","model-format");Directory.CreateDirectory(folder);var lines=new List<string>();int failed=0;lines.Add("id,name,vertices,triangles,wheels,width,height,length,load_ms,result");string[] paths=args.Length>1?new string[]{Path.Combine(root,"VEHICLES","VEH_"+args[1]+"_MS.BIN")}:Directory.GetFiles(Path.Combine(root,"VEHICLES"),"VEH_*_MS.BIN");
   foreach(string path in paths){uint id=uint.Parse(Path.GetFileName(path).Split('_')[1]);var time=Stopwatch.StartNew();try{VehicleModel m=library.Load(id);lines.Add(string.Join(",",new string[]{id.ToString(),library.Name(id),m.Vertices.ToString(),m.Triangles.ToString(),m.Wheels.ToString(),m.Bounds.SizeX.ToString("F3",CultureInfo.InvariantCulture),m.Bounds.SizeY.ToString("F3",CultureInfo.InvariantCulture),m.Bounds.SizeZ.ToString("F3",CultureInfo.InvariantCulture),time.ElapsedMilliseconds.ToString(),"OK"}));if(id==329012){ModelOverlay.Preview(m,Path.Combine(folder,"sl65-original-mesh.png"),1);ModelOverlay.Preview(m,Path.Combine(folder,"sl65-transparent-mesh.png"),.45);}}catch(Exception ex){failed++;lines.Add(id+",ERROR,,,,,,,,"+ex.Message.Replace(',',';'));File.AppendAllText(Path.Combine(folder,"model-errors.txt"),id+" "+ex.ToString()+Environment.NewLine);}}
   File.WriteAllLines(Path.Combine(folder,"model-validation.csv"),lines);return failed==0?0:1;
  }
 }
}