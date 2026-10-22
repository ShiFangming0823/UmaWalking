"""Deterministic real-player navigation checks. Uses test commands only, no OS input."""
import argparse,datetime,hashlib,json,subprocess,time,math
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--build-directory',default=str(Path(__file__).resolve().parents[2]/'build'/'UmaWalking'));p.add_argument('--evidence-directory',required=True);args=p.parse_args()
b=Path(args.build_directory);out=Path(args.evidence_directory);out.mkdir(parents=True,exist_ok=True);settings=b/'DesktopSettings.json';original=settings.read_bytes();checks=[];samples=[];last=0;process=None;started=datetime.datetime.now(datetime.timezone.utc);report={'Passed':False}
plane={'mode':'Planes','line':[],'surfaces':[{'points':[{'x':.05,'y':.2},{'x':.95,'y':.2},{'x':.95,'y':.9},{'x':.05,'y':.9}],'entrance':{'edge':3,'start':.35,'end':.65},'exit':{'edge':1,'start':.35,'end':.65}}]}
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
def wait(fn,seconds=65):
 end=time.monotonic()+seconds;s=None
 while time.monotonic()<end:
  s=read()
  if s and fn(s):return s
  if process.poll() is not None:raise AssertionError('Player exited')
  time.sleep(.08)
 raise AssertionError('Timeout '+json.dumps(s))
def check(ok,label,**values):
 row=dict(Passed=bool(ok),Check=label,**values);checks.append(row);print(json.dumps(row),flush=True)
 if not ok:raise AssertionError(label)
def actor(s,t):return next(a for a in s['actors']if a['token']==t)
def pause(value):
 s=wait(lambda s:True);before=s['simulationSeconds']
 if s['manualPaused']!=value:send('pause')
 return wait(lambda s:s['manualPaused']==value and (value or s['simulationSeconds']>before+.1))
def nav(t,u,v,tu,tv,speed=400,index=0):
 before=actor(wait(lambda s:True),t)['poseVersion'];send('testNavigation',t,groundU=u,groundV=v,groundIndex=index,navigationTarget={'x':tu,'y':tv},navigationSpeed=speed)
 return wait(lambda s:actor(s,t)['poseVersion']>before)
def observe(seconds):
 end=time.monotonic()+seconds;rows=[];stamp=''
 while time.monotonic()<end:
  s=read()
  if s and not s['paused'] and s['utc']!=stamp:stamp=s['utc'];rows.append(s);samples.append(s)
  time.sleep(.07)
 return rows
def validate(rows,label):
 minimum=1e9
 for s in rows:
  aa=s['actors']
  for a in aa:
   if a['groundStage']==1:assert 0<=a['groundU']<=1 and 0<=a['groundV']<=1,(label,a)
  if len(aa)==2 and aa[0]['groundIndex']==aa[1]['groundIndex']:
   a,c=aa;distance=math.hypot(a['screenX']-c['screenX'],(a['groundY']-c['groundY'])/math.sin(math.radians(10)));margin=distance-a['avoidanceRadius']-c['avoidanceRadius'];minimum=min(minimum,margin)
 check(minimum>=-2,label+' keeps ground-body separation',minimumClearance=round(minimum,2))
 check(all(a['desktopAttached'] for s in rows for a in s['actors']),label+' stays on desktop')
 return minimum
try:
 data=json.loads(original.decode('utf-8-sig'));data.update(Ground=plane,ModelSizeScale=.65,FrameRate=15,Avoid3DCollisions=True,InteractionProbability=0)
 settings.write_text(json.dumps(data),encoding='utf-8');si=subprocess.STARTUPINFO();si.dwFlags|=subprocess.STARTF_USESHOWWINDOW;si.wShowWindow=0
 process=subprocess.Popen([str(b/'UmaDesktop.exe'),'-force-d3d11','-force-d3d11-bitblt-model','--desktop-test-mode','--desktop-test-ignore-occlusion','--desktop-no-controller','--desktop-test-stable-population','--desktop-test-long-gait','--desktop-max-actors','2','-logFile',str(b/'DesktopLogs/Player-avoidance.log')],cwd=b,startupinfo=si)
 s=wait(lambda s:s['count']==2 and not s['loading']);one,two=[a['token']for a in s['actors']];check(s['avoid3DCollisions'],'Explicitly enabled 3D avoidance is active')
 for label,start1,start2,speed in [('head-on',(.4,.5,.9,.5),(.6,.5,.1,.5),600),('crossing',(.4,.5,.9,.5),(.5,.44,.5,.8),400),('overtaking',(.36,.5,.9,.5),(.48,.5,.9,.5),500)]:
  pause(True);nav(one,*start1,speed);nav(two,*start2,90 if label=='overtaking' else speed)
  before=wait(lambda s:True);pause(False);rows=observe(4);pause(True);validate(rows,label)
  check(any(a['avoidanceFrames']>actor(before,a['token'])['avoidanceFrames']for s in rows for a in s['actors']),label+' triggers detour or turn')
  check(any(math.hypot(actor(s,one)['screenX']-actor(before,one)['screenX'],actor(s,one)['groundY']-actor(before,one)['groundY'])>100 for s in rows),label+' continues useful movement')
 # A held model is a stationary obstacle, and the held pose remains exact.
 nav(one,.4,.5,.9,.5,500);nav(two,.57,.5,.6,.5,100);a=actor(wait(lambda s:True),two);send('testPointerDown',two,groundU=round(a['screenX']),groundV=round(a['groundY']-80));wait(lambda s:actor(s,two)['motionHeld']);pause(False);rows=observe(4);pause(True);validate(rows,'held obstacle')
 check(len({(actor(s,two)['animationTime'],actor(s,two)['groundU'],actor(s,two)['groundV'])for s in rows})==1,'Held model keeps exact pose and position while others avoid it')
 send('testPointerUp',two,groundU=round(a['screenX']),groundV=round(a['groundY']-80));wait(lambda s:not actor(s,two)['motionHeld'])
 click_before=wait(lambda s:True)['clickSpecials'];send('special',one);s=wait(lambda s:s['clickSpecials']>click_before);check(actor(s,one)['state']=='Special','Click immediately starts special despite navigation');send('testGait',one,mode='Walk')
 # Switch off: the previous straight routes are restored; no detour or stop.
 send('avoidance',avoid3DCollisions=False);wait(lambda s:not s['avoid3DCollisions']);nav(one,.4,.5,.9,.5,600);nav(two,.6,.5,.1,.5,600);pause(False);rows=observe(2.5);pause(True)
 check(all(a['avoidanceState']=='off'for s in rows for a in s['actors']),'Disabled switch bypasses planner for all actors')
 check(actor(rows[-1],one)['screenX']>actor(rows[-1],two)['screenX'],'Disabled actors retain original straight paths through meeting')
 check(json.loads(settings.read_text(encoding='utf-8-sig'))['Avoid3DCollisions'] is False,'Switch persists to saved settings')
 send('avoidance',avoid3DCollisions=True);wait(lambda s:s['avoid3DCollisions']);nav(one,.4,.5,.9,.5,600);nav(two,.6,.5,.1,.5,600);pause(False);rows=observe(3);pause(True);validate(rows,'re-enabled')
 # Opposing models on separate floors do not influence each other.
 twofloors=json.loads(json.dumps(plane));twofloors['surfaces'].append(json.loads(json.dumps(plane['surfaces'][0])));send('ground',ground=twofloors);wait(lambda s:len(s['ground']['surfaces'])==2);nav(one,.4,.5,.9,.5,500,0);nav(two,.6,.5,.1,.5,500,1);pause(False);rows=observe(2);pause(True)
 check(all(a['avoidanceState']=='clear'for s in rows for a in s['actors']),'Different floor indices are independent even when projected floors overlap')
 # 2D path and screen-bottom ground bypass the new planner entirely.
 for mode in ['Line','Default']:
  send('ground',ground={'mode':mode,'line':[{'x':.1,'y':.8},{'x':.9,'y':.8}],'surfaces':[]});wait(lambda s:s['ground']['mode']==mode);pause(False);rows=observe(1.1);pause(True)
  check(all(a['avoidanceState']=='off'for s in rows for a in s['actors']),mode+' bypasses 3D collision planning')
 # Graceful retirement still reaches its original exit with navigation enabled.
 send('ground',ground=plane);wait(lambda s:s['ground']['mode']=='Planes');nav(one,.8,.5,.9,.5,500);nav(two,.2,.8,.3,.8,100);send('retire',one);pause(False);s=wait(lambda s:all(a['token']!=one for a in s['actors']),30);check(s['departures']>=1,'Retiring actor walks through exit and is removed')
 report['Passed']=True
finally:
 if process and process.poll() is None:
  send('exit')
  try:process.wait(timeout=15)
  except subprocess.TimeoutExpired:process.kill();process.wait()
 settings.write_bytes(original);report.update(checks=checks,RuntimeAssemblySha256=hashlib.sha256((b/'UmaDesktop_Data/Managed/umamusume.dll').read_bytes()).hexdigest().upper(),OriginalSettingsRestored=settings.read_bytes()==original,PhysicalMouseInjection=False)
 (out/'avoidance-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8');(out/'avoidance-observations.json').write_text(json.dumps(samples,indent=2),encoding='utf-8')
