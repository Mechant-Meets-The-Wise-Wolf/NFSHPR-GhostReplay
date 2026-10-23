using System;
using System.ComponentModel;
using System.IO;
using System.Text;
namespace HprGhost {
 public enum LogCode {APP_STARTED,APP_STOPPED,GAME_CONNECTED,GAME_DISCONNECTED,LAYOUT_UNSUPPORTED,ACCESS_DENIED,DATA_UNAVAILABLE,INVALID_DATA,OPERATION_UNAVAILABLE,UNEXPECTED_ERROR,RECORDING_STARTED,RECORDING_STOPPED,RECORDING_SAVED,REPLAY_STARTED,REPLAY_STOPPED}
 public sealed class GameAbsentException : InvalidOperationException {public GameAbsentException():base("等待游戏启动。") {}}
 public sealed class LayoutMismatchException : InvalidOperationException {public LayoutMismatchException(string message):base(message) {}}
 public static class AppLog {
#if DEVELOPMENT
  public static bool DetailedDiagnostics;
  static StatusLog current;
#else
  static ErrorLog current;
#endif
  public static void Start(){
#if DEVELOPMENT
   if(DetailedDiagnostics)return;try{current=new StatusLog(Path.Combine(AppPaths.BaseFolder,"Logs"));current.Start();}catch(IOException){}catch(UnauthorizedAccessException){}
#else
   current=new ErrorLog(Path.Combine(AppPaths.BaseFolder,"Logs"));current.Start();
#endif
  }
  [System.Diagnostics.Conditional("DEVELOPMENT")]
  public static void Write(LogCode code){
#if DEVELOPMENT
   if(!DetailedDiagnostics&&current!=null)current.Write(code);
#endif
  }
  public static LogCode? Classify(Exception error){if(error is GameAbsentException)return null;if(error is LayoutMismatchException)return LogCode.LAYOUT_UNSUPPORTED;var win=error as Win32Exception;if(error is UnauthorizedAccessException||(win!=null&&win.NativeErrorCode==5))return LogCode.ACCESS_DENIED;if(error is InvalidDataException)return LogCode.INVALID_DATA;if(error is IOException)return LogCode.DATA_UNAVAILABLE;if(error is InvalidOperationException)return LogCode.OPERATION_UNAVAILABLE;return LogCode.UNEXPECTED_ERROR;}
  public static void Failure(Exception error){LogCode? code=Classify(error);if(code.HasValue){
#if DEVELOPMENT
   Write(code.Value);
#else
   if(current!=null)current.Write(code.Value);
#endif
  }}
 }
}
