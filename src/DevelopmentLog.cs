using System;
using System.IO;
using System.Text;
namespace HprGhost {
 // Only enum values may cross this boundary. Exception text, identifiers and
 // user data never enter the log, including via an InnerException or ToString.
 public sealed class StatusLog {
  public const int MaxBytes=16*1024;
  readonly string folder,file;readonly object sync=new object();LogCode? last;bool started;
  static readonly Encoding utf8=new UTF8Encoding(false);
  static readonly string Header=AppInfo.Name+" "+AppInfo.Version+"\n";
  public StatusLog(string logsFolder){folder=Path.GetFullPath(logsFolder);file=Path.Combine(folder,"status.log");}
  public void Start(){lock(sync){try{Directory.CreateDirectory(folder);foreach(string name in new string[]{"automatic.log","compatibility.log","race-transitions.log","diagnostic-error.txt"})try{File.Delete(Path.Combine(folder,name));}catch(IOException){}catch(UnauthorizedAccessException){}File.WriteAllText(file,Header,utf8);last=null;started=true;Append(LogCode.APP_STARTED);}catch(IOException){}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}}}
  public void Write(LogCode code){lock(sync){if(!started||!Enum.IsDefined(typeof(LogCode),code)||last==code)return;try{Append(code);}catch(IOException){}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}}}
  void Append(LogCode code){string line=code.ToString()+"\n";if(new FileInfo(file).Length+utf8.GetByteCount(line)>MaxBytes)File.WriteAllText(file,Header,utf8);File.AppendAllText(file,line,utf8);last=code;}
 }
}
