using System;
using System.IO;
namespace HprGhost {
 public static class LoaderTests {
  public static void Run(Action<bool,string> check,string folder){
   check(AsiLoader.Hash(AsiLoader.Bytes)==AsiLoader.Digest&&AsiLoader.IsX64(AsiLoader.Bytes),"bundled official loader has the pinned SHA256 and x64 PE architecture");
   bool originalLanguage=UiText.English;try{foreach(bool english in new[]{false,true}){UiText.English=english;using(var dialog=new ComponentDialog(true,false)){check(!dialog.RemoveLoader&&dialog.AcceptButton==dialog.CancelButton,"component uninstall defaults to keeping the shared loader and cancelling");dialog.Show();dialog.Update();using(var image=new System.Drawing.Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new System.Drawing.Rectangle(0,0,dialog.Width,dialog.Height));image.Save(Path.Combine(folder,english?"components-english.png":"components-chinese.png"),System.Drawing.Imaging.ImageFormat.Png);}}using(var dialog=new ComponentDialog(false,true)){System.Windows.Forms.Control remove=null;foreach(System.Windows.Forms.Control control in dialog.Controls)if(control is ActionButton&&((ActionButton)control).DialogResult==System.Windows.Forms.DialogResult.OK)remove=control;check(remove!=null&&!remove.Enabled&&!dialog.RemoveLoader,"uninstall UI disables removal while the game runs and never selects an existing loader");}}}finally{UiText.English=originalLanguage;}
   string root=Path.Combine(folder,"loader-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string target=Path.Combine(root,AsiLoader.FileName),receipt=Path.Combine(root,AsiLoader.ReceiptName),plugin=Path.Combine(root,NativeComponent.FileName);byte[] unrelated=System.Text.Encoding.ASCII.GetBytes("Unrelated plugin");
   try{
    check(AsiLoader.Install(root,true)&&File.Exists(target)&&File.Exists(receipt),"a missing loader installs immediately even when the game is already running");File.Delete(target);File.Delete(receipt);
    File.WriteAllBytes(target,unrelated);bool refused=false;try{AsiLoader.Install(root,false);}catch(IOException){refused=true;}check(refused&&AsiLoader.Hash(File.ReadAllBytes(target))==AsiLoader.Hash(unrelated),"automatic loader installation preserves an unrelated dinput8 DLL");File.Delete(target);
    check(AsiLoader.Install(root,false)&&AsiLoader.HasLoader(root)&&AsiLoader.OwnsLoader(root),"first playback installs the embedded x64 loader and a recognizable ownership receipt");
    check(AsiLoader.Install(root,true),"an existing loader is reused while the game runs");
    File.WriteAllBytes(plugin,NativeComponent.Bytes);refused=false;try{AsiLoader.Uninstall(root,true,true);}catch(IOException){refused=true;}check(refused&&File.Exists(plugin)&&File.Exists(target),"uninstall refuses to touch loaded components while the game runs");
    AsiLoader.Uninstall(root,false,false);check(!File.Exists(plugin)&&File.Exists(target)&&File.Exists(receipt),"default uninstall removes our playback plugin and preserves the shared ASI loader");
    File.WriteAllBytes(plugin,unrelated);bool preserved=AsiLoader.Uninstall(root,true,false);check(preserved&&File.Exists(plugin)&&!File.Exists(target)&&!File.Exists(receipt),"explicit loader removal affects only our unchanged loader and preserves modified playback plugins");File.Delete(plugin);
    File.WriteAllBytes(target,AsiLoader.Bytes);check(AsiLoader.Install(root,false)&&!AsiLoader.OwnsLoader(root)&&!File.Exists(receipt),"preexisting Ultimate ASI Loader is reused without claiming ownership");AsiLoader.Uninstall(root,true,false);check(File.Exists(target),"uninstall preserves a preexisting loader even when loader removal was requested");File.Delete(target);
    AsiLoader.Install(root,false);File.WriteAllBytes(target,unrelated);preserved=AsiLoader.Uninstall(root,true,false);check(preserved&&File.Exists(target)&&File.Exists(receipt),"a loader changed after installation is retained together with its receipt");File.Delete(target);File.Delete(receipt);
    string alternate=Path.Combine(root,"dxgi.dll");File.WriteAllBytes(alternate,AsiLoader.Bytes);check(AsiLoader.Install(root,false)&&!File.Exists(target)&&!File.Exists(receipt),"a recognized loader using an alternative proxy name is reused without a duplicate loader");File.Delete(alternate);
    File.WriteAllText(receipt,"Unrelated marker");refused=false;try{AsiLoader.Install(root,false);}catch(IOException){refused=true;}check(refused&&!File.Exists(target)&&File.ReadAllText(receipt)=="Unrelated marker","an existing unknown ownership receipt is not overwritten");File.Delete(receipt);
    File.WriteAllText(Path.Combine(root,"sentinel.hprghost"),"Replay sentinel");File.WriteAllText(Path.Combine(root,"GameFolder.txt"),"Configuration sentinel");AsiLoader.Install(root,false);File.WriteAllBytes(plugin,NativeComponent.Bytes);AsiLoader.Uninstall(root,true,false);check(File.ReadAllText(Path.Combine(root,"sentinel.hprghost"))=="Replay sentinel"&&File.ReadAllText(Path.Combine(root,"GameFolder.txt"))=="Configuration sentinel","full component uninstall preserves recordings and configuration");
   }finally{if(!Path.GetFullPath(root).StartsWith(Path.GetFullPath(folder)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Unsafe test cleanup");foreach(string name in new[]{AsiLoader.FileName,AsiLoader.ReceiptName,NativeComponent.FileName,"dxgi.dll","sentinel.hprghost","GameFolder.txt"}){string file=Path.Combine(root,name);if(File.Exists(file))File.Delete(file);}Directory.Delete(root);}
  }
 }
}



