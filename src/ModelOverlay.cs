using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace HprGhost {
 public sealed class ModelOverlay : Window {
  readonly Viewport3D viewport=new Viewport3D();readonly ModelVisual3D car=new ModelVisual3D();readonly MatrixCamera camera=new MatrixCamera();
  readonly MatrixTransform3D pose=new MatrixTransform3D();IntPtr window;int lastX=int.MinValue,lastY,lastWidth,lastHeight;uint modelId;int modelRevision=-1;public VehicleLibrary Library;
  [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetStyle(IntPtr h,int index);
  [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] static extern IntPtr SetStyle(IntPtr h,int index,IntPtr value);
  public ModelOverlay(VehicleLibrary library){Library=library;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;Topmost=true;Focusable=false;IsHitTestVisible=false;Content=viewport;viewport.Camera=camera;viewport.Opacity=.45;
   viewport.Children.Add(new ModelVisual3D{Content=Lights()});viewport.Children.Add(car);car.Transform=pose;
   SourceInitialized+=delegate{window=new WindowInteropHelper(this).Handle;SetStyle(window,-20,new IntPtr(GetStyle(window,-20).ToInt64()|0x20|0x08000000|0x80));};
  }
  public static Model3DGroup Lights(){var l=new Model3DGroup();l.Children.Add(new AmbientLight(Color.FromRgb(100,100,100)));l.Children.Add(new DirectionalLight(Color.FromRgb(205,205,205),new Vector3D(-.5,-1,-.3)));l.Children.Add(new DirectionalLight(Color.FromRgb(75,90,100),new Vector3D(.7,-.3,1)));l.Freeze();return l;}
  public void Prepare(uint id){if(modelId==id&&modelRevision==Library.Revision)return;VehicleModel mesh=Library.Load(id);car.Content=mesh.Geometry;modelId=id;modelRevision=Library.Revision;}
  public void Place(GameReader reader,Frame frame,Sample ghost,uint id,double opacity){
   if(frame==null||ghost==null||frame.Camera.ViewProjection==null){Hide();return;}Prepare(id);Native.Rect rect;var origin=new Native.Point();IntPtr target=reader.Window;if(target==IntPtr.Zero||Native.IsIconic(target)||!Native.GetClientRect(target,out rect)||!Native.ClientToScreen(target,ref origin)){Hide();return;}
   int width=rect.Right-rect.Left,height=rect.Bottom-rect.Top;if(width<1||height<1){Hide();return;}
   Pose p=ghost.Pose;pose.Matrix=new Matrix3D(p.Right.X,p.Right.Y,p.Right.Z,0,p.Up.X,p.Up.Y,p.Up.Z,0,p.Forward.X,p.Forward.Y,p.Forward.Z,0,p.Position.X,p.Position.Y,p.Position.Z,1);
   camera.ViewMatrix=Matrix3D.Identity;camera.ProjectionMatrix=Projection(frame.Camera.ViewProjection);viewport.Opacity=Math.Max(.1,Math.Min(.9,opacity));
   bool showing=!IsVisible;if(showing)Show();if(showing||origin.X!=lastX||origin.Y!=lastY||width!=lastWidth||height!=lastHeight){Native.SetWindowPos(window,new IntPtr(-1),origin.X,origin.Y,width,height,0x10|0x40);lastX=origin.X;lastY=origin.Y;lastWidth=width;lastHeight=height;}
  }
  public static Matrix3D Projection(float[] m){if(m==null||m.Length!=16)throw new InvalidDataException("Missing camera matrix");return new Matrix3D(m[0],m[1],m[2],m[3],m[4],m[5],m[6],m[7],m[8],m[9],m[10],m[11],m[12],m[13],m[14],m[15]);}
#if DEVELOPMENT
  public static void Preview(VehicleModel model,string file,double opacity){
   var v=new Viewport3D{Width=900,Height=600,Opacity=opacity};v.Camera=new PerspectiveCamera(new Point3D(-6,3.2,7),new Vector3D(6,-2.5,-7),new Vector3D(0,1,0),42);v.Children.Add(new ModelVisual3D{Content=Lights()});v.Children.Add(new ModelVisual3D{Content=model.Geometry});v.Measure(new Size(900,600));v.Arrange(new Rect(0,0,900,600));v.UpdateLayout();var image=new RenderTargetBitmap(900,600,96,96,PixelFormats.Pbgra32);image.Render(v);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var stream=File.Create(file))png.Save(stream);
  }
#endif

 }
}