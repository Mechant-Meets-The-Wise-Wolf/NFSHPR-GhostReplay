using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
#if DEVELOPMENT
[assembly: System.Reflection.AssemblyConfiguration("Development")]
#else
[assembly: System.Reflection.AssemblyConfiguration("Release")]
#endif

[assembly: System.Reflection.AssemblyTitle("HPRGhostReplay")]
[assembly: System.Reflection.AssemblyProduct("HPRGhostReplay")]
[assembly: System.Reflection.AssemblyVersion(HprGhost.AppInfo.FileVersion)]
[assembly: System.Reflection.AssemblyFileVersion(HprGhost.AppInfo.FileVersion)]
[assembly: System.Reflection.AssemblyInformationalVersion(HprGhost.AppInfo.Version)]
[assembly: System.Reflection.AssemblyCopyright("Copyright (c) 2026 LoughShinny302")]

namespace HprGhost {
 public static class Program {
  [STAThread] public static int Main(string[] args){
   try{
#if DEVELOPMENT
    AppLog.DetailedDiagnostics=args.Length>0&&args[0]!="--capture-ui";
    int result;if(DevelopmentCommands.TryRun(args,out result))return result;
    if(args.Length>0&&!(args.Length==2&&args[0]=="--capture-ui"))return 2;
#else
    // A release has no diagnostic commands, exports, or hidden capture mode.
    if(args.Length!=0)return 2;
#endif
    Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
    using(var gate=new Mutex(false,"Local\\HPRGhostReplay")){bool owns=false;try{try{owns=gate.WaitOne(0,false);}catch(AbandonedMutexException){owns=true;}if(!owns){MessageBox.Show(UiText.Get("HPRGhostReplay 已经在运行，请使用已打开的窗口。"),"HPRGhostReplay");return 0;}
     AppLog.Start();var window=new ControlWindow();
#if DEVELOPMENT
     if(args.Length==2&&args[0]=="--capture-ui")window.CapturePath=Path.GetFullPath(args[1]);
#endif
     Application.Run(window);return 0;
    }finally{if(owns){AppLog.Write(LogCode.APP_STOPPED);gate.ReleaseMutex();}}}
   }catch(Exception ex){AppLog.Failure(ex);
#if DEVELOPMENT
    if(AppLog.DetailedDiagnostics){string folder=Path.Combine(AppPaths.BaseFolder,"research");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"diagnostic-error.txt"),ex.ToString());}else
#endif
    MessageBox.Show(UiText.ErrorText(ex.Message),"HPRGhostReplay");return 1;
   }
  }
 }
}
