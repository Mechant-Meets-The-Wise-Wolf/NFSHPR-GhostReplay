using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace HprGhost {
 public static class UiTheme {
  public static readonly Color Background=Color.FromArgb(32,32,32),Surface=Color.FromArgb(43,43,43),Inset=Color.FromArgb(36,36,36),Border=Color.FromArgb(63,63,63),Text=Color.FromArgb(242,242,242),Muted=Color.FromArgb(177,177,177),Red=Color.FromArgb(231,112,119),Cyan=Color.FromArgb(96,205,255),Lime=Color.FromArgb(137,195,147),Amber=Color.FromArgb(216,183,126);
  public static Font Font(float size,FontStyle style){return new Font("Microsoft YaHei UI",size,style,GraphicsUnit.Point);}
  public static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
  public static Label Label(Control parent,string text,int x,int y,int w,int h,float size,Color color,FontStyle style){var l=new Label{Text=text,Bounds=new Rectangle(x,y,w,h),Font=Font(size,style),ForeColor=color,BackColor=Color.Transparent,AutoEllipsis=true,UseMnemonic=false};UiText.Set(l,text);parent.Controls.Add(l);return l;}
 }
 public class SurfacePanel : Panel {
  public Color SurfaceColor {get;protected set;} public Color EdgeColor {get;protected set;}
  public SurfacePanel(){DoubleBuffered=true;BackColor=UiTheme.Background;SurfaceColor=UiTheme.Surface;EdgeColor=UiTheme.Border;}
  protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(UiTheme.Background);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var p=UiTheme.Round(new RectangleF(0,0,Width-1,Height-1),8))using(var b=new SolidBrush(SurfaceColor))using(var pen=new Pen(EdgeColor)){e.Graphics.FillPath(b,p);e.Graphics.DrawPath(pen,p);}}
 }
 // Paint the entire surface; no native Button renderer or theme can leave
 // accent-coloured corners/default borders underneath a rounded shape.
 public sealed class ActionButton : Control,IButtonControl {
  public Color Accent=UiTheme.Cyan;public bool Primary;bool hover,pressed;
  public DialogResult DialogResult {get;set;}
  public ActionButton(){Font=UiTheme.Font(9.5f,FontStyle.Regular);Cursor=Cursors.Hand;TabStop=true;AccessibleRole=AccessibleRole.PushButton;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable|ControlStyles.StandardClick,true);}
  public void NotifyDefault(bool value){Invalidate();}
  public void PerformClick(){if(Enabled)OnClick(EventArgs.Empty);}
  protected override void OnClick(EventArgs e){base.OnClick(e);var form=FindForm();if(form!=null&&DialogResult!=DialogResult.None)form.DialogResult=DialogResult;}
  protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hover=pressed=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left){pressed=true;Focus();Invalidate();}base.OnMouseDown(e);}
  protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
  protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space){pressed=true;e.Handled=true;Invalidate();}else if(e.KeyCode==Keys.Enter){PerformClick();e.Handled=true;}base.OnKeyDown(e);}
  protected override void OnKeyUp(KeyEventArgs e){if(e.KeyCode==Keys.Space){pressed=false;PerformClick();e.Handled=true;Invalidate();}base.OnKeyUp(e);}
  protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
  protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}protected override void OnLostFocus(EventArgs e){pressed=false;Invalidate();base.OnLostFocus(e);}
  protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent is SurfacePanel?((SurfacePanel)Parent).SurfaceColor:Parent==null?UiTheme.Background:Parent.BackColor);}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Color fill=Primary&&Enabled?Accent:!Enabled?Color.FromArgb(48,48,48):pressed?Color.FromArgb(68,68,68):hover?Color.FromArgb(78,78,78):Color.FromArgb(61,61,61);Color ink=!Enabled?Color.FromArgb(130,130,130):Primary?Color.FromArgb(24,24,24):UiTheme.Text;using(var shape=UiTheme.Round(new RectangleF(.5f,.5f,Width-1,Height-1),4))using(var brush=new SolidBrush(fill))using(var pen=new Pen(Primary&&Enabled?fill:Color.FromArgb(82,82,82))){e.Graphics.FillPath(brush,shape);e.Graphics.DrawPath(pen,shape);}TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(4,4,Width-8,Height-8),UiTheme.Text,fill);}
 }
 public sealed class ActivityCard : SurfacePanel {
  public readonly Label State,Clock,EventLabel,Detail,Hint,Caption;public readonly ActionButton Toggle;public readonly Color Accent;
  bool active,lit;double progress;public bool IsRunning {get{return active;}}public bool IsLit{get{return lit;}}
  public ActivityCard(string title,string subtitle,Color accent){Accent=accent;UiTheme.Label(this,title,20,16,280,30,13,UiTheme.Text,FontStyle.Bold);UiTheme.Label(this,subtitle,20,51,465,24,9,UiTheme.Muted,FontStyle.Regular);Toggle=new ActionButton{Bounds=new Rectangle(357,16,128,32),Accent=accent,Font=UiTheme.Font(9,FontStyle.Bold)};Controls.Add(Toggle);State=UiTheme.Label(this,"等待设置",38,86,268,28,10,accent,FontStyle.Bold);Caption=UiTheme.Label(this,"",322,89,163,25,9,UiTheme.Muted,FontStyle.Regular);Caption.TextAlign=ContentAlignment.MiddleRight;Clock=UiTheme.Label(this,"00:00.00",20,121,460,49,27,UiTheme.Text,FontStyle.Bold);Clock.Font=new Font("Segoe UI",26,FontStyle.Regular);EventLabel=UiTheme.Label(this,"进入赛事后自动开始",20,178,465,27,10,UiTheme.Text,FontStyle.Bold);Detail=UiTheme.Label(this,"",20,209,465,24,9,UiTheme.Muted,FontStyle.Regular);Hint=UiTheme.Label(this,"",20,246,220,25,8.5f,UiTheme.Muted,FontStyle.Regular);}
  public void SetState(string text,bool running,bool enabled,Color color,double fraction){UiText.Set(State,text);State.ForeColor=color;bool changed=active!=running||lit!=enabled;active=running;lit=enabled;SurfaceColor=lit?Color.FromArgb(65,65,65):UiTheme.Surface;EdgeColor=lit?Color.FromArgb(108,108,108):UiTheme.Border;progress=Math.Max(0,Math.Min(1,fraction));if(changed)Invalidate(true);else {Invalidate(new Rectangle(18,86,15,28));Invalidate(new Rectangle(20,234,465,4));}}
  public void Pulse(){}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Color c=State.ForeColor;float a=active?1:.65f;using(var b=new SolidBrush(Color.FromArgb((int)(a*255),c)))e.Graphics.FillEllipse(b,20,96,6,6);using(var b=new SolidBrush(UiTheme.Border))e.Graphics.FillRectangle(b,20,234,465,3);if(progress>0)using(var b=new SolidBrush(c))e.Graphics.FillRectangle(b,20,234,(float)(465*progress),3);}
 }
 public sealed class DeleteDialog : Form {
  public DeleteDialog(string name){Icon=AppInfo.CreateIcon();Text=UiText.Get("删除录像");ClientSize=new Size(580,240);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;BackColor=UiTheme.Background;ForeColor=UiTheme.Text;Font=UiTheme.Font(10,FontStyle.Regular);
   UiTheme.Label(this,"删除这份录像？",24,20,530,30,14,UiTheme.Text,FontStyle.Bold);
   var filename=UiTheme.Label(this,"",24,65,530,46,10,UiTheme.Text,FontStyle.Regular);UiText.Literal(filename,name);
   UiTheme.Label(this,"文件将从录像库中移除。",24,121,530,28,9,UiTheme.Muted,FontStyle.Regular);
   var remove=new ActionButton{Text=UiText.Get("确认删除"),Bounds=new Rectangle(398,177,156,40),DialogResult=DialogResult.OK,Accent=UiTheme.Red};var cancel=new ActionButton{Text=UiText.Get("取消"),Bounds=new Rectangle(286,177,100,40),DialogResult=DialogResult.Cancel};Controls.Add(remove);Controls.Add(cancel);AcceptButton=cancel;CancelButton=cancel;Shown+=delegate{cancel.Focus();};
  }
 }
 public sealed class NameDialog : Form {
  readonly TextBox input=new TextBox();public string Value {get{return input.Text;}}
  public NameDialog(string value){Icon=AppInfo.CreateIcon();Text=UiText.Get("重命名录像");ClientSize=new Size(580,200);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;BackColor=UiTheme.Background;ForeColor=UiTheme.Text;Font=UiTheme.Font(10,FontStyle.Regular);UiTheme.Label(this,"为这份录像取个名字",24,20,520,30,14,UiTheme.Text,FontStyle.Bold);input.SetBounds(24,68,530,32);input.BackColor=UiTheme.Surface;input.ForeColor=UiTheme.Text;input.BorderStyle=BorderStyle.FixedSingle;input.Text=value;Controls.Add(input);var save=new ActionButton{Text=UiText.Get("保存名称"),Bounds=new Rectangle(398,133,156,40),Primary=true,DialogResult=DialogResult.OK};var cancel=new ActionButton{Text=UiText.Get("取消"),Bounds=new Rectangle(286,133,100,40),DialogResult=DialogResult.Cancel};Controls.Add(save);Controls.Add(cancel);AcceptButton=save;CancelButton=cancel;Shown+=delegate{input.Focus();input.SelectAll();};}
 }
}

