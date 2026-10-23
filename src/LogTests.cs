using System;
using System.IO;
using System.Text;
namespace HprGhost {
 public static class LogTests {
  sealed class PrivateException : Exception {public override string Message {get{throw new Exception("message must never be read");}}public override string ToString(){throw new Exception("stack must never be read");}}
  public static void Run(Action<bool,string> check,string parent){
   string test=Path.Combine(parent,"private-logs-"+Guid.NewGuid().ToString("N")),folder=Path.Combine(test,"Logs");Directory.CreateDirectory(folder);
   try{
    foreach(string name in new string[]{"automatic.log","compatibility.log","race-transitions.log","diagnostic-error.txt"})File.WriteAllText(Path.Combine(folder,name),"legacy private data");
    string replay=Path.Combine(test,"keep.hprghost"),settings=Path.Combine(test,"GameFolder.txt"),other=Path.Combine(folder,"my-note.txt");File.WriteAllText(replay,"keep replay");File.WriteAllText(settings,"keep settings");File.WriteAllText(other,"keep note");
    var log=new StatusLog(folder);log.Start();string file=Path.Combine(folder,"status.log");
    check(!File.Exists(Path.Combine(folder,"race-transitions.log"))&&!File.Exists(Path.Combine(folder,"automatic.log"))&&!File.Exists(Path.Combine(folder,"compatibility.log"))&&!File.Exists(Path.Combine(folder,"diagnostic-error.txt")),"new session removes only the known legacy default logs containing private details");
    check(File.ReadAllText(replay)=="keep replay"&&File.ReadAllText(settings)=="keep settings"&&File.ReadAllText(other)=="keep note","log cleanup preserves recordings settings and unrelated files");
    log.Write(LogCode.GAME_CONNECTED);log.Write(LogCode.GAME_CONNECTED);check(File.ReadAllLines(file).Length==3,"consecutive repeated states are not logged repeatedly");
    string secret=@"C:\Users\PrivateUser\private\run.hprghost 0x12345678 2026-10-16T19:01:00+08:00 PID=9876";
    foreach(Exception error in new Exception[]{new IOException(secret),new InvalidDataException(secret),new UnauthorizedAccessException(secret),new LayoutMismatchException(secret),new PrivateException()}){LogCode? code=AppLog.Classify(error);if(code.HasValue)log.Write(code.Value);}
    check(AppLog.Classify(new PrivateException())==LogCode.UNEXPECTED_ERROR&&AppLog.Classify(new GameAbsentException())==null,"error classification never reads exception messages or stacks and routine game waiting is silent");
    string text=File.ReadAllText(file);check(!text.Contains("PrivateUser")&&!text.Contains("0x12345678")&&!text.Contains("2026-10-16")&&!text.Contains("9876")&&!text.Contains(".hprghost")&&!text.Contains(":\\"),"default log does not contain exception paths addresses filenames calendar times or PIDs");
    log.Write((LogCode)int.MaxValue);foreach(string line in File.ReadAllLines(file)){if(line.StartsWith(AppInfo.Name+" "))continue;LogCode parsed;check(Enum.TryParse<LogCode>(line,out parsed)&&Enum.IsDefined(typeof(LogCode),parsed),"every emitted log row belongs to the fixed status-code allowlist");}
    for(int i=0;i<4000;i++)log.Write((i&1)==0?LogCode.REPLAY_STARTED:LogCode.REPLAY_STOPPED);
    check(new FileInfo(file).Length<=StatusLog.MaxBytes&&File.ReadAllText(file).EndsWith("REPLAY_STOPPED\n"),"log stays within 16 KiB and retains the latest state during a repeated-error flood");
    log.Start();check(File.ReadAllLines(file).Length==2&&!File.ReadAllText(file).Contains("REPLAY_STARTED"),"a new session clears prior session history instead of retaining dates and play patterns");
    string blocked=Path.Combine(test,"not-a-folder");File.WriteAllText(blocked,"keep");var unwritable=new StatusLog(blocked);unwritable.Start();unwritable.Write(LogCode.UNEXPECTED_ERROR);check(File.ReadAllText(blocked)=="keep","log I/O failure cannot break recording or overwrite an unrelated file");
   }finally{string full=Path.GetFullPath(test),allowed=Path.GetFullPath(parent)+Path.DirectorySeparatorChar;if(!full.StartsWith(allowed,StringComparison.OrdinalIgnoreCase))throw new IOException("Unsafe log test cleanup");Directory.Delete(full,true);}
  }
 }
}
