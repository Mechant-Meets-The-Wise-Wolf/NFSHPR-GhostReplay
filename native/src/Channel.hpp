// Included inside namespace ghost. No disk I/O or asset parsing in Present.
struct Asset {std::vector<Vertex> v;std::vector<uint32_t> i;std::vector<Sample> t;uint32_t model=0,event=0;};
static bool validPose(const float* p){for(int j=0;j<12;j++)if(!std::isfinite(p[j]))return false;double length=0;for(int j=9;j<12;j++)length+=(double)p[j]*p[j];if(length>=1e12)return false;for(int j=0;j<3;j++){float n=0;for(int k=0;k<3;k++)n+=p[j*3+k]*p[j*3+k];if(fabsf(sqrtf(n)-1)>.1f)return false;}for(int j=0;j<3;j++)for(int k=j+1;k<3;k++){float dot=0;for(int n=0;n<3;n++)dot+=p[j*3+n]*p[k*3+n];if(fabsf(dot)>.1f)return false;}float c[3];cross(p,p+3,c);return c[0]*p[6]+c[1]*p[7]+c[2]*p[8]>.8f;}
static bool parsePayload(const unsigned char* bytes,size_t size,Asset& asset){
 if(size<sizeof(PayloadHeader)||size>128*1024*1024)return false;PayloadHeader h;memcpy(&h,bytes,sizeof(h));
 if(memcmp(h.magic,"HPRNAT03",8)||h.bytes!=size||!h.model||!h.event||h.vertices<3||h.vertices>1000000||h.indices<3||h.indices>3000000||h.indices%3||h.samples<2||h.samples>1000000)return false;
 uint64_t expected=32ULL+40ULL*h.vertices+4ULL*h.indices+60ULL*h.samples;if(expected!=size)return false;
 asset.model=h.model;asset.event=h.event;asset.v.resize(h.vertices);asset.i.resize(h.indices);asset.t.resize(h.samples);size_t at=32;
 memcpy(asset.v.data(),bytes+at,40ULL*h.vertices);at+=40ULL*h.vertices;memcpy(asset.i.data(),bytes+at,4ULL*h.indices);at+=4ULL*h.indices;
 for(auto& v:asset.v){for(float x:v.p)if(!std::isfinite(x)||fabsf(x)>30)return false;for(float x:v.n)if(!std::isfinite(x))return false;for(float x:v.colour)if(!std::isfinite(x)||x<0||x>1)return false;}for(auto index:asset.i)if(index>=h.vertices)return false;
 double last=-1;for(auto& s:asset.t){memcpy(&s.time,bytes+at,8);memcpy(s.pose,bytes+at+8,48);memcpy(&s.speed,bytes+at+56,4);at+=60;if(!std::isfinite(s.time)||s.time<=last||s.time>21600||!validPose(s.pose)||!std::isfinite(s.speed)||s.speed<0||s.speed>500)return false;last=s.time;}return asset.t.front().time==0&&asset.t.back().time>0;
}
static bool loadMappedAsset(const std::wstring& name,Asset& output){
 if(name.rfind(L"Local\\HPRGhostReplayAsset_",0)!=0)return false;HANDLE map=OpenFileMappingW(FILE_MAP_READ,FALSE,name.c_str());if(!map)return false;auto bytes=(const unsigned char*)MapViewOfFile(map,FILE_MAP_READ,0,0,0);bool ok=false;
 if(bytes){MEMORY_BASIC_INFORMATION info{};PayloadHeader header{};if(VirtualQuery(bytes,&info,sizeof(info))&&info.RegionSize>=32){memcpy(&header,bytes,32);if(header.bytes>=32&&header.bytes<=info.RegionSize){try{ok=parsePayload(bytes,header.bytes,output);}catch(...){ok=false;}}}UnmapViewOfFile(bytes);}CloseHandle(map);return ok;
}
static bool channelValid(){return control&&control->magic==0x334e4748&&control->size==512&&control->version==3;}
static bool canDraw(){return channelValid()&&control->enabled&&control->error==0&&control->ownerPid==activeOwner&&control->ownerToken==activeToken&&control->assetRevision==control->loadedRevision&&control->loadedToken==activeToken&&(DWORD)(GetTickCount()-(DWORD)control->ownerHeartbeat)<2000;}
static void pollAsset(LONG& attemptedToken,LONG& attemptedRevision){
 if(!channelValid())return;LONG seq=InterlockedCompareExchange(&control->sequence,0,0);if(seq&1)return;LONG token=control->ownerToken,revision=control->assetRevision,owner=control->ownerPid;LONG length=control->nameLength;
 if(!owner||!token||revision<=0||(token==attemptedToken&&revision==attemptedRevision)||length<1||length>127)return;
 std::wstring name(control->assetName,control->assetName+length);MemoryBarrier();if(seq!=control->sequence||token!=control->ownerToken||revision!=control->assetRevision)return;
 attemptedToken=token;attemptedRevision=revision;Asset asset;if(!loadMappedAsset(name,asset)){if(token==control->ownerToken&&revision==control->assetRevision)InterlockedExchange(&control->error,8);return;}
 std::lock_guard<std::mutex> lock(renderLock);if(seq!=control->sequence||token!=control->ownerToken||revision!=control->assetRevision||owner!=control->ownerPid)return;
 vertices.swap(asset.v);indices.swap(asset.i);track.swap(asset.t);modelId=replayModel=asset.model;replayEvent=asset.event;activeToken=token;activeOwner=owner;assetGeneration++;
 InterlockedExchange(&control->error,0);InterlockedExchange(&control->loadedToken,token);InterlockedExchange(&control->loadedRevision,revision);
}
