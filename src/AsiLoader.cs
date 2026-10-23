using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
namespace HprGhost {
 public static class AsiLoader {
  public const string FileName="dinput8.dll",ReceiptName="HPRGhost-loader.sha256";
  public const string Digest="031A3E5576D91DCE1E438D36B9A3D462C7334AB4791990A8FF1E3DDC0E132DAF";
  static byte[] bytes;
  public static byte[] Bytes{get{if(bytes==null)using(var source=typeof(AsiLoader).Assembly.GetManifestResourceStream("HprGhost.AsiLoader.dll")){if(source==null)throw new InvalidDataException("加载器资源校验失败，请重新解压软件。");using(var output=new MemoryStream()){source.CopyTo(output);var data=output.ToArray();if(Hash(data)!=Digest||!IsX64(data))throw new InvalidDataException("加载器资源校验失败，请重新解压软件。");bytes=data;}}return bytes;}}
  internal static string Hash(byte[] data){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","");}
  internal static bool IsX64(byte[] data){if(data.Length<64||data[0]!=77||data[1]!=90)return false;int offset=BitConverter.ToInt32(data,60);return offset>=64&&offset<=data.Length-6&&BitConverter.ToUInt32(data,offset)==0x4550&&BitConverter.ToUInt16(data,offset+4)==0x8664;}
  public static bool HasLoader(string root){foreach(string name in new[]{"dinput8.dll","dsound.dll","d3d9.dll","d3d10.dll","d3d11.dll","d3d12.dll","dxgi.dll","winmm.dll","version.dll","wininet.dll","winhttp.dll","xinput1_1.dll","xinput1_2.dll","xinput1_3.dll","xinput1_4.dll","xinput9_1_0.dll","xinputuap.dll","binkw64.dll","bink2w64.dll"}){string path=Path.Combine(root,name);if(!File.Exists(path))continue;var version=FileVersionInfo.GetVersionInfo(path);if((version.ProductName??"").IndexOf("Ultimate-ASI-Loader",StringComparison.OrdinalIgnoreCase)<0&&(version.FileDescription??"").IndexOf("ASI Loader",StringComparison.OrdinalIgnoreCase)<0)continue;if(!IsX64(File.ReadAllBytes(path)))throw new IOException("检测到非 x64 加载器，请先移走错误版本。");return true;}return false;}
  public static bool Install(string root,bool gameRunning){if(HasLoader(root))return true;string target=Path.Combine(root,FileName),receipt=Path.Combine(root,ReceiptName);if(File.Exists(target)||Directory.Exists(target))throw new IOException("游戏目录已有其他 dinput8.dll，自动安装已停止；该文件不会被覆盖。");if(File.Exists(receipt)||Directory.Exists(receipt))throw new IOException("加载器安装标记已存在，请检查 HPRGhost-loader.sha256。");byte[] data=Bytes;string temp=Path.Combine(root,".hprghost-loader-"+Guid.NewGuid().ToString("N")+".tmp");bool marked=false;try{File.WriteAllBytes(temp,data);using(var marker=new FileStream(receipt,FileMode.CreateNew,FileAccess.Write,FileShare.None)){byte[] note=Encoding.ASCII.GetBytes(Digest+"\n");marker.Write(note,0,note.Length);}marked=true;File.Move(temp,target);return true;}catch{if(marked&&File.Exists(receipt)&&File.ReadAllText(receipt).Trim()==Digest)File.Delete(receipt);throw;}finally{if(File.Exists(temp))File.Delete(temp);}}
  public static bool OwnsLoader(string root){string receipt=Path.Combine(root,ReceiptName),target=Path.Combine(root,FileName);return File.Exists(receipt)&&File.ReadAllText(receipt).Trim()==Digest&&File.Exists(target)&&Hash(File.ReadAllBytes(target))==Digest;}
  public static bool Uninstall(string root,bool removeLoader,bool gameRunning){if(gameRunning)throw new IOException("请先退出游戏，再卸载回放组件。");bool preserved=false;string plugin=Path.Combine(root,NativeComponent.FileName);if(File.Exists(plugin)){if(Hash(File.ReadAllBytes(plugin))==NativeComponent.Digest)File.Delete(plugin);else preserved=true;}if(removeLoader){if(OwnsLoader(root)){File.Delete(Path.Combine(root,FileName));File.Delete(Path.Combine(root,ReceiptName));}else if(File.Exists(Path.Combine(root,FileName)))preserved=true;}return preserved;}
 }
}

