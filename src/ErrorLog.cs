using System;
using System.IO;
using System.Text;
namespace HprGhost {
 // A release creates no log during healthy operation. Only fixed failure codes
 // reach this sink; the first failure starts a bounded report for this run.
 public sealed class ErrorLog {
  public const int MaxBytes=16*1024;
  readonly string folder,file;readonly object sync=new object();bool first=true;LogCode? last;
  static readonly Encoding utf8=new UTF8Encoding(false);
  static readonly string Header=AppInfo.Name+" "+AppInfo.Version+"\n";
  public ErrorLog(string logsFolder){folder=Path.GetFullPath(logsFolder);file=Path.Combine(folder,"errors.log");}
  public void Start(){lock(sync){first=true;last=null;if(!Directory.Exists(folder))return;foreach(string name in new string[]{"automatic.log","compatibility.log","race-transitions.log","diagnostic-error.txt","status.log"})try{File.Delete(Path.Combine(folder,name));}catch(IOException){}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}}}
  static bool IsFailure(LogCode code){switch(code){case LogCode.LAYOUT_UNSUPPORTED:case LogCode.ACCESS_DENIED:case LogCode.DATA_UNAVAILABLE:case LogCode.INVALID_DATA:case LogCode.OPERATION_UNAVAILABLE:case LogCode.UNEXPECTED_ERROR:return true;default:return false;}}
  public void Write(LogCode code){if(!IsFailure(code))return;lock(sync){if(last==code)return;try{Directory.CreateDirectory(folder);string line=code.ToString()+"\n";if(first||!File.Exists(file)||new FileInfo(file).Length+utf8.GetByteCount(line)>MaxBytes)File.WriteAllText(file,Header,utf8);File.AppendAllText(file,line,utf8);first=false;last=code;}catch(IOException){}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}}}
 }
}
