#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <d3d11.h>
#include <dxgi.h>
#include <d3dcompiler.h>
#include <vector>
#include <string>
#include <fstream>
#include <mutex>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <algorithm>
#include <atomic>
#include "MinHook.h"
extern "C" __declspec(dllexport) const char HPRGhostReplayComponentId[]="HPRGHOST_NATIVE_COMPONENT_V3";

namespace ghost {
template<class T> void release(T*& p){if(p){p->Release();p=nullptr;}}
struct Vertex {float p[3],n[3],colour[4];};
struct Sample {double time;float pose[12],speed;};
struct Control {volatile LONG magic,size,version,sequence,enabled,opacity,ownerPid,ownerHeartbeat,pid,heartbeat,connected,error,assetRevision,loadedRevision,frames,draws,skips,event,lastDrawTick,nameLength;wchar_t assetName[128];volatile LONG ownerToken,loadedToken,reserved[42];};
static_assert(sizeof(Control)==512,"IPC size");
struct PayloadHeader {char magic[8];uint32_t bytes,model,event,vertices,indices,samples;};
static_assert(sizeof(PayloadHeader)==32,"Payload header size");
struct Constants {float world[16],vp[16],alpha[4];};
static std::wstring folder,assets;static HMODULE module;static HANDLE mapping;static Control* control;
static std::vector<Vertex> vertices;static std::vector<uint32_t> indices;static std::vector<Sample> track;
static uint32_t modelId=0,replayModel=0,replayEvent=0;static uintptr_t base;static std::mutex renderLock;
static std::atomic<LONG> activeToken{0},activeOwner{0},assetGeneration{0};
static ID3DBlob *vsCode=nullptr,*psCode=nullptr;static HMODULE shaderCompiler=nullptr;static void* presentTarget;static void* resizeTarget;
using Present=HRESULT(STDMETHODCALLTYPE*)(IDXGISwapChain*,UINT,UINT);
using Resize=HRESULT(STDMETHODCALLTYPE*)(IDXGISwapChain*,UINT,UINT,UINT,DXGI_FORMAT,UINT);
static Present originalPresent;static Resize originalResize;
static void log(const char*){} // Release reports failures through the control channel only.
static bool readBytes(uintptr_t address,void* output,size_t size){if(address<65536||address>0x00007fffffffffffULL-size)return false;SIZE_T got=0;return ReadProcessMemory(GetCurrentProcess(),(void*)address,output,size,&got)&&got==size;}
template<class T> bool read(uintptr_t address,T& output){return readBytes(address,&output,sizeof(output));}
static bool pointer(uintptr_t at,uintptr_t& output){return read(at,output)&&output>=65536&&output<0x00007fffffffffffULL;}
static bool layout(){
 struct Anchor {uintptr_t rva;const unsigned char* bytes;size_t count;};
 static const unsigned char a[]={0x48,0x89,0x5c,0x24,0x08,0x57,0x48,0x83,0xec,0x30,0x0f,0x29,0x74,0x24,0x20},b[]={0x48,0x8b,0x41,0x08,0x48,0x8d,0x0d,0xc5,0x3a,0x09,0x01},c[]={0x48,0x8b,0x81,0x90,0x1e,0,0,0x4c,0x63,0x80,0x04,0x6c,0,0},d[]={0x48,0x8b,0x05,0x6a,0x04,0,0x01};
 Anchor anchors[]={{0x296bb0,a,sizeof(a)},{0x1f4fb0,b,sizeof(b)},{0x228f20,c,sizeof(c)},{0x29c077,d,sizeof(d)}};
 for(auto& x:anchors){unsigned char actual[32];if(!readBytes(base+x.rva,actual,x.count)||memcmp(actual,x.bytes,x.count))return false;}return true;
}
template<class T> bool get(std::ifstream& f,T& value){return !!f.read((char*)&value,sizeof(value));}
static bool loadMesh(const std::wstring& path){std::ifstream f(path.c_str(),std::ios::binary);char magic[8];uint32_t nv=0,ni=0;if(!f.read(magic,8)||memcmp(magic,"GHNMESH1",8)||!get(f,modelId)||!get(f,nv)||!get(f,ni)||nv<3||nv>1000000||ni<3||ni>3000000||ni%3)return false;vertices.resize(nv);indices.resize(ni);if(!f.read((char*)vertices.data(),nv*sizeof(Vertex))||!f.read((char*)indices.data(),ni*4))return false;for(auto& v:vertices)for(float x:v.p)if(!std::isfinite(x)||fabsf(x)>30)return false;for(auto i:indices)if(i>=nv)return false;return true;}
static bool loadReplay(const std::wstring& path){std::ifstream f(path.c_str(),std::ios::binary);char magic[8];int version;bool completed;uint32_t tag,count;double result;if(!f.read(magic,8)||memcmp(magic,"HPRGHOST",8)||!get(f,version)||version!=4)return false;for(int i=0;i<4;i++){int n;if(!get(f,n)||n<0||n>1024)return false;f.seekg(n,std::ios::cur);}if(!get(f,tag)||!get(f,replayModel)||!get(f,replayEvent)||!get(f,completed)||!get(f,result)||!get(f,count)||!completed||count<2||count>1000000||replayModel!=modelId)return false;track.resize(count);double last=-1;for(auto& s:track){if(!get(f,s.time)||!f.read((char*)s.pose,48)||!get(f,s.speed)||!std::isfinite(s.time)||s.time<=last||s.time>21600)return false;for(float x:s.pose)if(!std::isfinite(x))return false;last=s.time;}return track[0].time==0;}
static bool normalize(float* p){float n=sqrtf(p[0]*p[0]+p[1]*p[1]+p[2]*p[2]);if(n<1e-6f)return false;for(int i=0;i<3;i++)p[i]/=n;return true;}
static void cross(const float* a,const float* b,float* c){c[0]=a[1]*b[2]-a[2]*b[1];c[1]=a[2]*b[0]-a[0]*b[2];c[2]=a[0]*b[1]-a[1]*b[0];}
struct Quaternion {
 double x,y,z,w;
 Quaternion unit()const{double n=sqrt(x*x+y*y+z*z+w*w);return {x/n,y/n,z/n,w/n};}
 static Quaternion from(const float* p){double a=p[0],b=p[3],c=p[6],d=p[1],e=p[4],f=p[7],g=p[2],h=p[5],i=p[8],t=a+e+i,s;Quaternion q;if(t>0){s=sqrt(t+1)*2;q={(h-f)/s,(c-g)/s,(d-b)/s,s/4};}else if(a>e&&a>i){s=sqrt(1+a-e-i)*2;q={s/4,(b+d)/s,(c+g)/s,(h-f)/s};}else if(e>i){s=sqrt(1+e-a-i)*2;q={(b+d)/s,s/4,(f+h)/s,(c-g)/s};}else{s=sqrt(1+i-a-e)*2;q={(c+g)/s,(f+h)/s,s/4,(d-b)/s};}return q.unit();}
 static Quaternion slerp(Quaternion a,Quaternion b,double t){double dot=a.x*b.x+a.y*b.y+a.z*b.z+a.w*b.w;if(dot<0){b={-b.x,-b.y,-b.z,-b.w};dot=-dot;}dot=std::min(1.,dot);double u,v;if(dot>.9995){u=1-t;v=t;}else {double theta=acos(dot);u=sin((1-t)*theta)/sin(theta);v=sin(t*theta)/sin(theta);}return Quaternion{a.x*u+b.x*v,a.y*u+b.y*v,a.z*u+b.z*v,a.w*u+b.w*v}.unit();}
 void basis(float* p)const{auto q=unit();double x=q.x,y=q.y,z=q.z,w=q.w;p[0]=(float)(1-2*(y*y+z*z));p[1]=(float)(2*(x*y+z*w));p[2]=(float)(2*(x*z-y*w));p[3]=(float)(2*(x*y-z*w));p[4]=(float)(1-2*(x*x+z*z));p[5]=(float)(2*(y*z+x*w));p[6]=(float)(2*(x*z+y*w));p[7]=(float)(2*(y*z-x*w));p[8]=(float)(1-2*(x*x+y*y));}
};
static bool replayPose(double time,float* pose){if(track.size()<2||time<0||time>track.back().time)return false;auto hi=std::upper_bound(track.begin(),track.end(),time,[](double t,const Sample& s){return t<s.time;});if(hi==track.end())hi=track.end()-1;if(hi==track.begin())return false;auto& b=*hi;auto& a=*(hi-1);double dt=b.time-a.time;if(dt<=0||dt>.5)return false;float distance=0;for(int j=9;j<12;j++)distance+=(b.pose[j]-a.pose[j])*(b.pose[j]-a.pose[j]);if(sqrtf(distance)>std::max(30.,220*dt))return false;float t=(float)((time-a.time)/dt);for(int j=9;j<12;j++)pose[j]=a.pose[j]*(1-t)+b.pose[j]*t;Quaternion::slerp(Quaternion::from(a.pose),Quaternion::from(b.pose),t).basis(pose);return true;}
struct GameFrame {float pose[12],vp[16];uint32_t event=0,vehicle=0;int64_t race=0,tick=0;};
static bool simTick(int64_t& tick){uintptr_t manager,data;return pointer(base+0x129c498,manager)&&pointer(manager+8,data)&&read(data+0x20,tick);}
static bool gameFrame(GameFrame& out){
 int64_t before,after;uintptr_t race,manager,package,empty,resource,definition,state,vtable,getter;
 if(!simTick(before)||!pointer(base+0x1279218,race)||!pointer(base+0x1279220,manager)||!pointer(manager+0x4990,package)||!read(base+0x12795c8,empty)||package==empty)return false;
 int offset,competitor,part,primitive;uint16_t parts,finishPart,timerPart;
 if(!read(manager+0x4998,offset)||offset<0||offset>0x10000000||!pointer(package+0x28,resource)||!read(resource+offset+0x1c,out.event)||!read(race+0x6c04,competitor)||competitor<0||competitor>=8)return false;
 uintptr_t record=race+0x1180+competitor*0xb50;
 if(!pointer(record+0x10,definition)||!read(record+0x18,part)||!read(definition+0x28,parts)||!read(record+0x60,finishPart)||!read(record+0x62,timerPart)||!read(record+0x64,primitive)||parts>4096||finishPart>4096)return false;
 if(finishPart){uintptr_t ends;int64_t value;if(!pointer(record+0x38,ends)||!read(ends+finishPart*8ULL,value)||value!=INT64_MIN)return false;}
 bool start=false;if(primitive>=0&&primitive<65536){uintptr_t flags;unsigned char v;if(!pointer(record+0x28,flags)||!read(flags+primitive,v))return false;start=v!=0;}
 if(part<0||part>=parts||!(timerPart<=part||start))return false;
 if(!pointer(race+0x239c8,state)||!pointer(state+0x18,vtable)||!pointer(vtable+0x28,getter)||getter!=base+0x2a7ef0)return false;
 unsigned char table[0x150];if(!readBytes(state+0x2f0,table,sizeof(table)))return false;int count;memcpy(&count,table+0x140,4);if(count<0||count>8)return false;uintptr_t clockAddress=0;for(int i=0;i<count;i++){int id;memcpy(&id,table+i*40,4);if(id==competitor){memcpy(&out.race,table+i*40+8,8);clockAddress=state+0x2f0+i*40+8;break;}}if(!clockAddress||out.race<0||out.race>21600000000LL)return false;
 int index,poolOffset;unsigned char active;uintptr_t vm,vd,vehicle,cm,cd;
 if(!read(record+0x991,active)||!active||!read(record+0x924,index)||index<0||index>=8||!pointer(base+0x129c4c8,vm)||!pointer(vm+8,vd)||!read(vd+0x3108,poolOffset)||poolOffset<0||poolOffset>65536||!pointer(base+0x1288a80+8ULL*(poolOffset+index),vehicle))return false;
 float transform[16];if(!readBytes(vehicle+0xa0,transform,64))return false;for(int row=0;row<4;row++)for(int col=0;col<3;col++)out.pose[row*3+col]=transform[row*4+col];
 if(!read(race+0x31b20+0x1d34+competitor*4,out.vehicle)||!pointer(base+0x129c4e8,cm)||!pointer(cm+8,cd)||!readBytes(cd+0x90,out.vp,64)||!simTick(after)||before!=after)return false;
 float cameraAfter[16];int64_t raceAfter;uintptr_t raceAfterPointer,poolAfterPointer;if(!readBytes(cd+0x90,cameraAfter,64)||memcmp(cameraAfter,out.vp,64)||!read(clockAddress,raceAfter)||raceAfter!=out.race||!read(base+0x1279218,raceAfterPointer)||raceAfterPointer!=race||!read(vm+8,poolAfterPointer)||poolAfterPointer!=vd)return false;
 for(float x:out.pose)if(!std::isfinite(x))return false;for(float x:out.vp)if(!std::isfinite(x))return false;out.tick=before;return true;
}
static const char* shader=R"(
cbuffer Transform : register(b0) {row_major float4x4 World;row_major float4x4 VP;float4 Alpha;};
struct Input {float3 position:POSITION;float3 normal:NORMAL;float4 colour:COLOR;};
struct Output {float4 position:SV_POSITION;float3 normal:NORMAL;float4 colour:COLOR;};
Output VS(Input i){Output o;o.position=mul(mul(float4(i.position,1),World),VP);o.position.z=o.position.w*(o.position.w/(1+max(0,o.position.w)));o.normal=mul(float4(i.normal,0),World).xyz;o.colour=i.colour;return o;}
float4 PS(Output i):SV_TARGET {float light=.38+.52*abs(dot(normalize(i.normal),normalize(float3(-.5,.8,-.3))));return float4(i.colour.rgb*light,Alpha.x);}
)";
static bool compileShaders(){shaderCompiler=LoadLibraryExW(L"d3dcompiler_47.dll",nullptr,LOAD_LIBRARY_SEARCH_SYSTEM32);if(!shaderCompiler)return false;auto compile=(decltype(&D3DCompile))GetProcAddress(shaderCompiler,"D3DCompile");ID3DBlob* errors=nullptr;bool ok=compile&&SUCCEEDED(compile(shader,strlen(shader),nullptr,nullptr,nullptr,"VS","vs_4_0",D3DCOMPILE_OPTIMIZATION_LEVEL3,0,&vsCode,&errors));release(errors);ok=ok&&SUCCEEDED(compile(shader,strlen(shader),nullptr,nullptr,nullptr,"PS","ps_4_0",D3DCOMPILE_OPTIMIZATION_LEVEL3,0,&psCode,&errors));release(errors);return ok;}
struct Renderer {
 LONG generation=-1;
 ID3D11Device* device=nullptr;ID3D11DeviceContext *immediate=nullptr,*deferred=nullptr;ID3D11RenderTargetView* target=nullptr;ID3D11VertexShader* vs=nullptr;ID3D11PixelShader* ps=nullptr;ID3D11InputLayout* layout=nullptr;ID3D11Buffer *vb=nullptr,*ib=nullptr,*cb=nullptr;ID3D11BlendState *blend=nullptr,*noColour=nullptr;ID3D11DepthStencilState *depth=nullptr,*depthRead=nullptr;ID3D11DepthStencilView* selfDepth=nullptr;UINT depthWidth=0,depthHeight=0;ID3D11RasterizerState* raster=nullptr;IDXGISwapChain* chain=nullptr;
 ~Renderer(){reset();}
 void reset(){release(target);release(selfDepth);depthWidth=depthHeight=0;release(raster);release(depth);release(depthRead);release(blend);release(noColour);release(cb);release(ib);release(vb);release(layout);release(ps);release(vs);release(deferred);release(immediate);release(device);chain=nullptr;}
 bool init(ID3D11Device* dev){reset();device=dev;device->AddRef();device->GetImmediateContext(&immediate);if(FAILED(device->CreateDeferredContext(0,&deferred)))return false;
  if(FAILED(device->CreateVertexShader(vsCode->GetBufferPointer(),vsCode->GetBufferSize(),nullptr,&vs))||FAILED(device->CreatePixelShader(psCode->GetBufferPointer(),psCode->GetBufferSize(),nullptr,&ps)))return false;
  D3D11_INPUT_ELEMENT_DESC elements[]={{"POSITION",0,DXGI_FORMAT_R32G32B32_FLOAT,0,0,D3D11_INPUT_PER_VERTEX_DATA,0},{"NORMAL",0,DXGI_FORMAT_R32G32B32_FLOAT,0,12,D3D11_INPUT_PER_VERTEX_DATA,0},{"COLOR",0,DXGI_FORMAT_R32G32B32A32_FLOAT,0,24,D3D11_INPUT_PER_VERTEX_DATA,0}};
  if(FAILED(device->CreateInputLayout(elements,3,vsCode->GetBufferPointer(),vsCode->GetBufferSize(),&layout)))return false;
  D3D11_BUFFER_DESC bd{};bd.ByteWidth=(UINT)(vertices.size()*sizeof(Vertex));bd.Usage=D3D11_USAGE_IMMUTABLE;bd.BindFlags=D3D11_BIND_VERTEX_BUFFER;D3D11_SUBRESOURCE_DATA data{vertices.data(),0,0};if(FAILED(device->CreateBuffer(&bd,&data,&vb)))return false;
  bd.ByteWidth=(UINT)(indices.size()*4);bd.BindFlags=D3D11_BIND_INDEX_BUFFER;data.pSysMem=indices.data();if(FAILED(device->CreateBuffer(&bd,&data,&ib)))return false;bd.ByteWidth=sizeof(Constants);bd.BindFlags=D3D11_BIND_CONSTANT_BUFFER;bd.Usage=D3D11_USAGE_DEFAULT;if(FAILED(device->CreateBuffer(&bd,nullptr,&cb)))return false;
  D3D11_BLEND_DESC b{};auto& r=b.RenderTarget[0];r.BlendEnable=TRUE;r.SrcBlend=D3D11_BLEND_SRC_ALPHA;r.DestBlend=D3D11_BLEND_INV_SRC_ALPHA;r.BlendOp=D3D11_BLEND_OP_ADD;r.SrcBlendAlpha=D3D11_BLEND_ONE;r.DestBlendAlpha=D3D11_BLEND_INV_SRC_ALPHA;r.BlendOpAlpha=D3D11_BLEND_OP_ADD;r.RenderTargetWriteMask=7;if(FAILED(device->CreateBlendState(&b,&blend)))return false;
  D3D11_BLEND_DESC noOutput{};if(FAILED(device->CreateBlendState(&noOutput,&noColour)))return false;
  D3D11_DEPTH_STENCIL_DESC d{};d.DepthEnable=TRUE;d.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ALL;d.DepthFunc=D3D11_COMPARISON_LESS_EQUAL;if(FAILED(device->CreateDepthStencilState(&d,&depth)))return false;d.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ZERO;if(FAILED(device->CreateDepthStencilState(&d,&depthRead)))return false;
  D3D11_RASTERIZER_DESC rs{};rs.FillMode=D3D11_FILL_SOLID;rs.CullMode=D3D11_CULL_NONE;rs.DepthClipEnable=TRUE;rs.MultisampleEnable=TRUE;return SUCCEEDED(device->CreateRasterizerState(&rs,&raster));
 }
 bool render(ID3D11RenderTargetView* rtv,UINT width,UINT height,const Constants& constants){
  if(!selfDepth||depthWidth!=width||depthHeight!=height){release(selfDepth);ID3D11Resource* resource=nullptr;ID3D11Texture2D* colour=nullptr;rtv->GetResource(&resource);HRESULT hr=resource->QueryInterface(__uuidof(ID3D11Texture2D),(void**)&colour);release(resource);if(FAILED(hr))return false;D3D11_TEXTURE2D_DESC td{};colour->GetDesc(&td);release(colour);td.Format=DXGI_FORMAT_D32_FLOAT;td.BindFlags=D3D11_BIND_DEPTH_STENCIL;td.Usage=D3D11_USAGE_DEFAULT;td.CPUAccessFlags=td.MiscFlags=0;ID3D11Texture2D* z=nullptr;hr=device->CreateTexture2D(&td,nullptr,&z);if(FAILED(hr))return false;hr=device->CreateDepthStencilView(z,nullptr,&selfDepth);release(z);if(FAILED(hr))return false;depthWidth=width;depthHeight=height;}
  deferred->UpdateSubresource(cb,0,nullptr,&constants,0,0);deferred->ClearDepthStencilView(selfDepth,D3D11_CLEAR_DEPTH,1,0);deferred->OMSetRenderTargets(1,&rtv,selfDepth);float factor[4]={0,0,0,0};deferred->OMSetBlendState(noColour,factor,~0U);deferred->OMSetDepthStencilState(depth,0);deferred->RSSetState(raster);D3D11_VIEWPORT view{0,0,(float)width,(float)height,0,1};deferred->RSSetViewports(1,&view);UINT stride=sizeof(Vertex),offset=0;deferred->IASetInputLayout(layout);deferred->IASetVertexBuffers(0,1,&vb,&stride,&offset);deferred->IASetIndexBuffer(ib,DXGI_FORMAT_R32_UINT,0);deferred->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);deferred->VSSetShader(vs,nullptr,0);deferred->VSSetConstantBuffers(0,1,&cb);deferred->PSSetShader(nullptr,nullptr,0);deferred->DrawIndexed((UINT)indices.size(),0,0);
  deferred->OMSetBlendState(blend,factor,~0U);deferred->OMSetDepthStencilState(depthRead,0);deferred->PSSetShader(ps,nullptr,0);deferred->PSSetConstantBuffers(0,1,&cb);deferred->DrawIndexed((UINT)indices.size(),0,0);
  ID3D11CommandList* list=nullptr;if(FAILED(deferred->FinishCommandList(FALSE,&list)))return false;immediate->ExecuteCommandList(list,TRUE);release(list);return true;
 }
 bool draw(IDXGISwapChain* sc,const Constants& constants){DXGI_SWAP_CHAIN_DESC desc{};if(FAILED(sc->GetDesc(&desc)))return false;ID3D11Device* actual=nullptr;if(FAILED(sc->GetDevice(__uuidof(ID3D11Device),(void**)&actual)))return false;bool changed=device!=actual||chain!=sc||generation!=assetGeneration;if(changed){if(!init(actual)){release(actual);return false;}chain=sc;generation=assetGeneration;}release(actual);if(!target){ID3D11Texture2D* buffer=nullptr;if(FAILED(sc->GetBuffer(0,__uuidof(ID3D11Texture2D),(void**)&buffer)))return false;HRESULT hr=device->CreateRenderTargetView(buffer,nullptr,&target);release(buffer);if(FAILED(hr))return false;}return render(target,desc.BufferDesc.Width,desc.BufferDesc.Height,constants);}
};
static Renderer renderer;
static void matrix(const float* pose,float* out){memset(out,0,64);for(int i=0;i<4;i++)for(int j=0;j<3;j++)out[i*4+j]=pose[i*3+j];out[15]=1;}
#include "Channel.hpp"
static HRESULT STDMETHODCALLTYPE onPresent(IDXGISwapChain* sc,UINT interval,UINT flags){
 if(channelValid()&&!(flags&DXGI_PRESENT_TEST)){
  DXGI_SWAP_CHAIN_DESC desc{};DWORD owner=0;
  if(SUCCEEDED(sc->GetDesc(&desc))&&desc.OutputWindow&&desc.BufferDesc.Width>300&&GetWindowThreadProcessId(desc.OutputWindow,&owner)&&owner==GetCurrentProcessId()){
   InterlockedExchange(&control->connected,1);InterlockedIncrement(&control->frames);
   bool live=canDraw();
   if(live&&GetForegroundWindow()==desc.OutputWindow){std::unique_lock<std::mutex> lock(renderLock,std::try_to_lock);if(lock.owns_lock()){GameFrame frame;float pose[12];if(gameFrame(frame)){InterlockedExchange(&control->event,(LONG)frame.event);if(frame.event==replayEvent&&replayPose(frame.race*1e-6,pose)){Constants c{};matrix(pose,c.world);memcpy(c.vp,frame.vp,64);c.alpha[0]=std::max(.1f,std::min(.9f,(float)control->opacity*.01f));if(renderer.draw(sc,c)){InterlockedIncrement(&control->draws);InterlockedExchange(&control->lastDrawTick,(LONG)GetTickCount());}else{InterlockedExchange(&control->error,7);InterlockedExchange(&control->enabled,0);}}else InterlockedIncrement(&control->skips);}else InterlockedIncrement(&control->skips);}else InterlockedIncrement(&control->skips);}
  }
 }
 return originalPresent(sc,interval,flags);
}
static HRESULT STDMETHODCALLTYPE onResize(IDXGISwapChain* sc,UINT count,UINT width,UINT height,DXGI_FORMAT format,UINT flags){{std::lock_guard<std::mutex> lock(renderLock);if(renderer.chain==sc)release(renderer.target);}return originalResize(sc,count,width,height,format,flags);}
static DWORD WINAPI worker(void*){
 HMODULE pin;GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_PIN,(LPCWSTR)&worker,&pin);
 base=(uintptr_t)GetModuleHandleW(nullptr);
 mapping=CreateFileMappingW(INVALID_HANDLE_VALUE,nullptr,PAGE_READWRITE,0,sizeof(Control),L"Local\\HPRGhostReplayNative_v3");if(!mapping)return 1;control=(Control*)MapViewOfFile(mapping,FILE_MAP_ALL_ACCESS,0,0,sizeof(Control));if(!control)return 1;
 if(control->magic!=0x334e4748){control->size=512;control->version=3;MemoryBarrier();control->magic=0x334e4748;}
 control->enabled=0;control->pid=(LONG)GetCurrentProcessId();control->connected=0;control->loadedRevision=0;control->loadedToken=0;control->error=0;control->heartbeat=(LONG)GetTickCount();control->lastDrawTick=0;
 bool matched=false;for(int attempt=0;attempt<600;attempt++){control->heartbeat=(LONG)GetTickCount();if(layout()){matched=true;break;}Sleep(200);}if(!matched){control->error=1;return 1;}
 if(!compileShaders()){control->error=3;return 1;}
 WNDCLASSW wc{};wc.lpfnWndProc=DefWindowProcW;wc.hInstance=module;wc.lpszClassName=L"HPRGhostNativeDiscovery";RegisterClassW(&wc);HWND window=CreateWindowW(wc.lpszClassName,L"",WS_OVERLAPPED,0,0,16,16,nullptr,nullptr,module,nullptr);
 DXGI_SWAP_CHAIN_DESC sd{};sd.BufferDesc.Width=16;sd.BufferDesc.Height=16;sd.BufferDesc.Format=DXGI_FORMAT_R8G8B8A8_UNORM;sd.SampleDesc.Count=1;sd.BufferUsage=DXGI_USAGE_RENDER_TARGET_OUTPUT;sd.BufferCount=1;sd.OutputWindow=window;sd.Windowed=TRUE;IDXGISwapChain* dummy=nullptr;ID3D11Device* dev=nullptr;ID3D11DeviceContext* ctx=nullptr;
 HRESULT hr=D3D11CreateDeviceAndSwapChain(nullptr,D3D_DRIVER_TYPE_WARP,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&sd,&dummy,&dev,nullptr,&ctx);if(FAILED(hr)){control->error=4;log("SWAPCHAIN_DISCOVERY_FAILED");DestroyWindow(window);return 1;}auto vt=*(void***)dummy;presentTarget=vt[8];resizeTarget=vt[13];release(ctx);release(dev);release(dummy);DestroyWindow(window);UnregisterClassW(wc.lpszClassName,module);
 if(MH_Initialize()!=MH_OK||MH_CreateHook(presentTarget,(void*)&onPresent,(void**)&originalPresent)!=MH_OK||MH_CreateHook(resizeTarget,(void*)&onResize,(void**)&originalResize)!=MH_OK){control->error=5;log("HOOK_CREATE_FAILED; no hooks enabled");return 1;}
 MH_QueueEnableHook(presentTarget);MH_QueueEnableHook(resizeTarget);if(MH_ApplyQueued()!=MH_OK){control->error=6;log("HOOK_ENABLE_FAILED");return 1;}LONG token=0,revision=0;for(;;){InterlockedExchange(&control->heartbeat,(LONG)GetTickCount());pollAsset(token,revision);Sleep(50);}return 0;
}
#ifdef GHOST_SMOKE
static int payloadSmoke(const wchar_t* input,const wchar_t* expected){std::ifstream file(input,std::ios::binary|std::ios::ate);if(!file)return 20;auto length=file.tellg();if(length<32||length>128*1024*1024)return 20;file.seekg(0);std::vector<unsigned char> bytes((size_t)length);if(!file.read((char*)bytes.data(),bytes.size()))return 20;Asset asset;if(!parsePayload(bytes.data(),bytes.size(),asset))return 21;Asset invalid;if(parsePayload(bytes.data(),bytes.size()-1,invalid))return 22;PayloadHeader header;memcpy(&header,bytes.data(),32);uint32_t badIndex=header.vertices;memcpy(bytes.data()+32+header.vertices*40ULL,&badIndex,4);if(parsePayload(bytes.data(),bytes.size(),invalid))return 23;
 vertices=std::move(asset.v);indices=std::move(asset.i);track=std::move(asset.t);modelId=asset.model;replayEvent=asset.event;std::ifstream fixture(expected,std::ios::binary);uint32_t count;if(!get(fixture,count)||count>10000)return 24;double maxError=0;for(uint32_t j=0;j<count;j++){double time;int valid;float target[12],actual[12];if(!get(fixture,time)||!get(fixture,valid)||!fixture.read((char*)target,48))return 24;bool found=replayPose(time,actual);if(found!=(valid!=0))return 25;if(found)for(int k=0;k<12;k++){double error=fabs((double)actual[k]-target[k]);maxError=std::max(maxError,error);if(error>(k>=9?.001:.00001))return 26;}}
 Control test{};test.magic=0x334e4748;test.size=512;test.version=3;test.enabled=1;test.ownerPid=activeOwner=1234;test.ownerToken=test.loadedToken=activeToken=99;test.assetRevision=test.loadedRevision=2;test.ownerHeartbeat=(LONG)GetTickCount();control=&test;if(!canDraw())return 27;test.ownerHeartbeat=(LONG)(GetTickCount()-5000);if(canDraw())return 28;test.ownerHeartbeat=(LONG)GetTickCount();test.assetRevision=3;if(canDraw())return 29;test.assetRevision=2;test.ownerPid=0;if(canDraw())return 30;control=nullptr;printf("PayloadValidated=1 RejectedTruncation=1 RejectedBadIndex=1 QuaternionParitySamples=%u MaxError=%.9g OwnerLeaseValidated=1\n",count,maxError);return 0;}
static void smokeProjection(float* result){float eye[]={6,3.2f,7},forward[]={-6,-2.7f,-7},upWorld[]={0,1,0},right[3],up[3];normalize(forward);cross(upWorld,forward,right);normalize(right);cross(forward,right,up);float view[16]={right[0],up[0],forward[0],0,right[1],up[1],forward[1],0,right[2],up[2],forward[2],0,0,0,0,1};for(int j=0;j<3;j++){view[12]-=eye[j]*right[j];view[13]-=eye[j]*up[j];view[14]-=eye[j]*forward[j];}float projection[16]={1.5f,0,0,0,0,2,0,0,0,0,100.f/99.9f,1,0,0,-10.f/99.9f,0};for(int row=0;row<4;row++)for(int col=0;col<4;col++){result[row*4+col]=0;for(int k=0;k<4;k++)result[row*4+col]+=view[row*4+k]*projection[k*4+col];}}
static bool hookSmoke(){
 HINSTANCE instance=GetModuleHandleW(nullptr);WNDCLASSW wc{};wc.lpfnWndProc=DefWindowProcW;wc.hInstance=instance;wc.lpszClassName=L"HPRGhostNativeSmoke";RegisterClassW(&wc);HWND window=CreateWindowW(wc.lpszClassName,L"",WS_OVERLAPPED,0,0,640,480,nullptr,nullptr,instance,nullptr);
 DXGI_SWAP_CHAIN_DESC desc{};desc.BufferDesc.Width=640;desc.BufferDesc.Height=480;desc.BufferDesc.Format=DXGI_FORMAT_R8G8B8A8_UNORM;desc.BufferCount=1;desc.BufferUsage=DXGI_USAGE_RENDER_TARGET_OUTPUT;desc.SampleDesc.Count=1;desc.OutputWindow=window;desc.Windowed=TRUE;IDXGISwapChain* sc=nullptr;ID3D11Device* dev=nullptr;ID3D11DeviceContext* ctx=nullptr;
 HRESULT hr=D3D11CreateDeviceAndSwapChain(nullptr,D3D_DRIVER_TYPE_WARP,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&desc,&sc,&dev,nullptr,&ctx);if(FAILED(hr))return false;auto vt=*(void***)sc;Control test{};test.magic=0x334e4748;test.size=512;test.version=3;control=&test;
 bool ok=MH_Initialize()==MH_OK&&MH_CreateHook(vt[8],(void*)&onPresent,(void**)&originalPresent)==MH_OK&&MH_CreateHook(vt[13],(void*)&onResize,(void**)&originalResize)==MH_OK;
 if(ok){MH_QueueEnableHook(vt[8]);MH_QueueEnableHook(vt[13]);ok=MH_ApplyQueued()==MH_OK&&SUCCEEDED(sc->Present(0,0))&&test.frames>0&&test.connected==1&&SUCCEEDED(sc->ResizeBuffers(1,320,240,DXGI_FORMAT_UNKNOWN,0));}
 MH_DisableHook(MH_ALL_HOOKS);MH_Uninitialize();control=nullptr;release(ctx);release(dev);release(sc);DestroyWindow(window);UnregisterClassW(wc.lpszClassName,instance);return ok;
}
#endif
}
#ifndef GHOST_SMOKE
BOOL WINAPI DllMain(HINSTANCE instance,DWORD reason,LPVOID){if(reason==DLL_PROCESS_ATTACH){ghost::module=instance;DisableThreadLibraryCalls(instance);HANDLE thread=CreateThread(nullptr,0,ghost::worker,nullptr,0,nullptr);if(thread)CloseHandle(thread);}return TRUE;}
#else
int wmain(int argc,wchar_t** argv){using namespace ghost;if(argc==4&&std::wstring(argv[1])==L"--payload")return payloadSmoke(argv[2],argv[3]);if(argc!=3)return 2;std::wstring outputPath=argv[2];folder=outputPath.substr(0,outputPath.find_last_of(L"\\/"));if(!loadMesh(argv[1])||!compileShaders())return 3;std::wstring meshPath=argv[1];std::wstring replayPath=meshPath.substr(0,meshPath.find_last_of(L"\\/"))+L"\\selected.hprghost";float first[12],last[12],outside[12];bool replayValid=loadReplay(replayPath)&&replayPose(0,first)&&replayPose(track.back().time,last)&&!replayPose(-.1,outside)&&!replayPose(track.back().time+.1,outside);if(!replayValid)return 11;if(!hookSmoke())return 12;ID3D11Device* device=nullptr;ID3D11DeviceContext* context=nullptr;if(FAILED(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_WARP,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&device,nullptr,&context)))return 4;Renderer r;if(!r.init(device))return 5;D3D11_TEXTURE2D_DESC td{};td.Width=640;td.Height=480;td.MipLevels=td.ArraySize=1;td.Format=DXGI_FORMAT_R8G8B8A8_UNORM;td.SampleDesc.Count=1;td.Usage=D3D11_USAGE_DEFAULT;td.BindFlags=D3D11_BIND_RENDER_TARGET;ID3D11Texture2D* image=nullptr;ID3D11RenderTargetView* target=nullptr;if(FAILED(device->CreateTexture2D(&td,nullptr,&image))||FAILED(device->CreateRenderTargetView(image,nullptr,&target)))return 6;
 float clear[]={.04f,.06f,.08f,1};context->ClearRenderTargetView(target,clear);context->OMSetRenderTargets(1,&target,nullptr);D3D11_VIEWPORT before{3,4,320,240,.2f,.8f};context->RSSetViewports(1,&before);Constants c{};float p[]={1,0,0,0,1,0,0,0,1,0,0,0};matrix(p,c.world);smokeProjection(c.vp);c.alpha[0]=.45f;if(!r.render(target,640,480,c))return 7;
 UINT n=1;D3D11_VIEWPORT after{};context->RSGetViewports(&n,&after);ID3D11RenderTargetView* saved=nullptr;context->OMGetRenderTargets(1,&saved,nullptr);bool restored=saved==target&&!memcmp(&before,&after,sizeof(before));release(saved);td.Usage=D3D11_USAGE_STAGING;td.BindFlags=0;td.CPUAccessFlags=D3D11_CPU_ACCESS_READ;ID3D11Texture2D* staging=nullptr;if(FAILED(device->CreateTexture2D(&td,nullptr,&staging)))return 8;context->CopyResource(staging,image);D3D11_MAPPED_SUBRESOURCE map{};if(FAILED(context->Map(staging,0,D3D11_MAP_READ,0,&map)))return 9;std::ofstream out(argv[2],std::ios::binary);out<<"P6\n640 480\n255\n";int changed=0;for(UINT y=0;y<480;y++)for(UINT x=0;x<640;x++){unsigned char* pixel=(unsigned char*)map.pData+y*map.RowPitch+x*4;out.write((char*)pixel,3);if(pixel[0]>25||pixel[1]>30||pixel[2]>35)changed++;}out.close();context->Unmap(staging,0);release(staging);release(target);release(image);release(context);release(device);printf("Vertices=%zu Triangles=%zu ChangedPixels=%d StateRestored=%d ReplayValidated=%d ReplaySamples=%zu ReplayEvent=%u\n",vertices.size(),indices.size()/3,changed,restored?1:0,replayValid?1:0,track.size(),replayEvent);return restored&&changed>100?0:10;}
#endif
