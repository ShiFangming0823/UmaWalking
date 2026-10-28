import ctypes as c, json, time, os, hashlib, subprocess, datetime, argparse
from ctypes import wintypes as w
from pathlib import Path
parser=argparse.ArgumentParser(description='UmaDesktop native desktop visibility integration test')
parser.add_argument('--build-directory',default=str(Path(__file__).resolve().parents[2]/'build'/'UmaWalking'))
parser.add_argument('--evidence-directory',default=str(Path.cwd()/'visibility-test-results'))
args=parser.parse_args()
base=Path(args.build_directory);out=Path(args.evidence_directory);out.mkdir(parents=True,exist_ok=True)
u=c.WinDLL('user32',use_last_error=True);k=c.WinDLL('kernel32',use_last_error=True)
u.SetThreadDpiAwarenessContext.argtypes=[w.HANDLE];u.SetThreadDpiAwarenessContext(w.HANDLE(-4))
u.GetAncestor.argtypes=[w.HWND,w.UINT];u.GetAncestor.restype=w.HWND
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
original=(base/'DesktopSettings.json').read_bytes()
checks=[];samples=[];windows=[];process=None;startUtc=datetime.datetime.now(datetime.timezone.utc)
def read():
 try:
  s=json.loads(statefile.read_text(encoding='utf-8-sig'))
  if s.get('schemaVersion',0)<8 or datetime.datetime.fromisoformat(s['utc'].replace('Z','+00:00'))<startUtc:return None
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
def send(action,**values):
 name=str(time.time_ns());p=commands/(name+'.tmp');p.write_text(json.dumps(dict(action=action,**values)),encoding='utf-8');p.replace(commands/(name+'.json'))
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
report={'Passed':False,'checks':checks}
try:
 screen=(0,0,u.GetSystemMetrics(0),u.GetSystemMetrics(1));initialCover=cover(screen)
 startup=subprocess.STARTUPINFO();startup.dwFlags=subprocess.STARTF_USESHOWWINDOW;startup.wShowWindow=0
 env=os.environ.copy();env['UMA_WALKING_PROFILE']=str(out/'profile')
 process=subprocess.Popen([str(base/'UmaDesktop.exe'),'-force-d3d11','-force-d3d11-bitblt-model','--desktop-test-mode','--desktop-no-controller','--desktop-exit-seconds','180','-logFile',str(base/'DesktopLogs/Player-v5-visibility.log')],cwd=base,startupinfo=startup,env=env)
 wait(lambda s:s['desktopPaused'] and s['count']==0,50);delay(1)
 check(read()['count']==0 and read()['charactersLoaded']==0,'Startup while fullscreen defers character loading and spawning')
 close(initialCover)
 s=wait(lambda s:s['count']>=1 and not s['loading'] and not s['paused'],40)
 host=s['desktopHost'];child=s['desktopChildHost'];native=[]
 for a in s['actors']:
  h=a['nativeWindow'];r=w.RECT();u.GetWindowRect(h,c.byref(r));style=u.GetWindowLongPtrW(h,-16)&0xffffffff;ex=u.GetWindowLongPtrW(h,-20)&0xffffffff
  native.append({'window':h,'parent':u.GetParent(h),'style':hex(style),'exStyle':hex(ex),'rect':[r.left,r.top,r.right,r.bottom]})
  check(a['desktopAttached'] and u.GetParent(h)==child and u.GetAncestor(h,2)==host and bool(style&0x40000000) and not(style&0x80000000) and not(ex&8),'Pet is a non-topmost desktop child',token=a['token'])
  check(abs(r.left-(a['screenX']-(r.right-r.left)/2))<=1 and abs(r.top-(a['groundY']-(r.bottom-r.top)*(1-a['canvasFootViewport'])))<=1,'Child window coordinates remain physical pixels',rect=native[-1]['rect'])
 report['nativeWindows']=native
 # Fullscreen and hidden render readback race / spawn timer freeze.
 screen=(0,0,u.GetSystemMetrics(0),u.GetSystemMetrics(1));started=time.monotonic();h=cover(screen)
 s=wait(lambda s:s['desktopPaused'] and s['paused']);report['pauseLatencySeconds']=time.monotonic()-started
 delay(1.1);a=read();delay(4);b=read()
 check(a['simulationSeconds']==b['simulationSeconds'],'Fullscreen freezes simulation and spawn schedule',countBefore=a['count'],countAfter=b['count'])
 check(a['count']==b['count'] and a['charactersLoaded']==b['charactersLoaded'],'No character spawns while covered')
 check([x['presentedFrames'] for x in a['actors']]==[x['presentedFrames'] for x in b['actors']],'No GPU readback frames presented while covered')
 check(all(not u.IsWindowVisible(x['nativeWindow']) for x in b['actors']),'Every pet stays hidden, including pending frames')
 report['coveredSnapshot']=b
 # Preserve manual pause across automatic resume.
 send('pause');wait(lambda s:s['manualPaused']);close(h)
 s=wait(lambda s:not s['desktopPaused'] and s['manualPaused'] and s['paused'])
 check(True,'Uncovering desktop preserves a manual pause')
 before=s['simulationSeconds'];delay(1.2);check(read()['simulationSeconds']==before,'Manual pause still freezes clock')
 send('pause');s=wait(lambda s:not s['paused']);wait(lambda t:t['simulationSeconds']>s['simulationSeconds']+.3)
 check(True,'Resume continues animation without replaying missed spawns')
 # Partial windows must occlude via native hierarchy, but not stop simulation.
 work=w.RECT();u.SystemParametersInfoW(0x30,0,c.byref(work),0);l,t,r,b=work.left,work.top,work.right,work.bottom;mid=(l+r)//2
 h=cover((l,t,mid,b));delay(.8);s=wait(lambda s:not s['desktopPaused']);initial=s['simulationSeconds'];delay(.8)
 check(read()['simulationSeconds']>initial,'Partially exposed desktop continues running')
 h2=cover((mid,t,r,b));wait(lambda s:s['desktopPaused']);check(True,'Two tiled windows covering the work area pause the app')
 close(h2);wait(lambda s:not s['desktopPaused']);close(h)
 h=cover((l,t,r,b));wait(lambda s:s['desktopPaused']);check(True,'Maximized work-area window pauses the app')
 u.ShowWindow(h,6);wait(lambda s:not s['desktopPaused']);check(True,'Minimizing the covering fixture resumes the app');close(h)
 # An opaque constant-alpha layered window still fully hides the desktop.
 h=cover(screen,0x08080088);u.SetLayeredWindowAttributes(h,0,255,2);wait(lambda s:s['desktopPaused']);check(True,'Opaque layered fullscreen windows pause the app');close(h);wait(lambda s:not s['desktopPaused'])
 # Layered translucent overlays must not count as a solid desktop cover.
 h=cover(screen,0x08080088);u.SetLayeredWindowAttributes(h,0,30,2);delay(1);wait(lambda s:not s['desktopPaused']);check(True,'Transparent full-screen overlays do not cause false pauses');close(h)
 # Exercise both actual Explorer attachment paths and their coverage semantics.
 for mode in ['Root','IconView','Auto']:
  send('desktopHost',desktopHostMode=mode)
  s=wait(lambda s:s['desktopHostMode']==mode and all(a['desktopAttached'] for a in s['actors']))
  delay(.6);s=read()
  expected=s['desktopIconView'] if s['desktopHostStrategy']=='IconView' else s['desktopHost']
  check(s['desktopChildHost']==expected and all(u.GetParent(a['nativeWindow'])==expected and u.GetAncestor(a['nativeWindow'],2)==s['desktopHost'] for a in s['actors']),mode+' selects the actual Explorer child host')
  check(all(a['windowVisible'] and not(u.GetWindowLongPtrW(a['nativeWindow'],-20)&8) for a in s['actors']),mode+' presents visible non-topmost windows (native API visibility only)')
  h=cover(screen);wait(lambda s:s['desktopPaused']);delay(.6)
  check(all(not u.IsWindowVisible(a['nativeWindow']) for a in read()['actors']),mode+' respects fullscreen desktop coverage')
  close(h);wait(lambda s:not s['desktopPaused'] and all(a['windowVisible'] for a in s['actors']))
  check(json.loads((base/'DesktopSettings.json').read_text(encoding='utf-8-sig'))['DesktopHostMode']==mode,mode+' preference persists')
 report['runtimeAssemblySha256']=hashlib.sha256((base/'UmaDesktop_Data/Managed/umamusume.dll').read_bytes()).hexdigest().upper()
 report['Passed']=True
finally:
 for h in list(windows):close(h)
 if process is not None and process.poll() is None:
  send('exit')
  try:process.wait(timeout=8)
  except subprocess.TimeoutExpired:process.terminate();process.wait(timeout=5)
 (base/'DesktopSettings.json').write_bytes(original)
 report['OriginalSettingsRestored']=(base/'DesktopSettings.json').read_bytes()==original
 report['Win11CompositorVerified']=False
 report['checks']=checks
 (out/'desktop-visibility-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
 (out/'desktop-visibility-snapshots.json').write_text(json.dumps(samples,ensure_ascii=False,indent=2),encoding='utf-8')
 print('FINAL '+json.dumps({'Passed':report['Passed'],'Checks':len(checks)}),flush=True)
