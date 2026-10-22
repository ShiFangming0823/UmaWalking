"""Real runtime options, restart acknowledgment and compact data validation. Never deletes game data."""
import json,time,subprocess,hashlib,datetime,traceback,argparse
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--build-directory',default=str(Path(__file__).resolve().parents[2]/'build'/'UmaWalking'));p.add_argument('--mini-data',required=True);p.add_argument('--evidence-directory',required=True);args=p.parse_args()
b=Path(args.build_directory);mini=Path(args.mini_data).resolve();out=Path(args.evidence_directory);out.mkdir(parents=True,exist_ok=True);config=b/'DesktopSettings.json';original=config.read_bytes();basecfg=json.loads(original.decode('utf-8-sig'));checks=[];samples=[];pid=None;initial=None;serial=0
report={'Passed':False,'OriginalDataDeleted':False}
def check(value,label,**details):
 checks.append(dict(Passed=bool(value),Check=label,**details));print(json.dumps(checks[-1]),flush=True)
 if not value:raise AssertionError(label)
def send(action,**kw):
 global serial
 serial=max(serial+1,time.time_ns());f=b/'DesktopCommands'/(str(serial)+'.tmp');f.write_text(json.dumps(dict(action=action,**kw)),encoding='utf-8');f.replace(f.with_suffix('.json'))
def read(allow_error=False):
 try:s=json.loads((b/'DesktopLogs/state.json').read_text(encoding='utf-8-sig'))
 except (OSError,json.JSONDecodeError):return None
 if s['processId']!=pid:return None
 if s.get('error') and not allow_error:raise RuntimeError(s['error'])
 samples.append(s);return s
def wait(fn,seconds=60,allow_error=False):
 end=time.monotonic()+seconds;s=None
 while time.monotonic()<end:
  s=read(allow_error)
  if s and fn(s):return s
  time.sleep(.15)
 raise AssertionError('Timed out: '+json.dumps(s,ensure_ascii=False))
def start(cfg,controller=False):
 global pid
 config.write_text(json.dumps(cfg,ensure_ascii=False),encoding='utf-8')
 flags=['--desktop-test-mode','--desktop-test-ignore-occlusion','--desktop-test-stable-population','--desktop-test-long-gait','--desktop-max-actors','2']
 if not controller:flags+=['--desktop-no-controller']
 proc=subprocess.Popen([str(b/'UmaDesktop.exe'),'-force-d3d11','-force-d3d11-bitblt-model',*flags,'-logFile',str(out/('options-player-'+str(time.time_ns())+'.log'))],cwd=b);pid=proc.pid;return proc

def stop():
 if pid:
  send('exit');time.sleep(2)

def switch(destination):
 global pid
 old=pid
 helper=subprocess.Popen([str(b/'UmaDesktopController.exe'),'--switch-data-test','--root',str(b),'--parent',str(pid),'--destination',str(destination)],cwd=b)
 end=time.monotonic()+30
 while time.monotonic()<end:
  try:r=json.loads((b/'DesktopLogs/restart-status.json').read_text(encoding='utf-8-sig'))
  except (OSError,json.JSONDecodeError):time.sleep(.2);continue
  if r.get('oldProcessId')==old and r.get('stage')=='Started':pid=r['newProcessId'];check(pid!=old,'Panel directory selection acknowledges settings then restarts');return
  if r.get('oldProcessId')==old and r.get('stage')=='Failed':raise RuntimeError(r)
  time.sleep(.2)
 raise AssertionError('Directory switch did not restart')
try:
 cfg=dict(basecfg);cfg.update(FrameRate=30,MsaaSamples=4,UseFxaa=True,Ground={'mode':'Default','line':[],'surfaces':[]},UiLanguage='zh')
 initial=start(cfg);s=wait(lambda s:s['count']==2 and not s['loading']);full_catalog=json.loads((b/'DesktopLogs/costume-catalog.json').read_text(encoding='utf-8-sig'))
 check(s['frameRate']==30,'Default frame rate is 30')
 for fps,aa in [(15,False),(30,True),(60,False),(120,True)]:
  before={a['token']:a['presentedFrames'] for a in s['actors']};send('performance',frameRate=fps,antiAliasing=aa)
  s=wait(lambda s:s['frameRate']==fps and s['msaaSamples']==(4 if aa else 1) and s['useFxaa']==aa)
  check(json.loads(config.read_text(encoding='utf-8-sig'))['FrameRate']==fps,'FPS and AA persist',FPS=fps,AA=aa)
  s=wait(lambda s:all(a['presentedFrames']>before.get(a['token'],0)+3 for a in s['actors']))
  time.sleep(1.5);s=read();check(s['fps']<=fps*1.5+2,'Frame limiter takes effect after settling',FPS=fps,ActualFPS=s['fps'])
  check(s['processId']==initial.pid and all(a['desktopAttached'] for a in s['actors']),'Performance switch keeps desktop models rendering without restart',FPS=fps,ActualFPS=s['fps'])
 for language in ['ja','en','zh']:
  send('language',language=language);s=wait(lambda s:s['language']==language)
  check(json.loads(config.read_text(encoding='utf-8-sig'))['UiLanguage']==language,'Language persists',language=language)
 send('pause');s=wait(lambda s:s['manualPaused']);clock=s['simulationSeconds'];frames=[a['presentedFrames'] for a in s['actors']];poses=[(a['animationTime'],a['specialElapsed']) for a in s['actors']];send('performance',frameRate=60,antiAliasing=False);s=wait(lambda s:s['frameRate']==60 and not s['useFxaa']);time.sleep(1);s=read()
 check(abs(s['simulationSeconds']-clock)<.001 and [(a['animationTime'],a['specialElapsed']) for a in s['actors']]==poses,'AA switch while paused preserves the frozen models')
 send('pause');s=wait(lambda s:not s['manualPaused'] and s['simulationSeconds']>clock+.1);check(True,'Unpause renders after AA change')
 switch(mini);s=wait(lambda s:s['count']==2 and not s['loading'] and Path(s['activeDataPath'])==mini)
 small_catalog=json.loads((b/'DesktopLogs/costume-catalog.json').read_text(encoding='utf-8-sig'))
 check(small_catalog==full_catalog,'Compact directory preserves complete character and costume catalog',Characters=len(small_catalog['characters']),Costumes=sum(len(c['costumes'])for c in small_catalog['characters']))
 mini_plan=json.loads((b/'DesktopLogs/asset-plan.json').read_text(encoding='utf-8-sig'));check(Path(mini_plan['source'])==mini,'Dependency catalog generated independently from extracted data')
 available={c['id']:c for c in small_catalog['characters']};loaded=[]
 for ident,category in [(1001,0),(1071,0),(1028,0),(1058,1),(1040,1),(1105,2),(1018,2),(1026,0)]:
  c=available.get(ident)
  if not c:continue
  outfits=[x for x in c['costumes']if x['category']==category] or c['costumes'];costume=outfits[0]['id'];target=s['actors'][0];old=target['token']
  send('testCharacter',token=old,characterId=ident,costumeId=costume)
  s=wait(lambda s:not s['loading'] and any(a['characterId']==ident and a['costumeId']==costume and a['token']!=old for a in s['actors']))
  a=next(a for a in s['actors']if a['characterId']==ident and a['costumeId']==costume);check(a['expressionMatchesPreset'] and a['presentedFrames']>0,'Extracted character, costume and expression render',ID=ident,Costume=costume)
  token=a['token'];send('testGait',token=token,mode='Run');s=wait(lambda s:any(a['token']==token and a['state']=='Run' for a in s['actors']));check(True,'Extracted running motion loads',ID=ident)
  send('special',token=token);s=wait(lambda s:any(a['token']==token and a['state']=='Special' for a in s['actors']));a=next(a for a in s['actors']if a['token']==token);check(a['expressionMatchesPreset'],'Extracted special and expression render',ID=ident);loaded.append(dict(ID=ident,Costume=costume,Clip=a['clip'],Expression=a['expression']))
 send('capture');time.sleep(.7)
 for image in (b/'DesktopLogs').glob('capture-*.png'):
  if image.stat().st_mtime>time.time()-4:(out/('compact-'+image.name)).write_bytes(image.read_bytes())
 active=s['activeDataPath'];send('dataPath',dataPath=str(out/'invalid-data'),requestId='invalid-test');time.sleep(.7);s=read()
 check(s['activeDataPath']==active and json.loads(config.read_text(encoding='utf-8-sig'))['DataPath']==active,'Invalid data directory does not alter the saved/active directory')
 check((Path(basecfg['DataPath'])/'meta').is_file() and (Path(basecfg['DataPath'])/'master/master.mdb').is_file(),'Original full data preserved')
 report['CompactSamples']=loaded;stop();pid=None
 bad=dict(cfg);bad['DataPath']=str(out/'missing-data');initial=start(bad,True);s=wait(lambda s:bool(s['error']) and s['controllerRunning'],allow_error=True)
 check(s['count']==0 and s['controllerRunning'],'Missing data still opens the control panel for path selection')
 switch(Path(basecfg['DataPath']));s=wait(lambda s:s['count']==2 and not s['loading']);check(Path(s['activeDataPath'])==Path(basecfg['DataPath']) and not s['error'],'Panel path switch recovers from missing data on restart')
 report['Passed']=True
except Exception:
 report['Failure']=traceback.format_exc();raise
finally:
 stop();config.write_bytes(original)
 report.update(checks=checks,OriginalSettingsRestored=config.read_bytes()==original,runtimeAssemblySha256=hashlib.sha256((b/'UmaDesktop_Data/Managed/umamusume.dll').read_bytes()).hexdigest().upper(),ControllerSha256=hashlib.sha256((b/'UmaDesktopController.exe').read_bytes()).hexdigest().upper())
 (out/'options-runtime-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');(out/'options-runtime-observations.json').write_text(json.dumps(samples,ensure_ascii=False),encoding='utf-8')
