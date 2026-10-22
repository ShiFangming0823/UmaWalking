"""Real game shoe geometry / ground contact and containment regression."""
import argparse,datetime,hashlib,json,math,subprocess,time
from pathlib import Path
from PIL import Image
p=argparse.ArgumentParser();p.add_argument('--build-directory',default=str(Path(__file__).resolve().parents[2]/'build'/'UmaWalking'));p.add_argument('--ground-settings',required=True);p.add_argument('--evidence-directory',required=True);args=p.parse_args()
b=Path(args.build_directory);out=Path(args.evidence_directory);out.mkdir(parents=True,exist_ok=True);settings=b/'DesktopSettings.json';original=settings.read_bytes();grounds=json.loads(Path(args.ground_settings).read_text(encoding='utf-8-sig'))['Ground'];checks=[];samples=[];last=0;process=None;report={'Passed':False};started=datetime.datetime.now(datetime.timezone.utc)
def check(ok,label,**values):
 row={'Passed':bool(ok),'Check':label,**values};checks.append(row);print(json.dumps(row),flush=True)
 if not ok:raise AssertionError(label)
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
def wait(fn,seconds=50):
 end=time.monotonic()+seconds;s=None
 while time.monotonic()<end:
  s=read()
  if s and fn(s):samples.append(s);return s
  if process.poll() is not None:raise AssertionError('Player exited')
  time.sleep(.07)
 raise AssertionError('Timeout '+json.dumps(s))
def inside(surface,point,tolerance=1):
 points=[(p['x']*3840,p['y']*2160)for p in surface['points']];signs=[]
 for a,c in zip(points,points[1:]+points[:1]):
  cross=(c[0]-a[0])*(point['y']-a[1])-(c[1]-a[1])*(point['x']-a[0]);length=math.hypot(c[0]-a[0],c[1]-a[1]);signs.append(cross/length)
 return min(signs)>=-tolerance or max(signs)<=tolerance
def load(character,costume):
 s=read();old=s['actors'][0]['token']
 if s['manualPaused']:send('pause');wait(lambda s:not s['manualPaused'])
 send('testCharacter',characterId=character,costumeId=costume)
 s=wait(lambda s:s['count']==1 and s['actors'][0]['token']!=old and s['actors'][0]['characterId']==character and s['actors'][0]['costumeId']==costume and not s['loading'],70)
 send('pause');return wait(lambda s:s['manualPaused'])
def pose(u,v,yaw,name):
 s=read();a=s['actors'][0];token=a['token'];version=a['poseVersion'];stamp=time.time_ns();send('capture');send('testGroundPose',token,mode='footing',groundU=u,groundV=v,poseYaw=yaw)
 s=wait(lambda s:s['actors'][0]['poseVersion']>version);a=s['actors'][0];file=b/f'DesktopLogs/capture-{token}.png'
 end=time.monotonic()+8
 while time.monotonic()<end:
  try:
   if file.stat().st_mtime_ns>=stamp:im=Image.open(file).convert('RGBA');im.save(out/(name+'.png'));return s,im
  except OSError:pass
  time.sleep(.1)
 raise AssertionError('Missing capture')
try:
 data=json.loads(original.decode('utf-8-sig'));data['Ground']=grounds;data['ModelSizeScale']=1.5;settings.write_text(json.dumps(data),encoding='utf-8')
 si=subprocess.STARTUPINFO();si.dwFlags|=subprocess.STARTF_USESHOWWINDOW;si.wShowWindow=0
 process=subprocess.Popen([str(b/'UmaDesktop.exe'),'-force-d3d11','-force-d3d11-bitblt-model','--desktop-test-mode','--desktop-test-ignore-occlusion','--desktop-no-controller','--desktop-test-stable-population','--desktop-max-actors','1','-logFile',str(b/'DesktopLogs/Player-footing.log')],cwd=b,startupinfo=si)
 wait(lambda s:s['count']==1 and not s['loading'],70)
 for character,costume in [(1001,'00'),(1052,'00'),(1052,'10'),(1028,'01')]:
  load(character,costume)
  for floor,surface in enumerate(grounds['surfaces']):
   send('ground',ground={'mode':'Planes','line':[],'surfaces':[surface]})
   wait(lambda s:len(s['ground']['surfaces'])==1 and abs(s['ground']['surfaces'][0]['points'][0]['x']-surface['points'][0]['x'])<1e-6)
   for n,(u,v,yaw) in enumerate([(.041,.5,0),(.959,.5,90),(.5,.041,180),(.5,.959,-90)]):
    s,im=pose(u,v,yaw,f'footing-{character}-{costume}-floor{floor+1}-{n}');a=s['actors'][0]
    check(len(a['footContacts'])==16 and all(inside(surface,p)for p in a['footContacts']),'Both shoe ground projections fit inside saved ground',character=character,costume=costume,floor=floor+1,edge=n,fit=a['groundFitScale'])
    check(abs(a['soleClearancePixels'])<.1,'Calibrated support sole meets floor without old camera-padding offset',character=character,costume=costume,gap=a['soleClearancePixels'])
    mask=im.getchannel('A').point(lambda p:255 if p>=32 else 0);box=mask.getbbox();anchor=im.height*(1-a['canvasFootViewport']);tolerance=a['footprintRadiusY']+8
    check(box is not None and abs(box[3]-anchor)<tolerance,'Actual rendered supporting shoe reaches the floor anchor',character=character,costume=costume,box=box,anchor=anchor,tolerance=tolerance)
 # Natural movement on the narrow saved platform, including the original Haru outfit.
 load(1052,'10');send('ground',ground={'mode':'Planes','line':[],'surfaces':[grounds['surfaces'][-1]]});wait(lambda s:s['actors'][0]['groundStage']==0)
 pose(.5,.5,0,'footing-haru-natural-start');send('pause');wait(lambda s:not s['manualPaused']);end=time.monotonic()+12;observed=0
 while time.monotonic()<end:
  s=read()
  if not s:time.sleep(.05);continue
  a=s['actors'][0]
  if a['groundStage'] in [1,2]:
   samples.append(s);observed+=1
   check(all(inside(grounds['surfaces'][-1],p)for p in a['footContacts']),'Natural animation keeps shoe footprint within platform')
  time.sleep(.5)
 check(observed>5,'Natural movement exercised the boundary checks')
 send('next',token=read()['actors'][0]['token']);wait(lambda s:s['departures']>0,60);check(True,'Footprint padding still permits leaving through configured exit')
 report['Passed']=True
finally:
 if process is not None and process.poll() is None:
  send('exit')
  try:process.wait(timeout=15)
  except subprocess.TimeoutExpired:process.kill();process.wait()
 settings.write_bytes(original);report.update(checks=checks,runtimeAssemblySha256=hashlib.sha256((b/'UmaDesktop_Data/Managed/umamusume.dll').read_bytes()).hexdigest().upper(),OriginalSettingsRestored=settings.read_bytes()==original)
 (out/'footing-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8');(out/'footing-observations.json').write_text(json.dumps(samples,indent=2),encoding='utf-8')
