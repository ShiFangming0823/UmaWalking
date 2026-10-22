"""Render a frozen real mini-character on saved diagonal/narrow grounds.
Compare its alpha silhouette with a rectangular-ground reference at the same pose.
"""
import argparse,datetime,hashlib,json,subprocess,time
from pathlib import Path
from PIL import Image

parser=argparse.ArgumentParser()
parser.add_argument('--build-directory',default=str(Path(__file__).resolve().parents[2]/'build'/'UmaWalking'))
parser.add_argument('--ground-settings',required=True)
parser.add_argument('--evidence-directory',required=True)
args=parser.parse_args();base=Path(args.build_directory);out=Path(args.evidence_directory);out.mkdir(parents=True,exist_ok=True)
settings=base/'DesktopSettings.json';original=settings.read_bytes()
grounds=json.loads(Path(args.ground_settings).read_text(encoding='utf-8-sig'))['Ground']
checks=[];samples=[];process=None;last=0;report={'Passed':False};started=datetime.datetime.now(datetime.timezone.utc)
def check(ok,label,**values):
 row={'Passed':bool(ok),'Check':label,**values};checks.append(row);print(json.dumps(row),flush=True)
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
 except (OSError,json.JSONDecodeError):return None
def wait(fn,seconds=40):
 end=time.monotonic()+seconds;s=None
 while time.monotonic()<end:
  s=read()
  if s and fn(s):samples.append(s);return s
  if process.poll() is not None:raise AssertionError('Player exited')
  time.sleep(.08)
 raise AssertionError('Timeout '+json.dumps(s))
def capture(token,u,v,name):
 f=base/f'DesktopLogs/capture-{token}.png';version=read()['actors'][0].get('poseVersion',0);stamp=time.time_ns();send('capture');send('testGroundPose',token,mode='proportions',groundU=u,groundV=v)
 s=wait(lambda s:s['actors'][0].get('poseVersion',0)>version)
 end=time.monotonic()+10
 while time.monotonic()<end:
  try:
   if f.stat().st_mtime_ns>=stamp:
    im=Image.open(f).convert('RGBA');mask=im.getchannel('A').point(lambda p:255 if p>=64 else 0);box=mask.getbbox()
    if box:
     im.save(out/(name+'.png'));return s,box,mask.crop(box).resize((160,256))
  except OSError:pass
  time.sleep(.1)
 raise AssertionError('Missing render '+name)
def same_quad(actual,expected):
 return len(actual['surfaces'])==1 and all(abs(a[k]-b[k])<1e-6 for a,b in zip(actual['surfaces'][0]['points'],expected['points']) for k in ['x','y'])
try:
 data=json.loads(original.decode('utf-8-sig'));data['ModelSizeScale']=1;data['Ground']={'mode':'Default','line':[],'surfaces':[]};settings.write_text(json.dumps(data),encoding='utf-8')
 si=subprocess.STARTUPINFO();si.dwFlags|=subprocess.STARTF_USESHOWWINDOW;si.wShowWindow=0
 process=subprocess.Popen([str(base/'UmaDesktop.exe'),'-force-d3d11','-force-d3d11-bitblt-model','--desktop-test-mode','--desktop-test-ignore-occlusion','--desktop-no-controller','--desktop-max-actors','1','-logFile',str(base/'DesktopLogs/Player-proportions.log')],cwd=base,startupinfo=si)
 s=wait(lambda s:s['count']==1 and not s['loading'],60);token=s['actors'][0]['token'];send('pause');wait(lambda s:s['manualPaused'])
 ref={'points':[{'x':.1,'y':.5},{'x':.9,'y':.5},{'x':.9,'y':.9},{'x':.1,'y':.9}],'entrance':{'edge':3,'start':.35,'end':.65},'exit':{'edge':1,'start':.35,'end':.65}}
 send('ground',ground={'mode':'Planes','line':[],'surfaces':[ref]});wait(lambda s:same_quad(s['ground'],ref))
 s,box,refmask=capture(token,.5,.5,'proportions-reference');reference=(box[2]-box[0])/(box[3]-box[1]);referenceHeight=box[3]-box[1]
 check(referenceHeight>250,'Reference is a fully rendered character',box=box)
 for index,surface in enumerate(grounds['surfaces']):
  send('ground',ground={'mode':'Planes','line':[],'surfaces':[surface]});wait(lambda s:same_quad(s['ground'],surface))
  for u,v in [(.15,.15),(.5,.5),(.85,.85)]:
   name=f'proportions-floor-{index+1}-{int(u*100)}'
   s,box,mask=capture(token,u,v,name);a=s['actors'][0]
   aspect=(box[2]-box[0])/(box[3]-box[1]);error=abs(aspect/reference-1)
   check(error<.06,'Saved ground preserves rendered character aspect ratio',floor=index+1,u=u,v=v,relativeError=error,box=box)
   pixels=list(mask.getdata());rp=list(refmask.getdata());union=sum(x>127 or y>127 for x,y in zip(pixels,rp));intersection=sum(x>127 and y>127 for x,y in zip(pixels,rp));iou=intersection/union
   check(iou>.93,'Normalized rendered silhouette has no shear or flattening',floor=index+1,u=u,v=v,alphaIoU=iou)
   expected=referenceHeight*a['groundDepthScale'];check(abs((box[3]-box[1])-expected)<4,'Width and height use the same perspective scale',floor=index+1,scale=a['groundDepthScale'])
   check(a['desktopAttached'] and a['desktopParent']!=0,'Fixed model remains in desktop layer')
 for scale in [.5,1.5,1.0]:
  send('scale',modelSizeScale=scale);wait(lambda s:abs(s['modelSizeScale']-scale)<.001)
  s,box,mask=capture(token,.5,.5,'proportions-size-'+str(scale));a=s['actors'][0]
  aspect=(box[2]-box[0])/(box[3]-box[1]);check(abs(aspect/reference-1)<.06,'Size slider preserves proportions on saved diagonal floor',scale=scale)
  expected=referenceHeight*a['groundDepthScale']*scale;check(abs((box[3]-box[1])-expected)<4,'Size slider applies uniform size at fixed floor position',scale=scale)
 check(json.loads(settings.read_text(encoding='utf-8-sig'))['Ground']['mode']=='Planes','Ground remains serializable')
 report['Passed']=True
finally:
 if process is not None and process.poll() is None:
  send('exit')
  try:process.wait(timeout=15)
  except subprocess.TimeoutExpired:process.kill();process.wait()
 settings.write_bytes(original)
 report.update(checks=checks,runtimeAssemblySha256=hashlib.sha256((base/'UmaDesktop_Data/Managed/umamusume.dll').read_bytes()).hexdigest().upper(),RestoredOriginalSettings=settings.read_bytes()==original)
 (out/'ground-proportions-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
 (out/'ground-proportions-observations.json').write_text(json.dumps(samples,indent=2),encoding='utf-8')
