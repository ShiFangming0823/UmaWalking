"""Real-player desktop-plane integration in a disposable build. Never edits shared profiles.
Test pointer commands exercise the normal drag queue; no system mouse input is injected.
"""
import argparse,ctypes,hashlib,json,math,os,subprocess,time,traceback
from pathlib import Path
from PIL import Image

p=argparse.ArgumentParser()
p.add_argument('--build-directory',required=True)
p.add_argument('--evidence-directory',required=True)
args=p.parse_args();base=Path(args.build_directory).resolve();out=Path(args.evidence_directory).resolve();out.mkdir(parents=True,exist_ok=True)
config=base/'DesktopSettings.json';original=config.read_bytes();checks=[];samples=[];process=None;serial=0
report={'Passed':False,'PhysicalMouseInjection':False}
u=ctypes.WinDLL('user32');u.SetThreadDpiAwarenessContext(ctypes.c_void_p(-4));width=u.GetSystemMetrics(0);height=u.GetSystemMetrics(1)
flat={'mode':'Desktop','line':[],'surfaces':[]}
plane={'mode':'Planes','line':[],'surfaces':[{'points':[{'x':.1,'y':.5},{'x':.9,'y':.5},{'x':.97,'y':.95},{'x':.03,'y':.95}],
       'entrance':{'edge':3,'start':.35,'end':.65},'exit':{'edge':1,'start':.35,'end':.65}}]}
line={'mode':'Line','line':[{'x':.1,'y':.8},{'x':.9,'y':.7}],'surfaces':[]}
def check(value,label,**values):
 row={'Passed':bool(value),'Check':label,**values};checks.append(row);print(json.dumps(row),flush=True)
 if not value:raise AssertionError(label)
def send(action,token=0,**values):
 global serial
 serial=max(serial+1,time.time_ns());f=base/'DesktopCommands'/(str(serial)+'.tmp')
 f.write_text(json.dumps(dict(action=action,token=token,**values)),encoding='utf-8');f.replace(f.with_suffix('.json'))
def read_once():
 try:
  s=json.loads((base/'DesktopLogs/state.json').read_text(encoding='utf-8-sig'))
  if not process or s['processId']!=process.pid:return None
  if s.get('error'):raise RuntimeError(s['error'])
  return s
 except(OSError,json.JSONDecodeError):return None
def read():
 for retry in range(12):
  snapshot=read_once()
  if snapshot is not None:return snapshot
  time.sleep(.025)
 return None
def wait(fn,seconds=60):
 end=time.monotonic()+seconds;s=None
 while time.monotonic()<end:
  s=read()
  if s and fn(s):samples.append(s);return s
  if process.poll() is not None:raise AssertionError('Test player exited early')
  time.sleep(.075)
 raise AssertionError('Timeout: '+json.dumps(s))
def actor(s,token):return next(a for a in s['actors'] if a['token']==token)
def launch():
 global process
 si=subprocess.STARTUPINFO();si.dwFlags|=subprocess.STARTF_USESHOWWINDOW;si.wShowWindow=0
 env=os.environ.copy();env['UMA_WALKING_PROFILE']=str(out/'profile')
 process=subprocess.Popen([str(base/'UmaDesktop.exe'),'-force-d3d11','-force-d3d11-bitblt-model','--desktop-test-mode',
  '--desktop-test-ignore-occlusion','--desktop-no-controller','--desktop-test-stable-population','--desktop-test-long-gait',
  '--desktop-max-actors','3','-logFile',str(base/'DesktopLogs/Player-desktop-plane.log')],cwd=base,startupinfo=si,env=env)
def ground(value):
 send('ground',ground=value)
 return wait(lambda s:s['ground']['mode']==value['mode'] and not s['loading'])
def capture(token,x,y,name):
 before=actor(read(),token)['poseVersion'];path=base/f'DesktopLogs/capture-{token}.png';stamp=time.time_ns()
 send('capture');send('testGroundPose',token,mode='proportions',groundU=x/width,groundV=y/height)
 s=wait(lambda s:actor(s,token)['poseVersion']>before)
 end=time.monotonic()+12
 while time.monotonic()<end:
  try:
   if path.stat().st_mtime_ns>=stamp:
    im=Image.open(path).convert('RGBA');box=im.getchannel('A').point(lambda p:255 if p>=64 else 0).getbbox()
    if box:im.save(out/(name+'.png'));return s,box,im.size
  except OSError:pass
  time.sleep(.08)
 raise AssertionError('No character capture: '+name)
def full_body(a,box,size):
 side=size[0];left=a['screenX']-side*.5+box[0];right=a['screenX']-side*.5+box[2]
 top=a['groundY']-side*(1-a['canvasFootViewport'])+box[1];bottom=a['groundY']-side*(1-a['canvasFootViewport'])+box[3]
 check(box[0]>1 and box[1]>1 and box[2]<side-1 and box[3]<side-1,'Full character fits its render canvas',box=box,side=side)
 check(left>=-1 and right<=width+1 and top>=-1 and bottom<=height+1,'Head, body and feet remain within screen',bounds=[left,top,right,bottom])
try:
 data=json.loads(original.decode('utf-8-sig'));data.update(Ground=flat,ModelSizeScale=1,InteractionProbability=0,FrameRate=30)
 config.write_text(json.dumps(data),encoding='utf-8');(base/'DesktopLogs').mkdir(exist_ok=True);launch()
 s=wait(lambda s:s['count']==3 and not s['loading'],120)
 check(all(a['orthographicDesktop'] and a['viewElevation']==30 for a in s['actors']),'All spawned actors use the new orthographic desktop plane')
 check(all(a['desktopAttached'] and a['desktopParent'] for a in s['actors']),'All pet windows remain attached to Explorer desktop')
 s=wait(lambda s:all(a['groundStage']==1 for a in s['actors']),90)
 check(all(a['portalOpacity']==1 for a in s['actors']),'Entry reaches the interior and becomes fully opaque')
 send('pause');s=wait(lambda s:s['manualPaused']);token=s['actors'][0]['token']
 a=actor(s,token);region=a['navigationRegion'];left,top=region[0]['x'],region[0]['y'];right,bottom=region[2]['x'],region[2]['y']
 boxes=[]
 for i,(tx,ty) in enumerate([(.1,.1),(.5,.5),(.9,.9),(.1,.9),(.9,.1)]):
  x=left+(right-left)*tx;y=top+(bottom-top)*ty
  s,box,size=capture(token,x,y,'desktop-position-'+str(i));a=actor(s,token);full_body(a,box,size)
  check(abs(a['screenX']-x)+abs(a['groundY']-y)<2,'Actor can reach both horizontal and vertical interior positions',x=x,y=y)
  boxes.append((box[2]-box[0],box[3]-box[1],a['groundDepthScale']))
 check(max(b[0] for b in boxes)-min(b[0] for b in boxes)<=2 and max(b[1] for b in boxes)-min(b[1] for b in boxes)<=2,'Rendered model has identical size at near and far desktop positions',sizes=boxes)
 check(max(b[2] for b in boxes)-min(b[2] for b in boxes)<1e-6,'Depth does not affect scale')
 # A real player drag freezes the action, clamps the full body, and commits the drop.
 send('pause');wait(lambda s:not s['manualPaused']);a=actor(read(),token);downx=round(a['screenX']);downy=round(a['groundY']-100)
 send('testPointerDown',token,groundU=downx,groundV=downy);s=wait(lambda s:actor(s,token)['pointerHeld']);frozen=actor(s,token)['animationTime']
 time.sleep(.6);check(actor(read(),token)['animationTime']==frozen,'Held desktop actor freezes its exact animation')
 for x,y in [(-5000,-5000),(width+5000,-5000),(width+5000,height+5000),(-5000,height+5000)]:
  version=actor(read(),token)['poseVersion'];send('testPointerMove',token,groundU=x,groundV=y);time.sleep(.45);s=read();a=actor(s,token)
  r=a['navigationRegion'];check(r[0]['x']-.1<=a['screenX']<=r[2]['x']+.1 and r[0]['y']-.1<=a['groundY']<=r[2]['y']+.1,'Offscreen drag clamps entire model to desktop',cursor=[x,y])
  check(a['animationTime']==frozen and a['portalOpacity']==1,'Boundary drag preserves pose and opacity')
 send('pause');wait(lambda s:s['manualPaused']);a=actor(read(),token);drop=(a['screenX'],a['groundY'])
 send('testPointerUp',token,groundU=-5000,groundV=height+5000);s=wait(lambda s:not actor(s,token)['motionHeld']);a=actor(s,token)
 check(math.hypot(a['screenX']-drop[0],a['groundY']-drop[1])<1,'Mouse release retains the drop position without snapping')
 send('pause');s=wait(lambda s:not s['manualPaused']);wait(lambda s:actor(s,token)['animationTime']!=frozen)
 check(True,'Released actor resumes animation and movement')
 send('pause');wait(lambda s:s['manualPaused'])
 for scale in [.5,1.5,1.0]:
  send('scale',modelSizeScale=scale);s=wait(lambda s:abs(s['modelSizeScale']-scale)<.001);a=actor(s,token);r=a['navigationRegion']
  s,box,size=capture(token,(r[0]['x']+r[2]['x'])/2,(r[0]['y']+r[2]['y'])/2,'desktop-size-'+str(scale));full_body(actor(s,token),box,size)
  check(all(math.isfinite(actor(s,token)[k]) for k in ['screenX','groundY','groundDepthScale']),'Size slider leaves finite screen coordinates',scale=scale)
 # Same retained actor switches camera/route modes without loss of proportions.
 for value in [plane,line,{'mode':'Default','line':[],'surfaces':[]},flat]:
  s=ground(value);a=actor(s,token)
  check(a['viewElevation']==(30 if value['mode']=='Desktop' else 10),'Changing mode restores the corresponding camera angle',mode=value['mode'])
  check(a['orthographicDesktop']==(value['mode']=='Desktop'),'Mode-specific projection flag follows switching',mode=value['mode'])
  check(a['desktopAttached'],'Switching grounds preserves desktop attachment',mode=value['mode'])
 # Include variants, manual entry queue, and population resizing share the normal scheduler.
 send('filter',mode='Include',includeIds=[1001],excludeIds=[]);send('actorLimit',maxActors=1)
 send('pause');s=wait(lambda s:not s['manualPaused']);s=wait(lambda s:s['count']==1 and s['actors'][0]['characterId']==1001,120)
 check(s['occupancyValid'] and s['costumesUnique'],'Desktop mode respects reduced capacity and Include selection')
 token=s['actors'][0]['token'];old=s['clickSpecials'];send('testNativeClick',token);s=wait(lambda s:s['clickSpecials']>old)
 check(actor(s,token)['state']=='Special','Click still immediately starts a special')
 send('enter',characterId=1028);wait(lambda s:len(s['pendingEntries'])>0)
 send('retire',token);s=wait(lambda s:any(a['token']==token and a['phase']=='Exiting' for a in s['actors']))
 check(actor(s,token)['groundStage']>=2,'Manual retirement walks to a chosen screen edge')
 fades=[];end=time.monotonic()+100
 while time.monotonic()<end:
  s=read()
  if all(a['token']!=token for a in s['actors']):break
  a=actor(s,token);fades.append(a['portalOpacity']);time.sleep(.09)
 check(all(a['token']!=token for a in s['actors']),'Retired actor fully leaves and releases its slot')
 check(any(.01<v<.99 for v in fades),'Real exit has gradual transparency',sampleCount=len(fades))
 s=wait(lambda s:s['count']==1 and s['actors'][0]['characterId']==1028 and not s['loading'],120)
 check(s['count']<=1 and not s['pendingEntries'],'Queued manual entry waits for a genuine vacancy')
 s=wait(lambda s:s['actors'][0]['groundStage']==1,90);token=s['actors'][0]['token'];send('pause');wait(lambda s:s['manualPaused']);a=actor(read(),token);r=a['navigationRegion']
 s,box,size=capture(token,(r[0]['x']+r[2]['x'])/2,(r[0]['y']+r[2]['y'])/2,'desktop-akebono');full_body(actor(s,token),box,size)
 check(actor(s,token)['height']>500,'Hishi Akebono keeps her larger base-size setting')
 # Test the shared avoidance switch on this plane, without overriding its safe region.
 send('avoidance',avoid3DCollisions=True);s=wait(lambda s:s['avoid3DCollisions']);send('pause');wait(lambda s:not s['manualPaused']);time.sleep(2)
 s=read();check(s['occupancyValid'] and all(math.isfinite(a['groundDepthScale']) for a in s['actors']),'Experimental avoidance remains usable on desktop plane')
 check(json.loads(config.read_text(encoding='utf-8-sig'))['Ground']['mode']=='Desktop','Desktop mode persists to settings')
 send('exit');process.wait(timeout=20);launch();s=wait(lambda s:s['count']>=1 and not s['loading'],120)
 check(s['ground']['mode']=='Desktop' and all(a['orthographicDesktop'] for a in s['actors']),'Saved desktop plane reloads after a real restart')
 report['Passed']=True
except Exception:
 report['error']=traceback.format_exc();raise
finally:
 if process is not None and process.poll() is None:
  send('exit')
  try:process.wait(timeout=20)
  except subprocess.TimeoutExpired:process.kill();process.wait()
 config.write_bytes(original)
 report.update(checks=checks,screen=[width,height],runtimeAssemblySha256=hashlib.sha256((base/'UmaDesktop_Data/Managed/umamusume.dll').read_bytes()).hexdigest().upper(),OriginalSettingsRestored=config.read_bytes()==original)
 (out/'desktop-plane-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
 (out/'desktop-plane-observations.json').write_text(json.dumps(samples,indent=2),encoding='utf-8')
