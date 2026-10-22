import json,time,datetime,subprocess,hashlib,math,argparse,ctypes,psutil
from ctypes import wintypes
from PIL import Image
from pathlib import Path
parser=argparse.ArgumentParser(description='UmaDesktop random speed and automatic special spacing integration')
parser.add_argument('--build-directory',default=str(Path(__file__).resolve().parents[2]/'build'/'UmaWalking'))
parser.add_argument('--evidence-directory',default=str(Path.cwd()/'spacing-test-results'))
args=parser.parse_args();base=Path(args.build_directory);out=Path(args.evidence_directory);out.mkdir(parents=True,exist_ok=True)
started=datetime.datetime.now(datetime.timezone.utc);checks=[];samples=[];process=None
report={'Passed':False}
def check(ok,label,**values):
 checks.append({'Passed':bool(ok),'Check':label,**values});print(json.dumps(checks[-1]),flush=True)
 if not ok:raise AssertionError(label)
def read():
 try:
  s=json.loads((base/'DesktopLogs/state.json').read_text(encoding='utf-8-sig'))
  if datetime.datetime.fromisoformat(s['utc'].replace('Z','+00:00'))<started:return None
  if s['error']:raise RuntimeError(s['error'])
  return s
 except(OSError,json.JSONDecodeError):return None
def wait(fn,seconds=30):
 end=time.monotonic()+seconds
 while time.monotonic()<end:
  s=read()
  if s and fn(s):samples.append(s);return s
  if process.poll() is not None:raise AssertionError('Runtime exited')
  time.sleep(.05)
 raise AssertionError('Timed out, last='+json.dumps(s,ensure_ascii=True))
lastSendNs=0
def send(action,token=0,**values):
 global lastSendNs
 lastSendNs=max(time.time_ns(),lastSendNs+1)
 name=str(lastSendNs);p=base/'DesktopCommands'/(name+'.tmp');p.write_text(json.dumps({'action':action,'token':token,**values}),encoding='utf-8');p.replace(p.with_suffix('.json'))
def actor(s,token):return next(a for a in s['actors'] if a['token']==token)
def gap(a,b):return abs(a['clearanceX']-b['clearanceX'])-a['clearanceHalfWidth']-b['clearanceHalfWidth']

settingsFile=base/'DesktopSettings.json';originalSettings=settingsFile.read_bytes();owned=[];images={}
u=ctypes.WinDLL('user32',use_last_error=True);k=ctypes.WinDLL('kernel32',use_last_error=True)
u.SetThreadDpiAwarenessContext.argtypes=[wintypes.HANDLE];u.SetThreadDpiAwarenessContext(wintypes.HANDLE(-4))
u.GetWindowRect.argtypes=[wintypes.HWND,ctypes.POINTER(wintypes.RECT)]
k.OpenProcess.argtypes=[wintypes.DWORD,wintypes.BOOL,wintypes.DWORD];k.OpenProcess.restype=wintypes.HANDLE
k.GetProcessTimes.argtypes=[wintypes.HANDLE,ctypes.POINTER(wintypes.FILETIME),ctypes.POINTER(wintypes.FILETIME),ctypes.POINTER(wintypes.FILETIME),ctypes.POINTER(wintypes.FILETIME)];k.CloseHandle.argtypes=[wintypes.HANDLE]
def live(pid):return psutil.pid_exists(pid)
def ticks(pid):
 h=k.OpenProcess(0x1000,False,pid)
 try:
  a,b,c,d=[wintypes.FILETIME() for _ in range(4)]
  if not k.GetProcessTimes(h,ctypes.byref(a),ctypes.byref(b),ctypes.byref(c),ctypes.byref(d)):raise ctypes.WinError()
  return ((a.dwHighDateTime<<32)|a.dwLowDateTime)+504911232000000000
 finally:k.CloseHandle(h)
def launch(flags):
 global process,started
 started=datetime.datetime.now(datetime.timezone.utc)
 process=subprocess.Popen([str(base/'UmaDesktop.exe'),'-force-d3d11','-force-d3d11-bitblt-model',*flags,'-logFile',str(base/'DesktopLogs/Player-size-restart.log')],cwd=base,startupinfo=si);owned.append(process.pid)
 return process.pid
def finish():
 if process is not None and live(process.pid):
  send('exit');process.wait(timeout=10)
def controller_for(pid):
 for p in psutil.process_iter(['pid','name','cmdline']):
  if p.info['name']=='UmaDesktopController.exe' and '--parent' in (p.info['cmdline'] or []):
   args=p.info['cmdline'];at=args.index('--parent')
   if args[at+1]==str(pid):return p.pid
 raise AssertionError('No owning controller')
def restart(owner=0,percent=100):
 global process,started
 old=process.pid;started=datetime.datetime.now(datetime.timezone.utc)
 helper=subprocess.Popen([str(base/'UmaDesktopController.exe'),'--restart','--root',str(base),'--parent',str(old),'--controller',str(owner),'--controller-start',str(ticks(owner) if owner else 0),'--size-percent',str(percent)],cwd=base,startupinfo=si)
 helper.wait(timeout=12)
 result=json.loads((base/'DesktopLogs/restart-status.json').read_text(encoding='utf-8-sig'))
 check(helper.returncode==0 and result['stage']=='Started','Independent restart helper launches exactly one replacement',result=result)
 check(not live(old) and (not owner or not live(owner)),'Old runtime and owning controller have terminated')
 pid=result['newProcessId'];owned.append(pid);process=psutil.Process(pid)
 # Match the Popen API used by the shared wait helper.
 process.poll=lambda:None if live(pid) else 0
 return wait(lambda s:s['processId']==pid and s['count']>=1 and not s['loading'],50)
try:
 si=subprocess.STARTUPINFO();si.dwFlags=subprocess.STARTF_USESHOWWINDOW;si.wShowWindow=0
 pid=launch(['--desktop-test-mode','--desktop-test-ignore-occlusion','--desktop-no-controller','--desktop-max-actors','2','--desktop-fps','20'])
 s=wait(lambda s:s['count']==2 and not s['loading'],50);send('pause');s=wait(lambda s:s['manualPaused'])
 before={a['token']:a for a in s['actors']};stamp=s['simulationSeconds']
 for scale in [.5,1.5,1.0]:
  send('capture');send('scale',modelSizeScale=scale)
  s=wait(lambda s:abs(s['modelSizeScale']-scale)<.001 and all(abs(a['height']-before[a['token']]['height']*scale)<.05 for a in s['actors']),15)
  # Paused rendering is throttled to 2 fps; wait for the actual GPU presentation,
  # not just the earlier status update from the command handler.
  end=time.monotonic()+8
  while time.monotonic()<end:
   s=read();ready=True
   if s is None:time.sleep(.1);continue
   for current in s['actors']:
    rr=wintypes.RECT();u.GetWindowRect(current['nativeWindow'],ctypes.byref(rr))
    if rr.right-rr.left!=round(768*scale):ready=False
    try:
     with Image.open(base/f'DesktopLogs/capture-{current["token"]}.png') as captured:
      if captured.size!=(round(768*scale),round(768*scale)):ready=False
    except OSError:ready=False
   if ready:break
   time.sleep(.1)
  check(s['simulationSeconds']==stamp,'Resizing while manually paused does not advance animation',scale=scale)
  for a in s['actors']:
   r=wintypes.RECT();u.GetWindowRect(a['nativeWindow'],ctypes.byref(r));side=round(768*scale)
   check(r.right-r.left==side and r.bottom-r.top==side,'Native output canvas resizes without changing character identity',scale=scale,character=a['characterId'],side=side,actualRect=[r.left,r.top,r.right,r.bottom])
   check(abs(r.top-(a['groundY']-side*(1-.004)))<2,'Physical ground anchor remains fixed',scale=scale)
   check(a['walkSpeedFactor']==before[a['token']]['walkSpeedFactor'] and a['runSpeedFactor']==before[a['token']]['runSpeedFactor'],'Character speed profile survives resizing')
   src=base/f'DesktopLogs/capture-{a["token"]}.png';im=Image.open(src).convert('RGBA');check(im.size==(side,side),'Actual rendered output uses requested canvas size')
   box=im.getchannel('A').getbbox();check(box is not None,'Resized character still renders visible pixels');images[(a['token'],scale)]=box
   im.save(out/f'size-{int(scale*100)}-{a["characterId"]}.png')
 for token in before:
  small=images[(token,.5)];large=images[(token,1.5)];ratio=(large[3]-large[1])/(small[3]-small[1]);check(abs(ratio-3)<.08,'Rendered character height scales threefold from 50 to 150 percent',ratio=ratio)
 send('pause');wait(lambda s:not s['manualPaused']);send('filter',mode='Include',includeIds=[1028],excludeIds=[])
 s=wait(lambda s:s['count']==1 and s['actors'][0]['characterId']==1028 and not s['loading'],60)
 send('pause');wait(lambda s:s['manualPaused']);send('scale',modelSizeScale=1.25)
 s=wait(lambda s:abs(s['modelSizeScale']-1.25)<.001)
 check(abs(s['actors'][0]['height']-570*1.25)<.1,'Hishi Akebono preserves the 1.5x relative size')
 check(abs(json.loads(settingsFile.read_text(encoding='utf-8-sig'))['ModelSizeScale']-1.25)<.001,'Size preference is persisted')
 send('testHang');time.sleep(1.2);s=restart(percent=125)
 check(abs(s['modelSizeScale']-1.25)<.001,'Force restart recovers a blocked Unity main thread and retains size')
 argsNow=json.loads((base/'DesktopLogs/launch.json').read_text(encoding='utf-8-sig'))['arguments'];check('--desktop-fps' in argsNow and argsNow[argsNow.index('--desktop-fps')+1]=='20','Restart preserves launch options')
 send('scale',modelSizeScale=1);wait(lambda s:abs(s['modelSizeScale']-1)<.001);finish()
 # Normal startups draw the first character through the same eligible shuffle pool.
 data=json.loads(settingsFile.read_text(encoding='utf-8-sig'));data.update(SelectionMode='All',IncludeIds=[],ExcludeIds=[],RandomSeed=0,ModelSizeScale=1);settingsFile.write_text(json.dumps(data),encoding='utf-8')
 first=[];pid=launch(['--desktop-max-actors','1','--desktop-fps','15'])
 s=wait(lambda s:s['processId']==pid and s['count']==1 and s['controllerRunning'] and not s['loading'],50)
 for round in range(3):
  first.append(s['actors'][0]['characterId'])
  if round<2:s=restart(owner=controller_for(process.pid),percent=100)
 check(len(set(first))>1,'Normal restart draws different first characters without a fixed initial character',firstCharacters=first)
 check(not s['testIgnoresOcclusion'],'Final startup uses genuine desktop visibility rules')
 report.update(Passed=True,firstCharacters=first)
finally:
 if process is not None and live(process.pid):
  send('exit')
  try:process.wait(timeout=10)
  except (subprocess.TimeoutExpired,psutil.TimeoutExpired):process.kill()
 # Restore user preferences after all owned instances have stopped.
 settingsFile.write_bytes(originalSettings)
 report.update(checks=checks,runtimeAssemblySha256=hashlib.sha256((base/'UmaDesktop_Data/Managed/umamusume.dll').read_bytes()).hexdigest().upper())
 (out/'size-restart-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');(out/'size-restart-snapshots.json').write_text(json.dumps(samples,ensure_ascii=False,indent=2),encoding='utf-8')
 print('FINAL '+json.dumps({'Passed':report['Passed'],'Checks':len(checks)}),flush=True)
