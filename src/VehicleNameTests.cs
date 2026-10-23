using System;
using System.IO;
namespace HprGhost {
 public static class VehicleNameTests {
  public static void Run(Action<bool,string> check){
   var cars=new VehicleLibrary("");int count=0;bool complete=true;foreach(string line in AppPaths.NameLines("vehicles.tsv")){int tab=line.IndexOf('\t');uint id;if(tab<=0||!uint.TryParse(line.Substring(0,tab),out id))continue;count++;string name=cars.Name(id);if(name.StartsWith("Vehicle ")||name.StartsWith("车型 ")||name=="未知车型"||name=="未记录车型")complete=false;}
   check(count==132&&complete,"all 132 embedded vehicle names are present without numeric placeholders");
   check(cars.Name(118492)=="Koenigsegg CCX"&&cars.Name(380028)=="Audi TT RS"&&cars.Name(292004)=="BMW M3 E92"&&cars.Name(383198)=="Jaguar XKR","previously missing short vehicle names resolve to verified models");
   check(cars.Name(172670)=="Mazda RX-8"&&cars.Name(172460)=="McLaren F1"&&cars.Name(172586)=="McLaren MP4-12C"&&cars.Name(1111341)=="Porsche 959","adjacent initializer strings cannot mislabel Mazda McLaren or Porsche");
   check(cars.Name(0)=="未记录车型"&&cars.Name(uint.MaxValue)=="未知车型","unknown vehicle names never expose resource IDs or guess another model");
   bool prior=UiText.English;try{UiText.English=true;check(UiText.Display(cars.Name(uint.MaxValue))=="Unknown vehicle"&&UiText.Display(cars.Name(0))=="No vehicle data","unknown vehicle fallback is localized in English");}finally{UiText.English=prior;}
   var replay=new Replay{EventLabel="[ONLINE] FAST TRACK",Author="LoughShinny302",VehicleResourceId=118492};replay.Samples.Add(new Sample{Time=0});replay.Samples.Add(new Sample{Time=117.29});
   string old=ReplayLibrary.DefaultName(replay,"Vehicle 118492");var item=new ReplayChoice{File=Path.Combine("unwritten-library",old+".hprghost"),Replay=replay,VehicleName=cars.Name(118492)};
   string original=item.File;check(item.HasAutomaticName&&item.DisplayName.Contains("Koenigsegg CCX")&&!item.DisplayName.Contains("118492")&&item.File==original,"legacy automatic placeholder names display the correct vehicle without renaming the source");
   item.File=Path.Combine("unwritten-library",old+"-2.hprghost");check(item.HasAutomaticName&&item.DisplayName.EndsWith("-2")&&!item.DisplayName.Contains("118492"),"legacy duplicate automatic names retain their suffix without exposing the placeholder");
   item.File=Path.Combine("unwritten-library","我的 Vehicle 118492 {个人命名}.hprghost");check(!item.HasAutomaticName&&item.DisplayName==item.Name,"custom recording titles remain literal even if they contain an ID or braces");
  }
 }
}
