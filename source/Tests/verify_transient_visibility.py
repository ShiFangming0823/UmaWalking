import ctypes as c, json, time, os, hashlib, subprocess, datetime, argparse
from ctypes import wintypes as w
from pathlib import Path
parser=argparse.ArgumentParser(description='UmaDesktop native desktop visibility integration test')
parser.add_argument('--build-directory',default=str(Path(__file__).resolve().parents[2]/'build'/'UmaWalking'))
parser.add_argument('--fixtures-directory',required=True)
parser.add_argument('--evidence-directory',default=str(Path.cwd()/'visibility-test-results'))
args=parser.parse_args()
base=Path(args.build_directory);out=Path(args.evidence_directory);out.mkdir(parents=True,exist_ok=True)
u=c.WinDLL('user32',use_last_error=True);k=c.WinDLL('kernel32',use_last_error=True)
u.SetThreadDpiAwarenessContext.argtypes=[w.HANDLE];u.SetThreadDpiAwarenessContext(w.HANDLE(-4))
u.GetParent.argtypes=[w.HWND];u.GetParent.restype=w.HWND
u.GetWindowLongPtrW.argtypes=[w.HWND,c.c_int];u.GetWindowLongPtrW.restype=c.c_ssize_t
u.GetWindowRect.argtypes=[w.HWND,c.POINTER(w.RECT)]
u.IsWindowVisible.argtypes=[w.HWND]
u.CreateWindowExW.argtypes=[w.DWORD,w.LPCWSTR,w.LPCWSTR,w.DWORD,c.c_int,c.c_int,c.c_int,c.c_int,w.HWND,w.HMENU,w.HINSTANCE,w.LPVOID];u.CreateWindowExW.restype=w.HWND
u.SetWindowPos.argtypes=[w.HWND,w.HWND,c.c_int,c.c_int,c.c_int,c.c_int,w.UINT]
u.ShowWindow.argtypes=[w.HWND,c.c_int];u.DestroyWindow.argtypes=[w.HWND]
k.OpenProcess.argtypes=[w.DWORD,w.BOOL,w.DWORD];k.OpenProcess.restype=w.HANDLE
k.GetProcessTimes.argtypes=[w.HANDLE,c.POINTER(w.FILETIME),c.POINTER(w.FILETIME),c.POINTER(w.FILETIME),c.POINTER(w.FILETIME)]
k.CloseHandle.argtypes=[w.HANDLE]
statefile=base/'DesktopLogs/state.json';commands=base/'DesktopCommands'
checks=[];samples=[];windows=[];process=None;startUtc=datetime.datetime.now(datetime.timezone.utc)
def read():
 try:
  s=json.loads(statefile.read_text(encoding='utf-8-sig'))
  if s.get('schemaVersion')!=4 or datetime.datetime.fromisoformat(s['utc'].replace('Z','+00:00'))<startUtc:return None
  if s.get('error'):raise RuntimeError(s['error'])
  return s
 except (OSError,json.JSONDecodeError):return None
def pump():
 msg=w.MSG()
 while u.PeekMessageW(c.byref(msg),0,0,0,1):u.TranslateMessage(c.byref(msg));u.DispatchMessageW(c.byref(msg))
def wait(pred,seconds=15):
 start=time.monotonic()
 while time.monotonic()-start<seconds:
  pump();s=read()
  if s is not None and pred(s):samples.append(s);return s
  time.sleep(.05)
 raise AssertionError('State wait timed out; last='+json.dumps(s,ensure_ascii=True))
def delay(seconds):
 end=time.monotonic()+seconds
 while time.monotonic()<end:pump();time.sleep(.03)
def check(value,label,**extra):
 checks.append(dict(Passed=bool(value),Check=label,**extra));print(json.dumps(checks[-1]),flush=True)
 if not value:raise AssertionError(label)
def send(action):
 name=str(time.time_ns());p=commands/(name+'.tmp');p.write_text(json.dumps({'action':action}),encoding='utf-8');p.replace(commands/(name+'.json'))
def cover(rect,ex=0x08000088):
 # Fixtures belong to this test only. No external application is moved or minimized.
 l,t,r,b=rect;h=u.CreateWindowExW(ex,'STATIC','UmaDesktop coverage integration fixture',0x80000000,l,t,r-l,b-t,0,0,0,0)
 if not h:raise c.WinError(c.get_last_error())
 windows.append(h);u.SetWindowPos(h,w.HWND(-1),l,t,r-l,b-t,0x10|0x40);u.UpdateWindow(h);pump();return h
def close(h):
 if h in windows:windows.remove(h)
 u.DestroyWindow(h);pump()
def cpu(pid):
 h=k.OpenProcess(0x1000,False,pid)
 try:
  a,b,d,e=w.FILETIME(),w.FILETIME(),w.FILETIME(),w.FILETIME()
  if not k.GetProcessTimes(h,c.byref(a),c.byref(b),c.byref(d),c.byref(e)):raise c.WinError(c.get_last_error())
  return (((d.dwHighDateTime<<32)|d.dwLowDateTime)+((e.dwHighDateTime<<32)|e.dwLowDateTime))/1e7
 finally:k.CloseHandle(h)

report={'Passed':False,'checks':checks,'Scope':'Native fixture windows under simulated process identities; no OS keyboard or third-party window automation.'}
fixture=None
fixtures=Path(args.fixtures_directory)
def overlay(exe,cls,seconds=5):
 ready=out/(str(time.time_ns())+'.ready')
 p=subprocess.Popen([str(fixtures/exe),cls,str(ready.resolve()),str(int(seconds*1000))],startupinfo=startup)
 end=time.monotonic()+4
 while not ready.exists() and time.monotonic()<end:
  pump();time.sleep(.03)
 if not ready.exists():raise AssertionError('Fixture failed to start: '+str(p.poll()))
 ready.unlink();return p
try:
 startup=subprocess.STARTUPINFO();startup.dwFlags=subprocess.STARTF_USESHOWWINDOW;startup.wShowWindow=0
 process=subprocess.Popen([str(base/'UmaDesktop.exe'),'-force-d3d11','-force-d3d11-bitblt-model','--desktop-test-mode','--desktop-max-actors','1','--desktop-no-controller','--desktop-exit-seconds','130','-logFile',str(base/'DesktopLogs/Player-transient-visibility.log')],cwd=base,startupinfo=startup)
 s=wait(lambda s:s['count']==1 and not s['loading'] and not s['paused'] and s['actors'][0]['presentedFrames']>5,45)
 check(s['switchEventHookInstalled'],'Real Alt+Tab WinEvent hook installs in the Unity runtime')
 check(not s['testIgnoresOcclusion'],'Visibility checks run without the test occlusion bypass')
 screen=(0,0,u.GetSystemMetrics(0),u.GetSystemMetrics(1))
 for exe,cls in [('PowerToys.FancyZones.exe','FancyZones_ZonesOverlay'),('explorer.exe','MultitaskingViewFrame'),('explorer.exe','XamlExplorerHostIslandWindow')]:
  fixture=overlay(exe,cls,5)
  s=wait(lambda s:s['windowManagementActive'])
  a=read();delay(1);b=read()
  check(not b['desktopPaused'] and b['simulationSeconds']>a['simulationSeconds'],cls+' preserves animation while visible')
  check(all(u.IsWindowVisible(x['nativeWindow']) for x in b['actors']),cls+' leaves native model windows shown')
  check(b['actors'][0]['presentedFrames']>a['actors'][0]['presentedFrames'],cls+' keeps rendering')
  # A fullscreen preview/layout transition cannot change the established visible state.
  h=cover(screen);a=read();delay(1.3);b=read()
  check(not b['desktopPaused'] and b['simulationSeconds']>a['simulationSeconds'],cls+' defers a full-cover transition while arranging/switching')
  close(h);fixture.wait(timeout=8);fixture=None;wait(lambda s:not s['windowManagementActive'] and not s['paused'])
 # After FancyZones exits, stable full coverage must still pause as before.
 fixture=overlay('PowerToys.FancyZones.exe','FancyZones_ZonesOverlay',3)
 wait(lambda s:s['windowManagementActive']);h=cover(screen);delay(1)
 check(not read()['desktopPaused'],'A visible FancyZones overlay keeps the established active state')
 fixture.wait(timeout=6);fixture=None;ended=time.monotonic()
 s=wait(lambda s:s['desktopPaused'] and not s['windowManagementActive'])
 check(True,'An actual fullscreen window pauses after FancyZones finishes',settleAndStateLatencySeconds=round(time.monotonic()-ended,3))
 # Opening transient UI must not resume a program which was already paused.
 fixture=overlay('PowerToys.FancyZones.exe','FancyZones_ZonesOverlay',3)
 s=wait(lambda s:s['windowManagementActive'] and s['desktopPaused']);a=s;delay(.7);b=read()
 check(b['desktopPaused'] and b['simulationSeconds']==a['simulationSeconds'],'An already covered desktop stays paused during FancyZones')
 close(h);delay(.5);check(read()['desktopPaused'],'Removing the fullscreen preview does not resume inside a transient interaction')
 fixture.wait(timeout=6);fixture=None;s=wait(lambda s:not s['desktopPaused'] and not s['windowManagementActive'])
 check(True,'Exposed desktop resumes when transient UI closes')
 check(all(a['desktopAttached'] and not(u.GetWindowLongPtrW(a['nativeWindow'],-20)&8) for a in s['actors']),'Compatibility never turns models into topmost app windows')
 report['runtimeAssemblySha256']=hashlib.sha256((base/'UmaDesktop_Data/Managed/umamusume.dll').read_bytes()).hexdigest().upper();report['Passed']=True
finally:
 for h in list(windows):close(h)
 if fixture is not None and fixture.poll() is None:fixture.wait(timeout=10)
 if process is not None and process.poll() is None:
  send('exit')
  try:process.wait(timeout=8)
  except subprocess.TimeoutExpired:process.terminate();process.wait(timeout=5)
 (out/'transient-visibility-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
 (out/'transient-visibility-snapshots.json').write_text(json.dumps(samples,ensure_ascii=False,indent=2),encoding='utf-8')
 print('FINAL '+json.dumps({'Passed':report['Passed'],'Checks':len(checks)}),flush=True)
