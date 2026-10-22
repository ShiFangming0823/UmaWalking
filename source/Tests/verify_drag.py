"""Player integration using its test-only pointer-event queue, with native hook routing
tested separately by Test-NativeInput.ps1. No system mouse input is injected.
"""
import argparse,datetime,hashlib,json,subprocess,time,math
from pathlib import Path

p=argparse.ArgumentParser();p.add_argument('--build-directory',default=str(Path(__file__).resolve().parents[2]/'build'/'UmaWalking'));p.add_argument('--evidence-directory',required=True)
args=p.parse_args();base=Path(args.build_directory);out=Path(args.evidence_directory);out.mkdir(parents=True,exist_ok=True)
settings=base/'DesktopSettings.json';original=settings.read_bytes();checks=[];samples=[];process=None;last=0;report={'Passed':False};started=datetime.datetime.now(datetime.timezone.utc)
def check(ok,label,**values):
 row=dict(Passed=bool(ok),Check=label,**values);checks.append(row);print(json.dumps(row),flush=True)
 if not ok:raise AssertionError(label)
def send(action,token=0,**values):
 global last
 last=max(last+1,time.time_ns());f=base/'DesktopCommands'/(str(last)+'.tmp');f.write_text(json.dumps(dict(action=action,token=token,**values)),encoding='utf-8');f.replace(f.with_suffix('.json'))
def read():
 try:
  s=json.loads((base/'DesktopLogs/state.json').read_text(encoding='utf-8-sig'))
  if datetime.datetime.fromisoformat(s['utc'].replace('Z','+00:00'))<started:return None
  if s['error']:raise RuntimeError(s['error'])
  return s
 except(OSError,json.JSONDecodeError):return None
def wait(fn,seconds=45):
 end=time.monotonic()+seconds;s=None
 while time.monotonic()<end:
  s=read()
  if s and fn(s):samples.append(s);return s
  if process.poll() is not None:raise AssertionError('Player exited')
  time.sleep(.07)
 raise AssertionError('Timeout: '+json.dumps(s))
def actor(s,token):return next(a for a in s['actors'] if a['token']==token)
def point_action(action,token,x,y):send(action,token,groundU=x,groundV=y)
try:
 data=json.loads(original.decode('utf-8-sig'))
 # Use two widely separated floors, including a diagonal narrow regression shape.
 floors=[{'points':[{'x':.08,'y':.4},{'x':.38,'y':.42},{'x':.4,'y':.9},{'x':.04,'y':.85}],'entrance':{'edge':3,'start':.35,'end':.65},'exit':{'edge':1,'start':.35,'end':.65}}, {'points':[{'x':.55,'y':.5},{'x':.61,'y':.49},{'x':.97,'y':.72},{'x':.97,'y':.82}],'entrance':{'edge':1,'start':.3,'end':.6},'exit':{'edge':1,'start':.3,'end':.6}}]
 data['Ground']={'mode':'Planes','line':[],'surfaces':floors};data['ModelSizeScale']=1;settings.write_text(json.dumps(data),encoding='utf-8')
 si=subprocess.STARTUPINFO();si.dwFlags|=subprocess.STARTF_USESHOWWINDOW;si.wShowWindow=0
 process=subprocess.Popen([str(base/'UmaDesktop.exe'),'-force-d3d11','-force-d3d11-bitblt-model','--desktop-test-mode','--desktop-test-ignore-occlusion','--desktop-no-controller','-logFile',str(base/'DesktopLogs/Player-drag.log')],cwd=base,startupinfo=si)
 s=wait(lambda s:s['count']>=2 and not s['loading'],60);token=s['actors'][0]['token'];other=s['actors'][1]['token']
 send('pause');wait(lambda s:s['manualPaused']);send('testGroundPose',token,groundU=.5,groundV=.5);s=wait(lambda s:abs(actor(s,token)['groundU']-.5)<.001)
 a=actor(s,token);downx=round(a['screenX']);downy=round(a['groundY']-120)
 send('special',token);point_action('testPointerDown',token,downx,downy);s=wait(lambda s:actor(s,token)['pointerHeld']);send('pause');s=wait(lambda s:not s['manualPaused']);a=actor(s,token)
 frozen=(a['animationTime'],a['specialElapsed'],a['expressionSelections'],a['clip']);clicks=s['clickSpecials'];evaluations=actor(s,other)['animationTime'];time.sleep(1.2);s=read();a=actor(s,token)
 check((a['animationTime'],a['specialElapsed'],a['expressionSelections'],a['clip'])==frozen,'Press freezes animation, special timer and expression without replacing action')
 check(actor(s,other)['animationTime']!=evaluations,'Other characters continue animating')
 # First move far outside every floor. Its foot must still be on a floor.
 point_action('testPointerMove',token,-1000,-1000);s=wait(lambda s:actor(s,token)['dragging']);a=actor(s,token)
 check(.0399<=a['groundU']<=.9601 and .0399<=a['groundV']<=.9601,'Dragging beyond screen clamps feet within a ground')
 old_index=a['groundIndex'];target_index=1-old_index
 # Compute a projective floor interior from the intersection of diagonals.
 def center(surface):
  pts=[(p['x']*3840,p['y']*2160)for p in surface['points']];ax,ay=pts[0];bx,by=pts[2];cx,cy=pts[1];dx,dy=pts[3]
  den=(bx-ax)*(dy-cy)-(by-ay)*(dx-cx);t=((cx-ax)*(dy-cy)-(cy-ay)*(dx-cx))/den
  return ax+t*(bx-ax),ay+t*(by-ay)
 x,y=center(floors[target_index]);point_action('testPointerMove',token,x,y-120);s=wait(lambda s:actor(s,token)['groundIndex']==target_index);a=actor(s,token)
 check(a['dragging'] and a['pointerHeld'],'Dragging crosses the gap to another ground')
 check(.0399<=a['groundU']<=.9601 and .0399<=a['groundV']<=.9601,'Cross-ground drop remains within destination')
 check((a['animationTime'],a['specialElapsed'],a['expressionSelections'],a['clip'])==frozen,'Crossing floors keeps the exact frozen action')
 check(a['opacity']==1 and a['portalOpacity']==1,'Dragged model stays visible at the boundary')
 drop=(a['screenX'],a['groundY'])
 check(math.hypot(drop[0]-downx,drop[1]-(downy+120))>300,'Long-distance drag moves the model hundreds of screen pixels')
 point_action('testPointerMove',token,x,y-120);point_action('testPointerUp',token,x,y-120);send('pause')
 s=wait(lambda s:s['manualPaused'] and not actor(s,token)['motionHeld']);a=actor(s,token)
 check(math.hypot(a['screenX']-drop[0],a['groundY']-drop[1])<1,'Mouse-up commits the final drag position without snapping to the press origin')
 time.sleep(.8);s=read();a=actor(s,token)
 check(math.hypot(a['screenX']-drop[0],a['groundY']-drop[1])<1,'Committed drop position stays stable when locomotion is paused')
 send('pause');wait(lambda s:not s['manualPaused']);s=wait(lambda s:actor(s,token)['specialElapsed']>frozen[1] or actor(s,token)['animationTime']>frozen[0]);a=actor(s,token)
 check(s['clickSpecials']==clicks,'Releasing a drag does not trigger another special')
 check(a['specialElapsed']>frozen[1] or a['animationTime']>frozen[0],'Release resumes the interrupted action clock')
 check(a['desktopAttached'] and a['desktopParent']!=0,'Dragged output stays attached to Explorer desktop')
 # Exercise both legacy and user-drawn 2D grounds, including release while paused.
 for mode in ['Line','Default']:
  ground={'mode':mode,'line':[{'x':.1,'y':.8},{'x':.9,'y':.65}],'surfaces':[]};send('ground',ground=ground);s=wait(lambda s:s['ground']['mode']==mode)
  a=actor(s,token);point_action('testPointerDown',token,round(a['screenX']),round(a['groundY']));wait(lambda s:actor(s,token)['pointerHeld'])
  point_action('testPointerMove',token,100000,-10000);s=wait(lambda s:actor(s,token)['dragging']);a=actor(s,token)
  check(.0399<=a['groundU']<=.9601 if mode=='Line' else a['screenX']<=3840-a['height']*.32+.01,'2D dragging clamps to path ends',mode=mode)
  stamp=a['animationTime'];send('pause');s=wait(lambda s:s['manualPaused']);point_action('testPointerUp',token,100000,-10000);s=wait(lambda s:not actor(s,token)['motionHeld'])
  check(s['manualPaused'],'Release does not override a manual global pause',mode=mode)
  send('pause');wait(lambda s:not s['manualPaused'])
 check(read()['count']<=6 and read()['namesUnique'],'Drag preserves fleet cap and unique names')
 report['Passed']=True
finally:
 if process is not None and process.poll() is None:
  send('exit')
  try:process.wait(timeout=15)
  except subprocess.TimeoutExpired:process.kill();process.wait()
 settings.write_bytes(original)
 report.update(checks=checks,runtimeAssemblySha256=hashlib.sha256((base/'UmaDesktop_Data/Managed/umamusume.dll').read_bytes()).hexdigest().upper(),OriginalSettingsRestored=settings.read_bytes()==original,PhysicalMouseInjection=False)
 (out/'drag-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8');(out/'drag-observations.json').write_text(json.dumps(samples,indent=2),encoding='utf-8')
