using System;
using System.IO;
namespace HprGhost {
 public static class ReleaseLogTests {
  public static void Run(Action<bool,string> check,string parent){
   string root=Path.Combine(parent,"release-logs-"+Guid.NewGuid().ToString("N")),folder=Path.Combine(root,"Logs");Directory.CreateDirectory(root);
   try{var log=new ErrorLog(folder);log.Start();foreach(LogCode code in new LogCode[]{LogCode.APP_STARTED,LogCode.GAME_CONNECTED,LogCode.RECORDING_STARTED,LogCode.RECORDING_STOPPED,LogCode.RECORDING_SAVED,LogCode.REPLAY_STARTED,LogCode.REPLAY_STOPPED,LogCode.GAME_DISCONNECTED,LogCode.APP_STOPPED})log.Write(code);
    check(!Directory.Exists(folder),"healthy release recording replay and connection states create no log directory or file");
    log.Write(LogCode.ACCESS_DENIED);string file=Path.Combine(folder,"errors.log");check(File.ReadAllLines(file).Length==2&&File.ReadAllText(file).EndsWith("ACCESS_DENIED\n"),"release creates only a fixed error-code report on the first fault");log.Write(LogCode.ACCESS_DENIED);log.Write((LogCode)int.MaxValue);check(File.ReadAllLines(file).Length==2,"release error sink ignores repeated and unknown values");
    for(int i=0;i<4000;i++)log.Write((i&1)==0?LogCode.ACCESS_DENIED:LogCode.INVALID_DATA);check(new FileInfo(file).Length<=ErrorLog.MaxBytes,"release failure report remains bounded to 16 KiB");
    File.WriteAllText(Path.Combine(folder,"status.log"),"old session metadata");File.WriteAllText(Path.Combine(root,"keep.hprghost"),"original replay");log.Start();check(!File.Exists(Path.Combine(folder,"status.log"))&&File.ReadAllText(Path.Combine(root,"keep.hprghost"))=="original replay","release startup removes old normal logs without touching a replay");
    log.Write(LogCode.DATA_UNAVAILABLE);check(File.ReadAllLines(file).Length==2&&!File.ReadAllText(file).Contains("ACCESS_DENIED"),"first fault in a new run replaces the preceding failure report");
    string blocked=Path.Combine(root,"not-a-directory");File.WriteAllText(blocked,"keep");var noAccess=new ErrorLog(blocked);noAccess.Start();noAccess.Write(LogCode.UNEXPECTED_ERROR);check(File.ReadAllText(blocked)=="keep","release error logging failures do not escape into the recorder");
   }finally{string full=Path.GetFullPath(root),allowed=Path.GetFullPath(parent)+Path.DirectorySeparatorChar;if(!full.StartsWith(allowed,StringComparison.OrdinalIgnoreCase))throw new IOException("Unsafe release log test cleanup");Directory.Delete(full,true);}
  }
 }
}
