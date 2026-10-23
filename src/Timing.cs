using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
namespace HprGhost {
 public static class FrameConsistency {
  public static bool Stable(long before,long inside,long after,long raceBefore,long raceAfter,float[] cameraBefore,float[] cameraAfter){
   if(before!=inside||inside!=after||raceBefore!=raceAfter||cameraBefore==null||cameraAfter==null||cameraBefore.Length!=16||cameraAfter.Length!=16)return false;
   for(int i=0;i<16;i++)if(!MathUtil.Finite(cameraBefore[i])||cameraBefore[i]!=cameraAfter[i])return false;return true;
  }
 }
}
