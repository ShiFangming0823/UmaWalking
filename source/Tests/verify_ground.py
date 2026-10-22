"""Real player / native-window integration. Restores preferences, never injects OS input."""
import argparse,ctypes,datetime,hashlib,json,subprocess,time,shutil
from ctypes import wintypes
from pathlib import Path
from PIL import Image

parser=argparse.ArgumentParser()
parser.add_argument('--build-directory',default=str(Path(__file__).resolve().parents[2]/'build'/'UmaWalking'))
parser.add_argument('--evidence-directory',required=True)
args=parser.parse_args();base=Path(args.build_directory);out=Path(args.evidence_directory);out.mkdir(parents=True,exist_ok=True)
settings=base/'DesktopSettings.json';original=settings.read_bytes();checks=[];samples=[];process=None;last=0
started=datetime.datetime.now(datetime.timezone.utc)
u=ctypes.WinDLL('user32');u.SetThreadDpiAwarenessContext.argtypes=[wintypes.HANDLE];u.SetThreadDpiAwarenessContext(wintypes.HANDLE(-4))
u.GetWindowRect.argtypes=[wintypes.HWND,ctypes.POINTER(wintypes.RECT)]
u.GetWindow.argtypes=[wintypes.HWND,wintypes.UINT];u.GetWindow.restype=wintypes.HWND
def check(ok,label,**values):
 row=dict(Passed=bool(ok),Check=label,**values);checks.append(row);print(json.dumps(row),flush=True)
 if not ok:raise AssertionError(label)
def read():
 try:
  s=json.loads((base/'DesktopLogs/state.json').read_text(encoding='utf-8-sig'))
  if datetime.datetime.fromisoformat(s['utc'].replace('Z','+00:00'))<started:return None
  if s['error']:raise RuntimeError(s['error'])
  return s
 except (OSError,json.JSONDecodeError):return None
def wait(predicate,seconds=45):
 end=time.monotonic()+seconds;s=None
 while time.monotonic()<end:
  s=read()
  if s and predicate(s):samples.append(s);return s
  if process.poll() is not None:raise AssertionError('Runtime exited')
  time.sleep(.08)
 raise AssertionError('Timeout: '+json.dumps(s))
def send(action,token=0,**values):
 global last
 last=max(last+1,time.time_ns());f=base/'DesktopCommands'/(str(last)+'.tmp');f.write_text(json.dumps(dict(action=action,token=token,**values)),encoding='utf-8');f.replace(f.with_suffix('.json'))
def actor(s,token):return next(a for a in s['actors'] if a['token']==token)
def plane(x0=.12,x1=.88,y0=.5,y1=.92):
 return {'points':[{'x':x0+.07,'y':y0},{'x':x1-.07,'y':y0},{'x':x1,'y':y1},{'x':x0,'y':y1}], 'entrance':{'edge':3,'start':.35,'end':.65},'exit':{'edge':1,'start':.35,'end':.65}}
def apply(config):
 send('ground',ground=config)
 return wait(lambda s:s['ground']==config and not s['loading'],60)
def config_mode(s,mode):return s['ground']['mode']==mode
def pose_capture(token,uu,vv,label):
 path=base/f'DesktopLogs/capture-{token}.png';version=actor(read(),token).get('poseVersion',0);stamp=time.time_ns();send('capture');send('testGroundPose',token,groundU=uu,groundV=vv)
 s=wait(lambda s:actor(s,token).get('poseVersion',0)>version)
 end=time.monotonic()+12
 while time.monotonic()<end:
  try:
   if path.stat().st_mtime_ns>=stamp:
    im=Image.open(path).convert('RGBA');box=im.getchannel('A').getbbox()
    if box:im.save(out/(label+'.png'));return s,box
  except OSError:pass
  time.sleep(.1)
 raise AssertionError('Missing rendered capture '+label)

report={'Passed':False}
try:
 data=json.loads(original.decode('utf-8-sig'));data['Ground']={'mode':'Default','line':[],'surfaces':[]};settings.write_text(json.dumps(data),encoding='utf-8')
 si=subprocess.STARTUPINFO();si.dwFlags|=subprocess.STARTF_USESHOWWINDOW;si.wShowWindow=0
 process=subprocess.Popen([str(base/'UmaDesktop.exe'),'-force-d3d11','-force-d3d11-bitblt-model','--desktop-test-mode','--desktop-test-ignore-occlusion','--desktop-no-controller','-logFile',str(base/'DesktopLogs/Player-ground-integration.log')],cwd=base,startupinfo=si)
 s=wait(lambda s:s['count']>=2 and not s['loading'],60);check(s['ground']['mode']=='Default','Legacy bottom ground remains default')
 send('groundEditBegin');s=wait(lambda s:s['groundEditing'] and s['paused']);stamp=s['simulationSeconds'];time.sleep(1.2);s=read()
 check(s['simulationSeconds']==stamp,'Drawing pauses population and motion')
 send('groundEditEnd');wait(lambda s:not s['groundEditing'] and not s['paused']);check(True,'Canceling editor resumes animation')
 line={'mode':'Line','line':[{'x':.1,'y':.8},{'x':.9,'y':.7}],'surfaces':[]}
 send('ground',ground=line);s=wait(lambda s:config_mode(s,'Line') and not s['loading']);time.sleep(2);s=read()
 screen_h=2160;screen_w=3840
 # Get the actual primary display dimensions with DPI virtualization disabled.
 screen_w=u.GetSystemMetrics(0);screen_h=u.GetSystemMetrics(1)
 for a in s['actors']:
  expected=(.8+(.7-.8)*a['groundU'])*screen_h
  check(abs(a['groundY']-expected)<1,'Line feet follow the drawn slope',token=a['token'])
 send('pause');wait(lambda s:s['manualPaused'])
 three={'mode':'Planes','line':line['line'],'surfaces':[plane(.04,.38,.45,.78),plane(.35,.72,.52,.86),plane(.63,.96,.6,.94)]}
 observed=set()
 for repeat in range(10):
  send('ground',ground=three);s=wait(lambda s:config_mode(s,'Planes') and len(s['ground']['surfaces'])==3 and not s['loading']);time.sleep(.55);s=read()
  observed.update(a['groundIndex'] for a in s['actors'])
 check(observed=={0,1,2},'Spawns and reassignments can choose all three surfaces',indices=sorted(observed))
 check(all(a['desktopAttached'] and a['desktopParent'] for a in s['actors']),'Custom ground models remain attached to Explorer desktop')
 check(all(a['groundStage']==0 for a in s['actors']),'New floor assignments start at their entrance')
 saved=json.loads(settings.read_text(encoding='utf-8-sig'));check(saved['Ground']['mode']=='Planes' and len(saved['Ground']['surfaces'])==3,'Ground configuration persists to disk')
 invalid=json.loads(json.dumps(three));invalid['surfaces'].append(plane());send('ground',ground=invalid);time.sleep(.8);s=read();check(len(s['ground']['surfaces'])==3,'Runtime rejects a fourth surface')
 single={'mode':'Planes','line':line['line'],'surfaces':[plane()]};send('ground',ground=single);wait(lambda s:len(s['ground']['surfaces'])==1)
 token=s['actors'][0]['token']
 # Frozen identical source pose isolates projection from gait/animation differences.
 s,far=pose_capture(token,.5,.15,'ground-far');s,near=pose_capture(token,.5,.85,'ground-near')
 ratio=(near[3]-near[1])/(far[3]-far[1]);check(ratio>1.12,'Real rendered model grows toward near edge',heightRatio=ratio,far=far,near=near)
 a=actor(s,token);r=wintypes.RECT();u.GetWindowRect(a['nativeWindow'],ctypes.byref(r))
 check(abs(r.top-(a['groundY']-(r.bottom-r.top)*(1-a['canvasFootViewport'])))<2,'Native desktop output follows projected ground anchor')
 old=s['clickSpecials'];send('testNativeClick',token);send('pause');s=wait(lambda s:s['clickSpecials']>old);check(actor(s,token)['state']=='Special','Click-triggered special still works on custom ground')
 send('next',token);s=wait(lambda s:all(a['token']!=token for a in s['actors']),45);check(True,'Character reaches configured exit and is removed')
 seen=set();end=time.monotonic()+18
 while time.monotonic()<end:
  s=read()
  if s:
   samples.append(s)
   check(s['count']<=6 and s['namesUnique'],'Global cap and unique names preserved')
   for a in s['actors']:
    if a['phase']=='Active':check(0<=a['groundU']<=1 and 0<=a['groundV']<=1,'Active feet stay within plane')
    seen.add(a['groundStage'])
  time.sleep(1)
 check(all(a['presentedFrames']>10 for a in s['actors']),'Custom projection continues presenting native frames')
 # Setting remains valid after an actual fresh player startup.
 send('exit');process.wait(timeout=15)
 started=datetime.datetime.now(datetime.timezone.utc)
 process=subprocess.Popen([str(base/'UmaDesktop.exe'),'-force-d3d11','-force-d3d11-bitblt-model','--desktop-test-mode','--desktop-test-ignore-occlusion','--desktop-no-controller','--desktop-max-actors','1','-logFile',str(base/'DesktopLogs/Player-ground-reload.log')],cwd=base,startupinfo=si)
 s=wait(lambda s:s['count']==1 and not s['loading'],60);check(s['ground']['mode']=='Planes' and s['actors'][0]['groundIndex']==0,'Saved plane reloads after restarting player')
 send('ground',ground={'mode':'Default','line':[],'surfaces':[]});s=wait(lambda s:config_mode(s,'Default'));check(s['actors'][0]['groundIndex']==-1,'Restore bottom ground restores legacy renderer and path')
 text=(base/'DesktopLogs/Player-ground-integration.log').read_text(encoding='utf-8-sig');check('Exception:' not in text and 'ERROR ' not in text,'No runtime or graphics exceptions')
 report['Passed']=True
finally:
 if process is not None and process.poll() is None:
  send('exit')
  try:process.wait(timeout=15)
  except subprocess.TimeoutExpired:process.kill();process.wait()
 settings.write_bytes(original)
 report.update(checks=checks,runtimeAssemblySha256=hashlib.sha256((base/'UmaDesktop_Data/Managed/umamusume.dll').read_bytes()).hexdigest().upper(),physicalMouseVerified=False)
 (out/'ground-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
 (out/'ground-observations.json').write_text(json.dumps(samples,indent=2),encoding='utf-8')
