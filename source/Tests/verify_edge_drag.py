"""Replay the live Bourbon right-edge drag using the player's test pointer queue.
No system input injection. Keep a sustained gait so specials cannot hide a jam.
"""
import argparse,datetime,hashlib,json,math,subprocess,time
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--build-directory',default=str(Path(__file__).resolve().parents[2]/'build'/'UmaWalking'));p.add_argument('--ground-settings',required=True);p.add_argument('--evidence-directory',required=True);args=p.parse_args()
b=Path(args.build_directory);out=Path(args.evidence_directory);out.mkdir(parents=True,exist_ok=True);settings=b/'DesktopSettings.json';original=settings.read_bytes();grounds=json.loads(Path(args.ground_settings).read_text(encoding='utf-8-sig'))['Ground'];checks=[];samples=[];last=0;process=None;report={'Passed':False,'PhysicalMouseInjection':False};started=datetime.datetime.now(datetime.timezone.utc)
def send(action,token=0,**values):
 global last
 last=max(last+1,time.time_ns());f=b/'DesktopCommands'/(str(last)+'.tmp');f.write_text(json.dumps(dict(action=action,token=token,**values)),encoding='utf-8');f.replace(f.with_suffix('.json'))
def read():
 try:
  s=json.loads((b/'DesktopLogs/state.json').read_text(encoding='utf-8-sig'))
  if datetime.datetime.fromisoformat(s['utc'].replace('Z','+00:00'))<started:return None
  if s['error']:raise RuntimeError(s['error'])
  return s
 except(OSError,json.JSONDecodeError):return None
def wait(fn,seconds=60):
 end=time.monotonic()+seconds;s=None
 while time.monotonic()<end:
  s=read()
  if s and fn(s):samples.append(s);return s
  if process.poll() is not None:raise AssertionError('Player exited')
  time.sleep(.08)
 raise AssertionError('Timeout '+json.dumps(s))
def check(ok,label,**values):
 checks.append(dict(Passed=bool(ok),Check=label,**values));print(json.dumps(checks[-1]),flush=True)
 if not ok:raise AssertionError(label)
def inside(surface,point,tolerance=1.5):
 pts=[(p['x']*3840,p['y']*2160)for p in surface['points']];signs=[]
 for a,c in zip(pts,pts[1:]+pts[:1]):
  signs.append(((c[0]-a[0])*(point['y']-a[1])-(c[1]-a[1])*(point['x']-a[0]))/math.hypot(c[0]-a[0],c[1]-a[1]))
 return min(signs)>=-tolerance or max(signs)<=tolerance
def place(token,floor,u=.5,v=.5):
 a=read()['actors'][0];send('testPointerDown',token,groundU=a['screenX'],groundV=a['groundY']);wait(lambda s:s['actors'][0]['pointerHeld'])
 # The diagonal intersection is the projective center on each saved floor.
 pts=grounds['surfaces'][floor]['points'];a,c,d,e=pts
 ax,ay=a['x']*3840,a['y']*2160;cx,cy=c['x']*3840,c['y']*2160;dx,dy=d['x']*3840,d['y']*2160;ex,ey=e['x']*3840,e['y']*2160
 t=((cx-ax)*(ey-cy)-(cy-ay)*(ex-cx))/((dx-ax)*(ey-cy)-(dy-ay)*(ex-cx));x,y=ax+t*(dx-ax),ay+t*(dy-ay)
 send('testPointerMove',token,groundU=x,groundV=y);wait(lambda s:s['actors'][0]['groundIndex']==floor and s['actors'][0]['dragging'])
 send('testPointerUp',token,groundU=x,groundV=y);return wait(lambda s:not s['actors'][0]['pointerHeld'])
try:
 data=json.loads(original.decode('utf-8-sig'));data['Ground']=grounds;data['ModelSizeScale']=1.5;settings.write_text(json.dumps(data),encoding='utf-8')
 si=subprocess.STARTUPINFO();si.dwFlags|=subprocess.STARTF_USESHOWWINDOW;si.wShowWindow=0
 process=subprocess.Popen([str(b/'UmaDesktop.exe'),'-force-d3d11','-force-d3d11-bitblt-model','--desktop-test-mode','--desktop-test-ignore-occlusion','--desktop-no-controller','--desktop-test-stable-population','--desktop-test-long-gait','--desktop-max-actors','1','-logFile',str(b/'DesktopLogs/Player-edge-drag.log')],cwd=b,startupinfo=si)
 s=wait(lambda s:s['count']==1 and not s['loading']);old=s['actors'][0]['token'];send('testCharacter',old,characterId=1026,costumeId='00');s=wait(lambda s:s['count']==1 and not s['loading'] and s['actors'][0]['token']!=old and s['actors'][0]['characterId']==1026);token=s['actors'][0]['token']
 for trial,(gait,clip) in enumerate([('Run','run03_loop'),('Run','run00_loop'),('Walk','walk01_loop'),('Walk','walk02_loop')]):
  place(token,0);send('testGait',token,mode=gait,costumeId='3d/motion/mini/event/body/type00/anm_min_eve_type00_'+clip);wait(lambda s:s['actors'][0]['state']==gait and s['actors'][0]['clip'].endswith(clip))
  a=read()['actors'][0];send('testPointerDown',token,groundU=a['screenX'],groundV=a['groundY']);wait(lambda s:s['actors'][0]['pointerHeld'])
  # Drag beyond the rightmost edge of the narrow saved platform.
  send('testPointerMove',token,groundU=6000,groundV=1860);wait(lambda s:s['actors'][0]['dragging'] and s['actors'][0]['groundIndex']==1)
  send('testPointerUp',token,groundU=6000,groundV=1860);s=wait(lambda s:not s['actors'][0]['pointerHeld']);start=s['actors'][0];rows=[];end=time.monotonic()+14
  while time.monotonic()<end:
   s=read()
   if not s:time.sleep(.05);continue
   a=s['actors'][0];samples.append(s);rows.append(a);time.sleep(.35)
  distance=max(math.hypot(a['screenX']-start['screenX'],a['groundY']-start['groundY'])for a in rows)
  check(all(a['phase']=='Active' and a['groundStage']==1 for a in rows),'Cross-floor edge drop remains active',trial=trial)
  check(all(a['portalOpacity']==1 and a['opacity']==1 for a in rows),'Active edge actor stays fully opaque',trial=trial)
  check(all(a['state']==gait and a['clip'].endswith(clip)for a in rows),'A sustained original gait was exercised without special interruptions',trial=trial)
  check(distance>(150 if gait=='Run' else 45),'Actor makes sustained progress away from dropped edge',trial=trial,distance=distance)
  check(all(all(inside(grounds['surfaces'][1],p)for p in a['footContacts'])for a in rows),'Recovery retains both shoes within the target ground',trial=trial)
  check(all(rows[i]['groundFitScale']<=rows[i-1]['groundFitScale']+.0001 for i in range(1,len(rows))),'Ground fit no longer expands and contracts every animation frame',trial=trial)
  check(all(a['desktopAttached']for a in rows),'Recovered model stays in Explorer desktop',trial=trial)
  send('capture');time.sleep(.3)
  f=b/f'DesktopLogs/capture-{token}.png'
  if f.exists():(out/f'edge-drag-{trial}.png').write_bytes(f.read_bytes())
 # A model already retiring must still leave, including after another floor drag.
 departureStart=time.monotonic();send('next',token);wait(lambda s:s['actors'][0]['phase']=='Exiting');place(token,0);place(token,1)
 wait(lambda s:s['departures']>0,80);check(True,'Dragging an exiting actor across grounds still completes the exit')
 check(time.monotonic()-departureStart<20,'Portal fading does not slow the entire diagonal exit approach',seconds=time.monotonic()-departureStart)
 report['Passed']=True
finally:
 if process is not None and process.poll() is None:
  send('exit')
  try:process.wait(timeout=15)
  except subprocess.TimeoutExpired:process.kill();process.wait()
 settings.write_bytes(original);report.update(checks=checks,runtimeAssemblySha256=hashlib.sha256((b/'UmaDesktop_Data/Managed/umamusume.dll').read_bytes()).hexdigest().upper(),OriginalSettingsRestored=settings.read_bytes()==original)
 (out/'edge-drag-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8');(out/'edge-drag-observations.json').write_text(json.dumps(samples,indent=2),encoding='utf-8')
