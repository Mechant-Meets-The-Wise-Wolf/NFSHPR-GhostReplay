using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
namespace HprGhost {
 public static class AppInfo {public const string Name="HPRGhostReplay",DisplayVersion="测试版 0.1",Version="0.1",FileVersion="0.1.0.1";public static System.Drawing.Icon CreateIcon(){using(var stream=typeof(AppInfo).Assembly.GetManifestResourceStream("HprGhost.AppIcon.ico")){if(stream==null)throw new InvalidOperationException("Missing embedded application icon");using(var icon=new System.Drawing.Icon(stream,new System.Drawing.Size(32,32)))return (System.Drawing.Icon)icon.Clone();}}}
 public static class AppPaths {
  public static string BaseFolder {get{return AppDomain.CurrentDomain.BaseDirectory;}}
  public static string Recordings {get{return Path.Combine(BaseFolder,"Recordings");}}
  public static string LogPath(string name){string dir=Path.Combine(BaseFolder,"Logs");Directory.CreateDirectory(dir);return Path.Combine(dir,name);}
  public static IEnumerable<string> NameLines(string name){using(var stream=typeof(AppPaths).Assembly.GetManifestResourceStream("HprGhost."+name)){if(stream!=null){using(var reader=new StreamReader(stream))return reader.ReadToEnd().Split(new char[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);}}string file=Path.Combine(BaseFolder,name);return File.Exists(file)?File.ReadAllLines(file):new string[0];}
  public static string SavedGameFolder {get{try{string f=Path.Combine(BaseFolder,"GameFolder.txt");return File.Exists(f)?File.ReadAllText(f).Trim():"";}catch(IOException){return "";}catch(UnauthorizedAccessException){return "";}}}
  public static void SaveGameFolder(string folder){if(!GameLocation.Valid(folder))throw new InvalidDataException("请选择游戏目录中的 NFS11Remastered.exe。");File.WriteAllText(Path.Combine(BaseFolder,"GameFolder.txt"),Path.GetFullPath(folder));}
 }
 public static class GameLocation {
  public static bool Valid(string folder){try{return !string.IsNullOrWhiteSpace(folder)&&File.Exists(Path.Combine(folder,"NFS11Remastered.exe"))&&File.Exists(Path.Combine(folder,"SHADERS.BNDL"))&&Directory.Exists(Path.Combine(folder,"VEHICLES"));}catch(ArgumentException){return false;}catch(NotSupportedException){return false;}catch(IOException){return false;}}
  // Actual running game wins over a saved location or the old development layout.
  public static string Find(string appFolder,string savedFolder,string runningFolder){if(Valid(runningFolder))return Path.GetFullPath(runningFolder);if(Valid(savedFolder))return Path.GetFullPath(savedFolder);string folder=Path.GetFullPath(appFolder);for(int i=0;i<4&&folder!=null;i++,folder=Path.GetDirectoryName(folder))if(Valid(folder))return folder;return "";}
  public static string Resolve(){string running="";var processes=Process.GetProcessesByName("NFS11Remastered");try{if(processes.Length==1)try{running=Path.GetDirectoryName(processes[0].MainModule.FileName);}catch(Win32Exception){}catch(InvalidOperationException){}}finally{foreach(var process in processes)process.Dispose();}string found=Find(AppPaths.BaseFolder,AppPaths.SavedGameFolder,running);return found!=""?found:SteamGameDiscovery.Resolve();}
 }
}



