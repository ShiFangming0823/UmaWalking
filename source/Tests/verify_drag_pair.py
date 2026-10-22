import argparse,datetime,hashlib,json,subprocess,time
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--build-directory',default=str(Path(__file__).resolve().parents[2]/'build'/'UmaWalking'));p.add_argument('--evidence-directory',required=True);args=p.parse_args()
b=Path(args.build_directory);out=Path(args.evidence_directory);settings=b/'DesktopSettings.json';original=settings.read_bytes();checks=[];last=0;process=None;report={'Passed':False};started=datetime.datetime.now(datetime.timezone.utc)
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
def wait(fn,seconds=40):
 end=time.monotonic()+seconds;s=None
 while time.monotonic()<end:
  s=read()
  if s and fn(s):return s
  time.sleep(.08)
 raise AssertionError('Timeout '+json.dumps(s))
def check(ok,label):
 checks.append({'Passed':bool(ok),'Check':label});print(label,ok,flush=True)
 if not ok:raise AssertionError(label)
try:
 data=json.loads(original.decode('utf-8-sig'));data['Ground']={'mode':'Default','line':[],'surfaces':[]};settings.write_text(json.dumps(data),encoding='utf-8')
 si=subprocess.STARTUPINFO();si.dwFlags|=subprocess.STARTF_USESHOWWINDOW;si.wShowWindow=0
 process=subprocess.Popen([str(b/'UmaDesktop.exe'),'-force-d3d11','-force-d3d11-bitblt-model','--desktop-test-mode','--desktop-pair-audit','--desktop-test-ignore-occlusion','--desktop-no-controller','-logFile',str(b/'DesktopLogs/Player-drag-pair.log')],cwd=b,startupinfo=si)
 s=wait(lambda s:s['count']==2 and all(a['state']=='Interaction' for a in s['actors']));a=s['actors'][0];token=a['token']
 send('testPointerDown',token,groundU=a['screenX'],groundV=a['groundY']);s=wait(lambda s:all(a['motionHeld'] for a in s['actors']));times=[a['animationTime']for a in s['actors']];time.sleep(1.2);s=read()
 check([a['animationTime']for a in s['actors']]==times,'Pressing one participant freezes both synchronized pair clocks')
 send('testPointerMove',token,groundU=700,groundV=a['groundY']);s=wait(lambda s:s['actors'][0]['dragging'] and not s['actors'][1]['motionHeld'])
 check(s['actors'][0]['state']=='Interaction' and s['actors'][0]['animationTime']==times[0],'Dragging detaches pair without replacing the held clip')
 check(s['actors'][1]['state']!='Interaction','The partner resumes locomotion after detaching')
 send('testPointerUp',token,groundU=700,groundV=a['groundY']);s=wait(lambda s:not s['actors'][0]['motionHeld'] and s['actors'][0]['animationTime']>times[0]);check(True,'Released participant continues the saved animation time')
 send('special',token);s=wait(lambda s:s['actors'][0]['state']=='Special');check(s['clickSpecials']>0 and not s['error'],'A new user special replaces the detached interaction cleanly')
 report['Passed']=True
finally:
 if process is not None and process.poll() is None:
  send('exit')
  try:process.wait(timeout=15)
  except subprocess.TimeoutExpired:process.kill();process.wait()
 settings.write_bytes(original);report.update(checks=checks,runtimeAssemblySha256=hashlib.sha256((b/'UmaDesktop_Data/Managed/umamusume.dll').read_bytes()).hexdigest().upper())
 out.mkdir(parents=True,exist_ok=True);(out/'drag-pair-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
