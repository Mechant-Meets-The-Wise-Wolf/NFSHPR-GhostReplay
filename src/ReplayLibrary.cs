using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
namespace HprGhost {
 public static class RaceTime {
  public static string Format(double seconds){if(!MathUtil.Finite(seconds)||seconds<0)return "00:00.00";long cs=(long)Math.Floor(seconds*100+.000001);return (cs/6000).ToString("00",CultureInfo.InvariantCulture)+":"+((cs/100)%60).ToString("00",CultureInfo.InvariantCulture)+"."+(cs%100).ToString("00",CultureInfo.InvariantCulture);}
  public static string FileFormat(double seconds){return Format(seconds).Replace(":","分").Replace(".","秒");}
 }
 public sealed class ReplayChoice {
  public string File;public Replay Replay;public string VehicleName;
  public string Name {get{return Path.GetFileNameWithoutExtension(File);}}
  static string DefaultSuffix(string name,string stem){if(name==stem)return "";int counter;if(name.StartsWith(stem+"-",StringComparison.Ordinal)&&int.TryParse(name.Substring(stem.Length+1),NumberStyles.None,CultureInfo.InvariantCulture,out counter)&&counter>=2)return name.Substring(stem.Length);return null;}
  string LegacySuffix {get{string suffix=DefaultSuffix(Name,ReplayLibrary.DefaultName(Replay,"Vehicle "+Replay.VehicleResourceId));return suffix??DefaultSuffix(Name,ReplayLibrary.DefaultName(Replay,"车型 "+Replay.VehicleResourceId));}}
  // A friendly display for old automatic placeholder names; never rename the file on disk.
  public string DisplayName {get{string suffix=LegacySuffix;return suffix==null?Name:ReplayLibrary.DefaultName(Replay,VehicleName)+suffix;}}
  public bool HasAutomaticName {get{return LegacySuffix!=null||DefaultSuffix(Name,ReplayLibrary.DefaultName(Replay,VehicleName))!=null;}}
 }
 public sealed class ReplayLibrary {
  public readonly string Folder;readonly Func<uint,string> vehicleName;
  public ReplayLibrary(string folder,Func<uint,string> names){Folder=Path.GetFullPath(folder);vehicleName=names;Directory.CreateDirectory(Folder);}
  public static string Clean(string name,int limit){var b=new StringBuilder();foreach(char c in name??""){if(c<32||Array.IndexOf(Path.GetInvalidFileNameChars(),c)>=0)b.Append('_');else b.Append(c);}string s=b.ToString().Trim().TrimEnd('.',' ');if(s.Length>limit)s=s.Substring(0,limit).TrimEnd('.',' ');return s.Length==0?"未命名":s;}
  public static string DefaultName(Replay r,string car){return Clean(r.EventLabel,40)+"-"+Clean(car,42)+"-"+RaceTime.FileFormat(r.Duration)+"-"+Clean(r.Author,20);}
  string Managed(string file){string full=Path.GetFullPath(file);if(!string.Equals(Path.GetDirectoryName(full),Folder,StringComparison.OrdinalIgnoreCase)||!string.Equals(Path.GetExtension(full),".hprghost",StringComparison.OrdinalIgnoreCase))throw new IOException("只能管理录像库中的幽灵文件。");return full;}
  string Unique(string stem){string f=Path.Combine(Folder,stem+".hprghost");int i=2;while(File.Exists(f))f=Path.Combine(Folder,stem+"-"+(i++)+".hprghost");return f;}
  public List<ReplayChoice> Read(){var list=new List<ReplayChoice>();foreach(string file in Directory.GetFiles(Folder,"*.hprghost")){try{Replay r=Replay.Load(file);if(GameLayout.CanPlay(r))list.Add(new ReplayChoice{File=file,Replay=r,VehicleName=vehicleName(r.VehicleResourceId)});}catch(InvalidDataException){}catch(EndOfStreamException){}}list.Sort(delegate(ReplayChoice a,ReplayChoice b){return string.Compare(b.Replay.CreatedUtc,a.Replay.CreatedUtc,StringComparison.Ordinal);});return list;}
  public string Save(Replay replay){string f=Unique(DefaultName(replay,vehicleName(replay.VehicleResourceId)));replay.Save(f);return f;}
  public string Import(string source){Replay r=Replay.Load(source);if(!GameLayout.CanPlay(r)||r.VehicleResourceId==0)throw new InvalidDataException("请导入包含车型信息的完整赛事录像。");if(string.Equals(Path.GetDirectoryName(Path.GetFullPath(source)),Folder,StringComparison.OrdinalIgnoreCase))return source;string target=Unique(DefaultName(r,vehicleName(r.VehicleResourceId)));File.Copy(source,target);return target;}
  public string Rename(string source,string title){source=Managed(source);title=(title??"").Trim();if(title.EndsWith(".hprghost",StringComparison.OrdinalIgnoreCase))title=title.Substring(0,title.Length-9);if(title.Length==0||title.Length>125||title!=Clean(title,125)||title=="."||title=="..")throw new InvalidDataException("名称不能为空，不能包含 \\ / : * ? \" < > |，最多 125 个字符。");string first=title.Split('.')[0].ToUpperInvariant();if(first=="CON"||first=="PRN"||first=="AUX"||first=="NUL"||System.Text.RegularExpressions.Regex.IsMatch(first,@"^(COM|LPT)[1-9]$"))throw new InvalidDataException("请换一个文件名。");string target=Managed(Path.Combine(Folder,title+".hprghost"));if(string.Equals(source,target,StringComparison.OrdinalIgnoreCase))return source;if(File.Exists(target))throw new IOException("已经有同名录像，请换一个名称。");File.Move(source,target);return target;}
  public void Delete(string file){File.Delete(Managed(file));}
 }
}
