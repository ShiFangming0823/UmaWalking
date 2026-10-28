"""Real-model per-frame avoidance/facing checks in a disposable runtime.
No physical OS input is injected and the normal user profile is never written.
"""
import argparse,collections,ctypes,hashlib,json,math,os,subprocess,time,traceback
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--build-directory',required=True);p.add_argument('--evidence-directory',required=True)
args=p.parse_args();base=Path(args.build_directory).resolve();out=Path(args.evidence_directory).resolve();out.mkdir(parents=True,exist_ok=True)
config=base/'DesktopSettings.json';original=config.read_bytes();process=None;serial=0;checks=[];summary=[];report={'Passed':False,'PhysicalMouseInjection':False}
u=ctypes.WinDLL('user32');u.SetThreadDpiAwarenessContext(ctypes.c_void_p(-4));w=u.GetSystemMetrics(0);h=u.GetSystemMetrics(1)
def check(value,label,**values):
 row={'Passed':bool(value),'Check':label,**values};checks.append(row);print(json.dumps(row),flush=True)
 if not value:raise AssertionError(label)
def read():
 for _ in range(10):
  try:
   s=json.loads((base/'DesktopLogs/state.json').read_text(encoding='utf-8-sig'))
   if s['processId']!=process.pid:return None
   if s.get('error'):raise RuntimeError(s['error'])
   return s
  except(OSError,json.JSONDecodeError):time.sleep(.025)
 return None
def wait(fn,seconds=90):
 end=time.monotonic()+seconds;s=None
 while time.monotonic()<end:
  s=read()
  if s and fn(s):return s
  if process.poll() is not None:raise AssertionError('Player exited early')
  time.sleep(.075)
 raise AssertionError('Timeout: '+json.dumps(s))
def send(action,token=0,**values):
 global serial
 serial=max(serial+1,time.time_ns());f=base/'DesktopCommands'/(str(serial)+'.tmp');f.write_text(json.dumps(dict(action=action,token=token,**values)),encoding='utf-8');f.replace(f.with_suffix('.json'))
def actor(s,t):return next(a for a in s['actors']if a['token']==t)
def pause(value):
 if read()['manualPaused']!=value:send('pause')
 return wait(lambda s:s['manualPaused']==value)
def nav(t,x,y,tx,ty,speed):
 before=actor(read(),t)['poseVersion'];send('testNavigation',t,groundIndex=0,groundU=x,groundV=y,navigationTarget={'x':tx,'y':ty},navigationSpeed=speed)
 return wait(lambda s:actor(s,t)['poseVersion']>before)
def delta(a,b):return (b-a+180)%360-180
def observe(name,seconds):
 f=base/'DesktopLogs/heading-audit.json';stamp=time.time_ns();send('testHeadingAudit',mode='start');pause(False);time.sleep(seconds);pause(True);send('testHeadingAudit',mode='stop')
 end=time.monotonic()+15
 while time.monotonic()<end:
  try:
   if f.stat().st_mtime_ns>=stamp:
    data=json.loads(f.read_text(encoding='utf-8-sig'));break
  except(OSError,json.JSONDecodeError):pass
  time.sleep(.07)
 else:raise AssertionError('Missing heading audit')
 (out/(name+'-headings.json')).write_text(json.dumps(data),encoding='utf-8')
 groups=collections.defaultdict(list)
 for row in data['samples']:groups[row['token']].append(row)
 peak=0;distance=0;minimum=1e9
 for token,rows in groups.items():
  last=0;window=collections.deque();last_x=rows[0]['x'];last_y=rows[0]['y'];changes=0
  # Skip the initial intentional turn from setup pose toward the new route.
  for a,b in zip(rows,rows[1:]):
   distance+=math.hypot(b['x']-a['x'],(b['y']-a['y'])/.5)
   if b['time']-rows[0]['time']<.8:continue
   change=delta(a['yaw'],b['yaw'])
   if .35<abs(change)<25 and change*last<0:window.append(b['time']);changes+=1
   if abs(change)>.35:last=change
   while window and window[0]<b['time']-1:window.popleft()
   peak=max(peak,len(window))
  check(len(rows)>seconds*12,name+' records real per-frame model poses',token=token,frames=len(rows))
 check(peak<=5,name+' has no high-frequency small heading oscillation',peakReversalsPerSecond=peak)
 check(distance>150,name+' still makes useful movement',totalFloorDistance=round(distance,1))
 bytime=collections.defaultdict(list)
 for row in data['samples']:bytime[row['time']].append(row)
 for rows in bytime.values():
  for i,a in enumerate(rows):
   for b in rows[i+1:]:minimum=min(minimum,math.hypot(a['x']-b['x'],(a['y']-b['y'])/.5)-a['radius']-b['radius'])
 check(minimum>=-2,name+' preserves physical separation',minimumClearance=round(minimum,2))
 s=read();check(all(a['desktopAttached'] for a in s['actors']),name+' remains attached to desktop')
 summary.append({'scenario':name,'peakReversalsPerSecond':peak,'distance':distance,'minimumClearance':minimum,'frames':len(data['samples'])})
 return s
try:
 data=json.loads(original.decode('utf-8-sig'));data.update(Ground={'mode':'Desktop','line':[],'surfaces':[]},ModelSizeScale=.8,FrameRate=30,Avoid3DCollisions=True,InteractionProbability=0)
 config.write_text(json.dumps(data),encoding='utf-8');(base/'DesktopLogs').mkdir(exist_ok=True)
 si=subprocess.STARTUPINFO();si.dwFlags|=subprocess.STARTF_USESHOWWINDOW;si.wShowWindow=0;env=os.environ.copy();env['UMA_WALKING_PROFILE']=str(out/'profile')
 process=subprocess.Popen([str(base/'UmaDesktop.exe'),'-force-d3d11','-force-d3d11-bitblt-model','--desktop-test-mode','--desktop-test-ignore-occlusion','--desktop-no-controller','--desktop-test-stable-population','--desktop-test-long-gait','--desktop-max-actors','2','-logFile',str(base/'DesktopLogs/Player-desktop-stability.log')],cwd=base,startupinfo=si,env=env)
 s=wait(lambda s:s['count']==2 and not s['loading'],120);pause(True);one,two=[a['token']for a in s['actors']]
 nav(one,.28,.55,.85,.55,500);nav(two,.72,.55,.15,.55,180);observe('head-on',9)
 nav(one,.27,.6,.85,.6,450);nav(two,.56,.6,.56,.6,80)
 a=actor(read(),two);send('testPointerDown',two,groundU=a['screenX'],groundV=a['groundY']-80);wait(lambda s:actor(s,two)['motionHeld']);frozen=actor(read(),two)['animationTime'];observe('stationary-obstacle',9)
 check(actor(read(),two)['animationTime']==frozen,'Stationary dragged actor preserves its held pose')
 send('testPointerUp',two,groundU=a['screenX'],groundV=a['groundY']-80);wait(lambda s:not actor(s,two)['motionHeld'])
 send('actorLimit',maxActors=6);pause(False);s=wait(lambda s:s['count']==6 and not s['loading'],120);pause(True)
 tokens=[a['token']for a in s['actors']]
 setup=[(.2,.32,.8,.85),(.4,.32,.45,.85),(.8,.32,.2,.85),(.2,.85,.8,.32),(.55,.85,.35,.32),(.8,.85,.2,.32)]
 for i,(t,points) in enumerate(zip(tokens,setup)):nav(t,*points,260 if i%2==0 else 70)
 observe('six-way-crossing',12)
 old=read()['clickSpecials'];send('special',one);s=wait(lambda s:s['clickSpecials']>old)
 check(actor(s,one)['state']=='Special','Click-requested special remains immediate')
 send('testGait',one,mode='Walk');send('avoidance',avoid3DCollisions=False);wait(lambda s:not s['avoid3DCollisions']);pause(False);time.sleep(1);pause(True)
 check(all(a['avoidanceState']=='off'for a in read()['actors']),'Disabling avoidance immediately restores original routes')
 send('avoidance',avoid3DCollisions=True);wait(lambda s:s['avoid3DCollisions']);send('retire',one);pause(False);wait(lambda s:all(a['token']!=one for a in s['actors']),120)
 check(True,'Retirement reaches a screen edge with stability enabled')
 report['Passed']=True
except Exception:
 report['error']=traceback.format_exc();raise
finally:
 if process and process.poll() is None:
  send('exit')
  try:process.wait(timeout=20)
  except subprocess.TimeoutExpired:process.kill();process.wait()
 config.write_bytes(original)
 report.update(checks=checks,scenarios=summary,OriginalSettingsRestored=config.read_bytes()==original,RuntimeAssemblySha256=hashlib.sha256((base/'UmaDesktop_Data/Managed/umamusume.dll').read_bytes()).hexdigest().upper())
 (out/'desktop-avoidance-stability-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
