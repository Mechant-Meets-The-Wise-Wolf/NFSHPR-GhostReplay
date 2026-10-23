using System;
using System.IO;
using System.Windows.Forms;
namespace HprGhost {
 public static class NativeUiCheck {
  public static int Run(string[] args){if(args.Length!=2)throw new ArgumentException("Expected a replay path for the native integration check");string path=Path.GetFullPath(args[1]);if(!File.Exists(path)||!path.StartsWith(Path.GetFullPath(AppPaths.Recordings)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Use a replay copy inside this check application's Recordings folder");Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);using(var window=new ControlWindow()){window.Shown+=delegate{try{window.ChooseNativeCheck(path);}catch(Exception ex){Directory.CreateDirectory(Path.Combine(AppPaths.BaseFolder,"research"));File.WriteAllText(Path.Combine(AppPaths.BaseFolder,"research","native-ui-check-error.txt"),ex.ToString());window.Close();}};Application.Run(window);}return 0;}
 }
 public sealed partial class ControlWindow {internal void ChooseNativeCheck(string path){RefreshFiles(path);ReplayChoice item=Selected();if(item==null||!string.Equals(Path.GetFullPath(item.File),path,StringComparison.OrdinalIgnoreCase))throw new IOException("Replay selection did not match requested check copy");Choose();}}
}
