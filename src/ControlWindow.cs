using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace HprGhost {
 public sealed partial class ControlWindow : Form {
  readonly ActivityCard recordCard=new ActivityCard("自动录制","正常发车，冲线后自动保存完整赛事",UiTheme.Red),playCard=new ActivityCard("幽灵回放","载入一份录像，进入对应赛事后自动对齐",UiTheme.Cyan);
  readonly Label brand,connection,libraryCount,notice,opacityLabel,fileDetail;
  readonly TextBox author=new TextBox();readonly TrackBar opacity=new TrackBar();readonly DataGridView files=new DataGridView();
  readonly ActionButton loadButton,renameButton,deleteButton,openFolderButton,languageButton,minimizeButton,closeButton;readonly ToolTip tips=new ToolTip();readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
  readonly VehicleLibrary vehicles=new VehicleLibrary("");
  readonly ReplayLibrary library;readonly IPlaybackRenderer playback;readonly RaceLifecycle lifecycle=new RaceLifecycle();readonly Dictionary<uint,string> events=new Dictionary<uint,string>();
  [DllImport("user32.dll")] static extern bool ReleaseCapture();
  [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h,int message,IntPtr w,IntPtr l);
  void DragHeader(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(Handle,0xa1,new IntPtr(2),IntPtr.Zero);}}
  GameReader reader;FramePump pump;Frame frame;RaceState race;Replay lastSaved;string loadedFile="",problem="",lastSetupStatus="";int notifiedInstalls;bool recording=false,playing=false,sampling;DateTime retryAt=DateTime.MinValue,lastSnapshot=DateTime.MinValue,lastUi=DateTime.MinValue;
#if DEVELOPMENT
  public string CapturePath;
#endif
  public ControlWindow():this(null){}
  internal ControlWindow(IPlaybackRenderer renderer){
   AutoScaleMode=AutoScaleMode.None;Icon=AppInfo.CreateIcon();Text=AppInfo.Name+" · "+UiText.Get(AppInfo.DisplayVersion);ClientSize=new Size(1120,838);BackColor=UiTheme.Background;ForeColor=UiTheme.Text;Font=UiTheme.Font(10,FontStyle.Regular);StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.None;MaximizeBox=false;DoubleBuffered=true;
   library=new ReplayLibrary(AppPaths.Recordings,vehicles.Name);playback=renderer??new NativePlayback(vehicles);
   var appMark=new PictureBox{Bounds=new Rectangle(34,27,32,32),Image=Icon.ToBitmap(),SizeMode=PictureBoxSizeMode.Normal,BackColor=Color.Transparent};Controls.Add(appMark);appMark.MouseDown+=DragHeader;brand=UiTheme.Label(this,"HPRGhostReplay",84,12,600,62,22,UiTheme.Text,FontStyle.Regular);brand.Font=new Font("Segoe UI",22,FontStyle.Regular);brand.TextAlign=ContentAlignment.MiddleLeft;brand.AutoEllipsis=false;
   connection=UiTheme.Label(this,"●  等待游戏启动",760,44,328,26,9,UiTheme.Amber,FontStyle.Bold);connection.TextAlign=ContentAlignment.MiddleRight;
   recordCard.SetBounds(32,90,516,282);playCard.SetBounds(572,90,516,282);Controls.Add(recordCard);Controls.Add(playCard);
   recordCard.Toggle.Click+=delegate{recording=!recording;if(!recording&&lifecycle.Capture!=null)lifecycle.Discard();RefreshStatus();};playCard.Toggle.Click+=delegate{playing=!playing;if(!playing)playback.Stop();RefreshStatus();};
   UiTheme.Label(recordCard,"记录人",255,249,65,24,8.5f,UiTheme.Muted,FontStyle.Regular);author.SetBounds(321,244,164,27);author.Text="LoughShinny302";author.BackColor=UiTheme.Inset;author.ForeColor=UiTheme.Text;author.BorderStyle=BorderStyle.FixedSingle;recordCard.Controls.Add(author);recordCard.Hint.SetBounds(20,246,216,25);
   opacityLabel=UiTheme.Label(playCard,"",20,249,210,24,8.5f,UiTheme.Muted,FontStyle.Regular);playCard.Hint.Visible=false;opacity.SetBounds(242,241,243,34);opacity.Minimum=10;opacity.Maximum=85;opacity.Value=45;UiText.Set(opacityLabel,"车影不透明度  {0}%",opacity.Value);opacity.TickStyle=TickStyle.None;opacity.BackColor=UiTheme.Surface;opacity.ValueChanged+=delegate{UiText.Set(opacityLabel,"车影不透明度  {0}%",opacity.Value);};playCard.Controls.Add(opacity);
   UiTheme.Label(this,"录像库",34,392,180,30,14,UiTheme.Text,FontStyle.Bold);libraryCount=UiTheme.Label(this,"",126,397,180,22,9,UiTheme.Muted,FontStyle.Regular);
   openFolderButton=Button("打开录像文件夹",500,388,168,34,OpenRecordings,false);tips.SetToolTip(openFolderButton,library.Folder);
   loadButton=Button("载入回放",680,388,128,34,Choose,true);Button("导入录像",820,388,128,34,Import,false);Button("刷新",960,388,128,34,delegate{RefreshFiles(null);},false);
   SetupGrid();files.SetBounds(32,435,1056,282);Controls.Add(files);
   fileDetail=UiTheme.Label(this,"选择录像，载入后会在对应赛事自动回放。",34,727,1052,22,8.5f,UiTheme.Muted,FontStyle.Regular);
   renameButton=Button("重命名",32,762,112,34,Rename,false);deleteButton=Button("删除录像",156,762,112,34,Delete,false);deleteButton.Accent=UiTheme.Red;
   Button("选择游戏",280,762,112,34,SelectGame,false);Button("回放组件",404,762,128,34,ManageComponents,false);notice=UiTheme.Label(this,"",548,764,540,34,9,UiTheme.Muted,FontStyle.Regular);notice.TextAlign=ContentAlignment.MiddleRight;
   var credit=new LinkLabel{Text="LoughShinny302 · B站空间",Bounds=new Rectangle(34,805,280,28),Font=UiTheme.Font(9,FontStyle.Regular),LinkColor=UiTheme.Cyan,ActiveLinkColor=UiTheme.Text,VisitedLinkColor=UiTheme.Cyan,LinkBehavior=LinkBehavior.HoverUnderline,TextAlign=ContentAlignment.MiddleLeft,UseMnemonic=false};UiText.Set(credit,"LoughShinny302 · B站空间");credit.LinkArea=new LinkArea(0,-1);credit.LinkClicked+=delegate {Guard(delegate {Process.Start(new ProcessStartInfo("https://space.bilibili.com/38767224"){UseShellExecute=true});});};Controls.Add(credit);tips.SetToolTip(credit,"https://space.bilibili.com/38767224");UiTheme.Label(this,"录制与回放可同时开启     ·     窗口 / 无边框模式",330,812,500,20,9,UiTheme.Muted,FontStyle.Regular);UiTheme.Label(this,AppInfo.DisplayVersion,840,811,248,20,9,UiTheme.Muted,FontStyle.Regular).TextAlign=ContentAlignment.MiddleRight;
   languageButton=Button("中文 / ENG",837,6,154,27,null,false);languageButton.Click+=delegate{UiText.English=!UiText.English;ApplyLanguage();};
   minimizeButton=Button("—",1007,6,34,27,delegate{WindowState=FormWindowState.Minimized;},false);closeButton=Button("×",1054,6,34,27,Close,false);foreach(Control c in Controls)if(c is Label&&c.Top<100)c.MouseDown+=DragHeader;MouseDown+=delegate(object sender,MouseEventArgs e){if(e.Y<100)DragHeader(sender,e);};
   LoadNames();lifecycle.Completed+=Saved;RefreshFiles(null);ApplyLanguage();
   timer.Interval=16;timer.Tick+=TimerTick;
#if DEVELOPMENT
   Shown+=delegate {if(renderer==null)Guard(PreparePlayback);timer.Start();if(!string.IsNullOrEmpty(CapturePath)){var shot=new System.Windows.Forms.Timer{Interval=1800};shot.Tick+=delegate{shot.Stop();using(var image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(0,0,Width,Height));image.Save(CapturePath,System.Drawing.Imaging.ImageFormat.Png);}shot.Dispose();};shot.Start();}};
#else
   Shown+=delegate {if(renderer==null)Guard(PreparePlayback);timer.Start();};
#endif
   FormClosed+=delegate {appMark.Image.Dispose();tips.Dispose();timer.Stop();playback.Dispose();if(pump!=null)pump.Dispose();if(reader!=null)reader.Dispose();lifecycle.Discard();};
  }
  ActionButton Button(string text,int x,int y,int w,int h,Action action,bool primary){var b=new ActionButton{Text=text,Bounds=new Rectangle(x,y,w,h),Primary=primary};UiText.Set(b,text);Controls.Add(b);if(action!=null)b.Click+=delegate{Guard(action);};return b;}
  void ApplyLanguage(){
   SuspendLayout();try{UiText.Apply(this);Text=AppInfo.Name+" · "+UiText.Get(AppInfo.DisplayVersion);libraryCount.Left=UiText.English?220:126;
    string[] headers={"录像","车辆","录像时长","记录人","录制时间"};for(int i=0;i<headers.Length;i++)files.Columns[i].HeaderText=UiText.Get(headers[i]);
    foreach(DataGridViewRow row in files.Rows){var item=row.Tag as ReplayChoice;if(item!=null)row.Cells[1].Value=UiText.Display(item.VehicleName);}
    minimizeButton.AccessibleName=UiText.Get("最小化");closeButton.AccessibleName=UiText.Get("关闭");languageButton.AccessibleName="中文 / English";tips.SetToolTip(languageButton,UiText.Get("当前：中文。点击切换为 English。"));
    RefreshStatus();UpdateSelection();
   }finally{ResumeLayout(true);Invalidate(true);}
  }
  void Guard(Action action){try{action();problem="";RefreshStatus();}catch(Exception ex){AppLog.Failure(ex);problem=ex.Message;UiText.Literal(notice,UiText.ErrorText(problem));notice.ForeColor=UiTheme.Amber;}}
  void LoadNames(){foreach(string s in AppPaths.NameLines("events.tsv")){int tab=s.IndexOf('\t');uint id;if(tab>0&&uint.TryParse(s.Substring(0,tab),out id))events[id]=s.Substring(tab+1);}}
  string EventName(uint id){string s;return events.TryGetValue(id,out s)?s:"未命名赛事";}
  void SetupGrid(){
   files.AllowUserToAddRows=false;files.AllowUserToDeleteRows=false;files.AllowUserToResizeRows=false;files.AllowUserToResizeColumns=false;files.ReadOnly=true;files.MultiSelect=false;files.SelectionMode=DataGridViewSelectionMode.FullRowSelect;files.RowHeadersVisible=false;files.BackgroundColor=UiTheme.Surface;files.BorderStyle=BorderStyle.None;files.GridColor=UiTheme.Border;files.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;files.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;files.EnableHeadersVisualStyles=false;files.ColumnHeadersHeight=32;files.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;files.RowTemplate.Height=40;
   files.ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle{BackColor=UiTheme.Surface,ForeColor=UiTheme.Muted,Font=UiTheme.Font(9,FontStyle.Regular),Padding=new Padding(14,0,0,0),SelectionBackColor=UiTheme.Surface};
   files.DefaultCellStyle=new DataGridViewCellStyle{BackColor=UiTheme.Inset,ForeColor=UiTheme.Text,SelectionBackColor=Color.FromArgb(62,62,62),SelectionForeColor=UiTheme.Text,Font=UiTheme.Font(9.5f,FontStyle.Regular),Padding=new Padding(14,0,0,0),WrapMode=DataGridViewTriState.False};
   files.Columns.Add("name","录像");files.Columns[0].Width=300;files.Columns.Add("vehicle","车辆");files.Columns[1].Width=290;files.Columns.Add("time","录像时长");files.Columns[2].Width=130;files.Columns[2].DefaultCellStyle.Font=new Font("Segoe UI",11,FontStyle.Regular);files.Columns[2].DefaultCellStyle.ForeColor=UiTheme.Text;files.Columns.Add("author","记录人");files.Columns[3].Width=150;files.Columns.Add("date","录制时间");files.Columns[4].AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill;foreach(DataGridViewColumn c in files.Columns)c.SortMode=DataGridViewColumnSortMode.NotSortable;
   files.SelectionChanged+=delegate {UpdateSelection();};files.CellDoubleClick+=delegate(object sender,DataGridViewCellEventArgs e){if(e.RowIndex>=0)Guard(Choose);};
  }
  ReplayChoice Selected(){return files.SelectedRows.Count==0?null:files.SelectedRows[0].Tag as ReplayChoice;}
  void UpdateSelection(){ReplayChoice item=Selected();if(loadButton!=null){loadButton.Enabled=renameButton.Enabled=deleteButton.Enabled=item!=null;}if(fileDetail!=null){if(item==null)UiText.Set(fileDetail,"选择录像，载入后会在对应赛事自动回放。");else UiText.Literal(fileDetail,item.DisplayName);}}
  void RefreshFiles(string selected){if(selected==null){var prev=Selected();selected=prev==null?null:prev.File;}files.Rows.Clear();foreach(var item in library.Read()){DateTime created;string date=DateTime.TryParse(item.Replay.CreatedUtc,null,DateTimeStyles.RoundtripKind,out created)?created.ToLocalTime().ToString("MM-dd  HH:mm"):"—";string main=item.HasAutomaticName?item.Replay.EventLabel:item.DisplayName;int i=files.Rows.Add((string.Equals(item.File,loadedFile,StringComparison.OrdinalIgnoreCase)?"▶  ":"")+main,UiText.Display(item.VehicleName),RaceTime.Format(item.Replay.Duration),item.Replay.Author,date);files.Rows[i].Tag=item;if(string.Equals(item.File,selected,StringComparison.OrdinalIgnoreCase)){files.CurrentCell=files.Rows[i].Cells[0];files.Rows[i].Selected=true;}}UiText.Set(libraryCount,"{0} 份录像",files.Rows.Count);UpdateSelection();}
  void OpenRecordings(){Directory.CreateDirectory(library.Folder);Process.Start(new ProcessStartInfo(library.Folder){UseShellExecute=true});}
  void PreparePlayback(){string root=GameLocation.Resolve();if(!GameLocation.Valid(root)){MessageBox.Show(this,UiText.Get("未找到游戏目录，请选择 NFS11Remastered.exe 确认位置。"),AppInfo.Name,MessageBoxButtons.OK,MessageBoxIcon.Information);SelectGame();return;}vehicles.SetGameRoot(root);playback.SetGameRoot(root);if(!string.Equals(AppPaths.SavedGameFolder,root,StringComparison.OrdinalIgnoreCase))AppPaths.SaveGameFolder(root);}
  void ManageComponents(){string root=GameLocation.Resolve();if(!GameLocation.Valid(root)){SelectGame();root=GameLocation.Resolve();if(!GameLocation.Valid(root))return;}using(var dialog=new ComponentDialog(AsiLoader.OwnsLoader(root),NativeComponent.GameRunning())){if(dialog.ShowDialog(this)!=DialogResult.OK)return;bool preserved=AsiLoader.Uninstall(root,dialog.RemoveLoader,NativeComponent.GameRunning());var native=playback as NativePlayback;if(native!=null)native.Unload();else playback.Stop();playing=false;lifecycle.Selected=null;loadedFile="";RefreshFiles(null);UiText.Set(notice,preserved?"已卸载可识别组件；已变更的文件已保留。":"回放组件已卸载，录像和配置已保留。");notice.ForeColor=UiTheme.Muted;}}
  void SelectGame(){using(var dialog=new OpenFileDialog{Title=UiText.Get("选择游戏目录中的 NFS11Remastered.exe"),Filter=UiText.Get("游戏主程序|NFS11Remastered.exe"),CheckFileExists=true})if(dialog.ShowDialog(this)==DialogResult.OK){string root=Path.GetDirectoryName(dialog.FileName);AppPaths.SaveGameFolder(root);vehicles.SetGameRoot(root);playback.SetGameRoot(root);UiText.Set(notice,"已记住游戏位置。录像仍保存在软件的 Recordings 文件夹。");notice.ForeColor=UiTheme.Muted;}}
  void Choose(){var item=Selected();if(item==null)throw new InvalidOperationException("请先选择一份录像。");if(item.Replay.VehicleResourceId==0)throw new InvalidDataException("这份旧录像缺少车型信息，请使用包含车型的新录像。");string root=reader==null?GameLocation.Resolve():Path.GetDirectoryName(reader.Process.MainModule.FileName);if(!GameLocation.Valid(root)){SelectGame();root=GameLocation.Resolve();if(!GameLocation.Valid(root))return;}vehicles.SetGameRoot(root);playback.SetGameRoot(root);playback.Load(item.Replay);lifecycle.Selected=item.Replay;loadedFile=item.File;playing=true;RefreshFiles(loadedFile);UiText.Set(notice,"已载入。进入对应赛事后自动回放。");notice.ForeColor=UiTheme.Cyan;}
  void Import(){using(var dialog=new OpenFileDialog{Title=UiText.Get("导入幽灵录像"),Filter=UiText.Get("幽灵录像|*.hprghost"),InitialDirectory=library.Folder})if(dialog.ShowDialog(this)==DialogResult.OK){string f=library.Import(dialog.FileName);RefreshFiles(f);UiText.Set(notice,"已导入录像库，选中后点击“载入回放”。");notice.ForeColor=UiTheme.Muted;}}
  void Rename(){var item=Selected();if(item==null)return;using(var d=new NameDialog(item.DisplayName))if(d.ShowDialog(this)==DialogResult.OK){bool loaded=string.Equals(loadedFile,item.File,StringComparison.OrdinalIgnoreCase);string target=library.Rename(item.File,d.Value);if(loaded)loadedFile=target;RefreshFiles(target);UiText.Set(notice,"录像已重命名，轨迹与成绩保持不变。");notice.ForeColor=UiTheme.Muted;}}
  void Delete(){var item=Selected();if(item==null)return;using(var dialog=new DeleteDialog(item.DisplayName))if(dialog.ShowDialog(this)!=DialogResult.OK)return;bool loaded=string.Equals(loadedFile,item.File,StringComparison.OrdinalIgnoreCase);library.Delete(item.File);if(loaded){lifecycle.Selected=null;loadedFile="";playing=false;playback.Stop();}RefreshFiles(null);UiText.Set(notice,"录像已删除。");notice.ForeColor=UiTheme.Muted;}
  void Saved(Replay replay){string f=library.Save(replay);lastSaved=replay;RefreshFiles(f);UiText.Set(notice,"整场录像已保存。");notice.ForeColor=UiTheme.Lime;AppLog.Write(LogCode.RECORDING_SAVED);}
  void RefreshStatus(){
   bool connected=reader!=null;UiText.Literal(connection,UiText.Get(connected?"●  已连接游戏":"●  等待游戏启动")+(lifecycle.Selected==null?UiText.Get(" · 未导入"):""));connection.ForeColor=connected?UiTheme.Lime:UiTheme.Amber;
   var nativeSetup=playback as NativePlayback;if(nativeSetup!=null&&nativeSetup.CompletedInstalls>notifiedInstalls){notifiedInstalls=nativeSetup.CompletedInstalls;MessageBox.Show(this,UiText.Get("已经在游戏目录安装必备插件。")+"\r\n\r\n"+UiText.Get("需要删除时，请点击下方“回放组件”进行卸载。")+(nativeSetup.RestartRequired?"\r\n\r\n"+UiText.Get("游戏正在运行，请重启游戏后使用。"):""),AppInfo.Name,MessageBoxButtons.OK,MessageBoxIcon.Information);}if(nativeSetup!=null&&lifecycle.Selected==null){string setup=nativeSetup.SetupStatus;if(setup!=lastSetupStatus){lastSetupStatus=setup;UiText.Set(notice,setup);notice.ForeColor=setup=="回放组件已就绪。"?UiTheme.Muted:UiTheme.Amber;}}
   bool active=race!=null&&race.Active,running=active&&race.Running,rec=lifecycle.Capture!=null&&recording;Replay selected=lifecycle.Selected;bool match=selected!=null&&active&&race.EventId==selected.EventId;bool play=playing&&match&&running&&frame!=null&&playback.Ready&&playback.IsDrawing&&selected.At(race.RaceTicks*1e-6)!=null;
   UiText.Set(recordCard.Toggle,recording?"关闭录制":"开启录制");recordCard.Toggle.Primary=false;recordCard.Toggle.Accent=UiTheme.Red;recordCard.Toggle.Invalidate();
   string rs=!recording?"录制已关闭":rec?"正在录制":!connected?"等待游戏连接":lastSaved!=null&&active&&race.Finished?"已保存完整录像":running?"等待下次发车":"已开启 · 等待发车";
   if(recordCard.IsRunning!=rec)AppLog.Write(rec?LogCode.RECORDING_STARTED:LogCode.RECORDING_STOPPED);recordCard.SetState(rs,rec,recording,recording?UiTheme.Red:UiTheme.Muted,0);UiText.Set(recordCard.Caption,lastSaved!=null&&active&&race.Finished?"已录时长":"录制用时");recordCard.Clock.Text=RaceTime.Format(rec?lifecycle.Capture.Replay.Duration:lastSaved!=null&&active&&race.Finished?lastSaved.Duration:0);
   if(rec||active)UiText.Literal(recordCard.EventLabel,rec?lifecycle.Capture.Replay.EventLabel:events.ContainsKey(race.EventId)?EventName(race.EventId):UiText.Get("未命名赛事"));else UiText.Set(recordCard.EventLabel,"进入任意赛事后自动录制");
   uint car=frame==null?0:frame.VehicleResourceId;if(car!=0)UiText.Literal(recordCard.Detail,UiText.Display(vehicles.Name(car)));else UiText.Set(recordCard.Detail,"自动识别当前车辆");UiText.Set(recordCard.Hint,rec?"●  冲线后自动保存":"重开会丢弃未完成录制");
   UiText.Set(playCard.Toggle,selected==null?"未导入":playing?"关闭回放":"开启回放");playCard.Toggle.Enabled=selected!=null;opacity.Enabled=selected!=null;playCard.Toggle.Primary=false;playCard.Toggle.Accent=UiTheme.Cyan;playCard.Toggle.Invalidate();
   string ps=selected==null?"未导入":!playing?"回放已关闭":play?"正在回放":!connected?"已就绪 · 等待游戏":!playback.Ready?"等待回放组件就绪":running&&match?"回放待机 · 返回游戏后继续":active&&race.Finished&&match?"本场回放已结束":running&&!match?"等待进入对应赛事":"已就绪 · 等待发车";
   double at=match&&playing?Math.Min(race.LiveTicks*1e-6,selected.Duration):0;if(playCard.IsRunning!=play)AppLog.Write(play?LogCode.REPLAY_STARTED:LogCode.REPLAY_STOPPED);playCard.SetState(ps,play,playing&&selected!=null,play?UiTheme.Cyan:playing&&selected!=null?UiTheme.Cyan:UiTheme.Muted,selected==null?0:at/Math.Max(.001,selected.Duration));opacity.BackColor=playCard.SurfaceColor;UiText.Set(playCard.Caption,"回放进度");playCard.Clock.Text=RaceTime.Format(at);
   if(selected==null){UiText.Set(playCard.EventLabel,"尚未载入录像");UiText.Set(playCard.Detail,"在录像库选中记录，然后点击“载入回放”");}else{UiText.Literal(playCard.EventLabel,selected.EventLabel);UiText.Set(playCard.Detail,"{0}  ·  时长 {1}",UiText.Display(vehicles.Name(selected.VehicleResourceId)),RaceTime.Format(selected.Duration));}if(selected!=null&&playing&&!playback.Ready){UiText.Set(notice,playback.Status);notice.ForeColor=UiTheme.Amber;}if(problem!=""){UiText.Literal(notice,UiText.ErrorText(problem));notice.ForeColor=UiTheme.Amber;}
  }
  void TimerTick(object sender,EventArgs args){Tick();}
  void Tick(){if(sampling)return;sampling=true;try{playback.Update(playing,opacity.Value,reader==null?0:reader.Process.Id);
   if(reader==null){if(DateTime.UtcNow<retryAt)return;reader=new GameReader();vehicles.SetGameRoot(Path.GetDirectoryName(reader.Process.MainModule.FileName));playback.SetGameRoot(Path.GetDirectoryName(reader.Process.MainModule.FileName));pump=new FramePump(reader);try{string root=Path.GetDirectoryName(reader.Process.MainModule.FileName);if(!string.Equals(AppPaths.SavedGameFolder,root,StringComparison.OrdinalIgnoreCase))AppPaths.SaveGameFolder(root);}catch(IOException){}catch(UnauthorizedAccessException){}}
   lifecycle.RecordEnabled=recording;lifecycle.Author=string.IsNullOrWhiteSpace(author.Text)?"LoughShinny302":author.Text.Trim();if(!recording&&lifecycle.Capture!=null)lifecycle.Discard();
   var batch=pump.Take();if(batch.Count==0){if(reader.Process.HasExited)throw new IOException("游戏已退出。");if(DateTime.UtcNow-lastSnapshot>TimeSpan.FromSeconds(1)&&pump.Error!="")problem=pump.Error;}
   else {foreach(var s in batch){lifecycle.Observe(s.Race,s.Frame,EventName(s.Race.EventId),reader.Hash);race=s.Race;frame=s.Frame;}lastSnapshot=DateTime.UtcNow;problem="";}
   if(DateTime.UtcNow-lastUi>TimeSpan.FromMilliseconds(80)){lastUi=DateTime.UtcNow;RefreshStatus();}
  }catch(Exception ex){playback.Stop();problem=ex.Message;bool gameExited=reader!=null&&reader.Process.HasExited;if(gameExited){if(pump!=null){pump.Dispose();pump=null;}reader.Dispose();reader=null;frame=null;race=null;lifecycle.Discard();AppLog.Write(LogCode.GAME_DISCONNECTED);}if(reader==null)retryAt=DateTime.UtcNow.AddSeconds(3);RefreshStatus();if(!gameExited)AppLog.Failure(ex);}finally{sampling=false;}}
 }
}




