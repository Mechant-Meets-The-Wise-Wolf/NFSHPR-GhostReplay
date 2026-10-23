using System;
using System.IO;
using System.Text;
namespace HprGhost {
 public static class GameLayout {
  public const string Profile="hpr-pc-x64-r1";
  public sealed class Anchor {
   public readonly string Name;public readonly long Rva;public readonly byte[] Bytes;
   public Anchor(string name,long rva,byte[] bytes){Name=name;Rva=rva;Bytes=bytes;}
  }
  public static readonly Anchor[] Anchors=new Anchor[]{
   new Anchor("车辆姿态",0x296bb0,new byte[]{0x48,0x89,0x5c,0x24,0x08,0x57,0x48,0x83,0xec,0x30,0x0f,0x29,0x74,0x24,0x20}),
   new Anchor("车辆资源池",0x1f4fb0,new byte[]{0x48,0x8b,0x41,0x08,0x48,0x8d,0x0d,0xc5,0x3a,0x09,0x01}),
   new Anchor("车型配置",0x228f20,new byte[]{0x48,0x8b,0x81,0x90,0x1e,0,0,0x4c,0x63,0x80,0x04,0x6c,0,0}),
   new Anchor("摄像机",0x29c077,new byte[]{0x48,0x8b,0x05,0x6a,0x04,0x00,0x01})
  };
  // EXE hash records provenance only. Runtime layout decides whether to attach.
  public static void Validate(string sourceHash,Func<long,int,byte[]> read){
   if(!HashValid(sourceHash))throw new InvalidDataException("无效的游戏文件标识。");
   foreach(Anchor a in Anchors){byte[] actual=read(a.Rva,a.Bytes.Length);if(actual==null||actual.Length!=a.Bytes.Length)throw new InvalidDataException("运行时代码读取不完整："+a.Name);
    for(int i=0;i<a.Bytes.Length;i++)if(actual[i]!=a.Bytes[i])throw new LayoutMismatchException("当前游戏布局尚未匹配，请等待主菜单后重试。");
   }
  }
  public static bool HashValid(string value){if(value==null||value.Length!=64)return false;foreach(char c in value)if(!((c>='0'&&c<='9')||(c>='a'&&c<='f')||(c>='A'&&c<='F')))return false;return true;}
  public static bool CanPlay(Replay replay){return replay!=null&&replay.Completed&&replay.EventId!=0&&HashValid(replay.GameHash);}

 }
}