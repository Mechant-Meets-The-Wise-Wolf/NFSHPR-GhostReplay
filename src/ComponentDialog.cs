using System;
using System.Drawing;
using System.Windows.Forms;
namespace HprGhost {
 public sealed class ComponentDialog:Form {
  readonly CheckBox loader=new CheckBox();public bool RemoveLoader{get{return loader.Checked;}}
  public ComponentDialog(bool owned,bool running){Icon=AppInfo.CreateIcon();Text=UiText.Get("回放组件");ClientSize=new Size(660,300);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;BackColor=UiTheme.Background;ForeColor=UiTheme.Text;Font=UiTheme.Font(9,FontStyle.Regular);
   UiTheme.Label(this,"卸载原生回放组件",24,20,610,32,14,UiTheme.Text,FontStyle.Bold);
   UiTheme.Label(this,"HPRGhostNative.asi  ·  幽灵绘制插件",24,66,610,28,10,UiTheme.Text,FontStyle.Regular);
   UiTheme.Label(this,"卸载后暂停自动安装，录像和配置保留；载入录像可恢复。",24,104,610,40,9,UiTheme.Muted,FontStyle.Regular);
   loader.SetBounds(24,150,610,44);loader.Text=UiText.Get("同时移除本软件安装的加载器（其他 ASI 插件可能依赖它）");loader.Enabled=owned;loader.Checked=false;Controls.Add(loader);
   UiTheme.Label(this,running?"请先退出游戏，再卸载回放组件。":owned?"加载器：本软件安装，默认保留。":"加载器：已有或已变更，卸载时保留。",24,201,610,30,9,UiTheme.Amber,FontStyle.Regular);
   var cancel=new ActionButton{Text=UiText.Get("取消"),Bounds=new Rectangle(378,246,112,34),DialogResult=DialogResult.Cancel};var remove=new ActionButton{Text=UiText.Get("卸载回放"),Bounds=new Rectangle(502,246,132,34),DialogResult=DialogResult.OK,Enabled=!running};Controls.Add(cancel);Controls.Add(remove);AcceptButton=cancel;CancelButton=cancel;
  }
 }
}

