using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
namespace HprGhost {
 public sealed partial class ControlWindow {
  void SaveLanguagePreview(string folder,string name){UiText.Literal(connection,UiText.Get("界面预览（模拟状态）"));using(var shot=new Bitmap(Width,Height)){DrawToBitmap(shot,new Rectangle(0,0,Width,Height));shot.Save(Path.Combine(folder,name),System.Drawing.Imaging.ImageFormat.Png);}}
  void CheckLanguages(Action<bool,string> check,string folder){
   check(!UiText.English&&languageButton.Text=="中文 / ENG"&&languageButton.Top<recordCard.Top,"Chinese is the default and the language switch is in the header");
   var capture=lifecycle.Capture;var selected=lifecycle.Selected;int count=capture.Replay.Samples.Count;string clock=playCard.Clock.Text;float x=frame.Pose.Position.X;string originalName=ReplayLibrary.DefaultName(selected,vehicles.Name(selected.VehicleResourceId));
   var choice=new ReplayChoice{File=Path.Combine(library.Folder,"SUN, SAND AND SUPERCARS {SL65}.hprghost"),Replay=selected,VehicleName=vehicles.Name(selected.VehicleResourceId)};
   int row=files.Rows.Add("SUN, SAND AND SUPERCARS",choice.VehicleName,"03:20.17","Player","10-16  16:40");files.Rows[row].Tag=choice;files.CurrentCell=files.Rows[row].Cells[0];files.Rows[row].Selected=true;UiText.Set(libraryCount,"{0} 份录像",files.Rows.Count);UpdateSelection();
   UiText.Set(notice,"已载入。进入对应赛事后自动回放。");languageButton.PerformClick();
   check(UiText.English&&recordCard.State.Text=="Recording"&&playCard.State.Text=="Replaying"&&recordCard.Toggle.Text=="Stop recording"&&playCard.Toggle.Text=="Stop replay","header switch updates simultaneous active module states and actions");
   check(object.ReferenceEquals(capture,lifecycle.Capture)&&object.ReferenceEquals(selected,lifecycle.Selected)&&capture.Replay.Samples.Count==count&&recording&&playing&&playCard.Clock.Text==clock&&frame.Pose.Position.X==x,"switching language preserves the active recording, selected replay, clocks and vehicle pose");
   check(files.Columns[0].HeaderText=="Replay"&&files.Columns[2].HeaderText=="Duration"&&Selected()==choice&&fileDetail.Text==choice.Name&&notice.Text=="Loaded. Replay starts in the matching event.","switch preserves library selection and literal braces in names while translating headers and feedback");
   opacity.Value=50;check(opacityLabel.Text=="Ghost opacity  50%","dynamic opacity follows the current language");opacity.Value=45;
   check(originalName==ReplayLibrary.DefaultName(selected,vehicles.Name(selected.VehicleResourceId))&&author.Text=="LoughShinny302","language never changes default file naming or the recording author");
   using(var rename=new NameDialog("刷新 {SSAS}"))using(var remove=new DeleteDialog(choice.Name)){
    check(rename.Text=="Rename replay"&&rename.Value=="刷新 {SSAS}"&&((Control)rename.AcceptButton).Text=="Save name","English rename dialog preserves arbitrary Unicode input and braces");
    check(remove.Text=="Delete replay"&&((Control)remove.AcceptButton).Text=="Cancel"&&remove.CancelButton==remove.AcceptButton,"English delete confirmation defaults to Cancel without touching a file");
   }
   SaveLanguagePreview(folder,"ui-english-active.png");
   playing=false;RefreshStatus();check(playCard.State.Text=="Replay off"&&recordCard.State.Text=="Recording","English off state remains independent of recording");playing=true;
   lifecycle.Selected=null;RefreshStatus();check(playCard.State.Text=="No replay"&&!playCard.Toggle.Enabled&&connection.Text.Contains("No replay loaded"),"English unloaded status is explicit in the card and header");lifecycle.Selected=selected;RefreshStatus();
   problem="已经有同名录像，请换一个名称。";RefreshStatus();check(notice.Text=="A replay with this name already exists. Choose another name.","application errors are translated while preserving the underlying diagnostic key");
   languageButton.PerformClick();check(!UiText.English&&problem!=""&&notice.Text==problem&&recordCard.State.Text=="正在录制"&&playCard.State.Text=="正在回放"&&files.Columns[2].HeaderText=="录像时长"&&fileDetail.Text==choice.Name,"switching back restores Chinese including an existing error, without clearing state or file names");
   problem="";UiText.Set(notice,"已载入。进入对应赛事后自动回放。");SaveLanguagePreview(folder,"ui-chinese-active.png");
   using(var rename=new NameDialog("我的录像"))using(var remove=new DeleteDialog(choice.Name))check(rename.Text=="重命名录像"&&remove.Text=="删除录像"&&((Control)remove.AcceptButton).Text=="取消","Chinese dialogs remain the primary default");
   files.Rows.RemoveAt(row);UiText.Set(libraryCount,"{0} 份录像",files.Rows.Count);UpdateSelection();RefreshStatus();
  }
 }
}
