"""Real Unity capacity/lifecycle tests, with isolated test settings restored on exit."""
import argparse,datetime,hashlib,json,subprocess,time,math
from pathlib import Path
from PIL import Image
p=argparse.ArgumentParser();p.add_argument('--build-directory',default=str(Path(__file__).resolve().parents[2]/'build'/'UmaWalking'));p.add_argument('--evidence-directory',required=True);args=p.parse_args()
b=Path(args.build_directory);out=Path(args.evidence_directory);out.mkdir(parents=True,exist_ok=True);settings=b/'DesktopSettings.json';original=settings.read_bytes();checks=[];samples=[];last=0;process=None;report={'Passed':False};previous=None
ids=[1001,1002,1003,1004,1005,1006,1007,1008,1028,1041]
line={'mode':'Line','line':[{'x':.08,'y':.9},{'x':.92,'y':.9}],'surfaces':[]}
def send(action,token=0,**kw):
 global last
 last=max(last+1,time.time_ns());f=b/'DesktopCommands'/(str(last)+'.tmp');f.write_text(json.dumps(dict(action=action,token=token,**kw)),encoding='utf-8');f.replace(f.with_suffix('.json'))
def read():
 try:s=json.loads((b/'DesktopLogs/state.json').read_text(encoding='utf-8-sig'))
 except(OSError,json.JSONDecodeError):return None
 if s['processId']!=process.pid:return None
 if s['error']:raise RuntimeError(s['error'])
 assert s['count']<=10 and s['occupancyValid'] and s['costumesUnique'],s
 samples.append(s);return s
def wait(fn,seconds=90):
 end=time.monotonic()+seconds;s=None
 while time.monotonic()<end:
  s=read()
  if s and fn(s):return s
  if process.poll() is not None:raise AssertionError('Player exited unexpectedly')
  time.sleep(.1)
 raise AssertionError('Timeout: '+json.dumps(s,ensure_ascii=False))
def current():return wait(lambda s:True)
def check(ok,label,**values):
 row=dict(Passed=bool(ok),Check=label,**values);checks.append(row);print(json.dumps(row),flush=True)
 if not ok:raise AssertionError(label)
def actor(s,t):return next((a for a in s['actors']if a['token']==t),None)
def queued(s):return[e['characterId']for e in s['pendingEntries']]
def cap(n):
 send('actorLimit',maxActors=n);return wait(lambda s:s['maxActors']==max(1,min(10,n)))
def settled(n,seconds=90):return wait(lambda s:s['count']==n and not s['loading'] and s['retiringCount']==0,seconds)
def filter(mode,include=None,exclude=None):
 send('filter',mode=mode,includeIds=include or [],excludeIds=exclude or []);return wait(lambda s:s['mode']==mode and s['includeIds']==(include or []) and s['excludeIds']==(exclude or []))
def pause(value):
 s=current()
 if s['manualPaused']!=value:send('pause')
 return wait(lambda s:s['manualPaused']==value)
def observe(seconds):
 end=time.monotonic()+seconds;rows=[];stamp=''
 while time.monotonic()<end:
  s=read()
  if s and s['utc']!=stamp:stamp=s['utc'];rows.append(s)
  time.sleep(.12)
 return rows
def stop():
 global process
 if process and process.poll() is None:
  send('exit')
  try:process.wait(timeout=20)
  except subprocess.TimeoutExpired:process.kill();process.wait()
 process=None
def launch(stable=True):
 global process
 si=subprocess.STARTUPINFO();si.dwFlags|=subprocess.STARTF_USESHOWWINDOW;si.wShowWindow=0
 flags=['--desktop-test-mode','--desktop-test-ignore-occlusion','--desktop-no-controller']
 if stable:flags+=['--desktop-test-stable-population','--desktop-test-long-gait']
 process=subprocess.Popen([str(b/'UmaDesktop.exe'),'-force-d3d11','-force-d3d11-bitblt-model',*flags,'-logFile',str(out/('player-'+str(time.time_ns())+'.log'))],cwd=b,startupinfo=si)
 return wait(lambda s:not s['loading'] and s['count']>0)
try:
 cfg=json.loads(original.decode('utf-8-sig'));cfg.pop('Avoid3DCollisions',None);cfg.update(MaxActors=1,ModelSizeScale=.6,FrameRate=15,RenderScale=.75,Ground=line,SelectionMode='All',IncludeIds=[],ExcludeIds=[])
 settings.write_text(json.dumps(cfg),encoding='utf-8');s=launch();check(not s['avoid3DCollisions'],'Missing avoidance setting defaults OFF')
 for n in range(1,11):
  cap(n);s=settled(n);check(s['targetCount']==n and s['namesUnique'] and len({a['characterId']for a in s['actors']})==n,'All random reaches chosen cap with distinct names',Limit=n)
 s=wait(lambda s:all(a['phase']=='Active' for a in s['actors']),70);send('capture');observe(1.2)
 for a in s['actors']:
  f=b/'DesktopLogs'/('capture-'+str(a['token'])+'.png')
  with Image.open(f) as im:check(im.convert('RGBA').getchannel('A').getbbox() is not None,'Ten independent render layers contain visible model pixels',Token=a['token'])
  (out/('ten-model-'+str(a['token'])+'.png')).write_bytes(f.read_bytes())
 check(len({a['nativeWindow']for a in s['actors']})==10 and all(a['desktopAttached'] and a['presentedFrames']>10 for a in s['actors']),'All ten models have independent active desktop windows')
 send('enter',characterId=1052);wait(lambda s:queued(s)==[1052]);rows=observe(1.1);check(all(s['count']==10 and queued(s)==[1052]for s in rows),'Manual entry at ten remains queued without an eleventh model')
 survivors={a['token']for a in sorted(current()['actors'],key=lambda a:a['ordinal'])[:3]}
 cap(3);s=wait(lambda s:s['activeCount']==3 and s['retiringCount']==7);rows=observe(1.1)
 check(all(s['activeCount']==3 for s in rows),'Lowering ten to three requests exactly seven exits once')
 s=settled(3);check({a['token']for a in s['actors']}==survivors and queued(s)==[1052],'Oldest survivors remain and pending request respects lower cap')
 cap(4);s=settled(4);manual=next(a for a in s['actors']if a['characterId']==1052);check(manual['manualEntry'] and queued(s)==[],'Raising the cap admits queued manual choice before random models')
 cap(1);s=settled(1);check(s['actors'][0]['token']==manual['token'],'Lowering also limits manual actors and prefers explicit choices')
 send('enter',characterId=1026);wait(lambda s:queued(s)==[1026]);cap(2);s=settled(2);check(all(a['manualEntry']for a in s['actors']),'Two explicit choices share the same total cap')
 cap(1);s=settled(1);check(s['actors'][0]['characterId']==1052,'Manual-only overflow retires newest and preserves one actor')
 send('enter',characterId=1028);wait(lambda s:queued(s)==[1028]);send('retire',token=s['actors'][0]['token']);s=wait(lambda s:s['count']==1 and not s['loading'] and s['actors'][0]['characterId']==1028)
 check(s['actors'][0]['manualEntry'] and not queued(s),'Cap one waits for complete departure before next manual arrival')
 filter('Include',[]);send('retire',token=s['actors'][0]['token']);settled(0);cap(10);filter('Include',[1001]);s=settled(10)
 check(s['includeVariants'] and not s['namesUnique'] and all(a['characterId']==1001 for a in s['actors']) and len({a['costumeId']for a in s['actors']})==10,'Include one fills ten slots with ten different outfits')
 # Costume replacement at a full cap uses the same slot.
 old=s['actors'][0];others={a['token']for a in s['actors']if a['token']!=old['token']};loaded=s['charactersLoaded'];send('costume',token=old['token']);s=wait(lambda s:s['charactersLoaded']==loaded+1 and not s['loading'] and s['count']==10)
 new=next(a for a in s['actors']if a['ordinal']==old['ordinal']);check(new['costumeId']!=old['costumeId'] and others.issubset({a['token']for a in s['actors']}),'Full-ten outfit replacement retains the other nine instances')
 # Lower the cap while a second replacement is in its fade/loading transaction.
 send('costume',token=new['token']);wait(lambda s:s['loading']);cap(1);s=settled(1)
 check(s['actors'][0]['ordinal']==old['ordinal'] and s['namesUnique'] and not s['includeVariants'],'Reducing cap during outfit replacement keeps one valid survivor')
 pause(True);cap(10);cap(3);rows=observe(.8);check(all(s['count']==1 and s['manualPaused'] for s in rows),'Changing limits while paused does not spawn or advance models')
 cap(10);pause(False);s=settled(10);check(s['includeVariants'],'Resume fills the latest requested cap')
 filter('Include',ids[:6]);s=current();check(s['includeVariants'] and s['targetCount']==10,'Include six below cap ten allows outfit variants')
 filter('Include',ids);s=settled(10);check(not s['includeVariants'] and s['namesUnique'],'Include ten removes excess copies and fills unique names')
 filter('Exclude',exclude=ids[2:]);s=settled(2);check(s['targetCount']==2 and {a['characterId']for a in s['actors']}==set(ids[:2]),'Exclude with only two eligible models stops at two')
 filter('Include',[]);s=settled(0);check(s['targetCount']==0,'Empty Include remains empty at cap ten')
 # Invalid command values cannot escape the supported range.
 cap(99);check(current()['maxActors']==10,'Oversized command clamps to ten');cap(0);check(current()['maxActors']==1,'Zero command clamps to one')
 cap(10);filter('All');s=settled(10);check(json.loads(settings.read_text(encoding='utf-8-sig'))['MaxActors']==10,'Latest chosen cap persists to disk')
 # Exercise the larger fleet on all floor types, including opt-in avoidance.
 plane={'mode':'Planes','line':[],'surfaces':[{'points':[{'x':.08,'y':.25},{'x':.92,'y':.25},{'x':.92,'y':.95},{'x':.08,'y':.95}],'entrance':{'edge':3,'start':.15,'end':.85},'exit':{'edge':1,'start':.15,'end':.85}}]}
 for mode,ground in [('Planes',plane),('Default',{'mode':'Default','line':[],'surfaces':[]}),('Line',line)]:
  send('ground',ground=ground);wait(lambda s:s['ground']['mode']==mode);rows=observe(1.2);check(all(s['count']==10 and s['occupancyValid']for s in rows),'Ten models remain valid on '+mode)
  if mode=='Planes':
   send('avoidance',avoid3DCollisions=True);wait(lambda s:s['avoid3DCollisions']);rows=observe(2);check(all(not s['error']for s in rows),'Experimental avoidance accepts ten models');send('avoidance',avoid3DCollisions=False);wait(lambda s:not s['avoid3DCollisions'])
 # Persist and restart using the same real settings file.
 stop();s=launch(False);check(s['maxActors']==10 and not s['avoid3DCollisions'],'Restart retains cap ten and default-off avoidance')
 s=settled(10);oldest=min(s['actors'],key=lambda a:a['ordinal'])['token'];loaded=s['charactersLoaded'];send('enter',characterId=1052);wait(lambda s:queued(s)==[1052]);s=wait(lambda s:s['retiringCount']>0)
 check(any(a['token']==oldest and a['phase']=='Exiting'for a in s['actors']),'Natural full-ten rotation retires FIFO oldest')
 s=wait(lambda s:s['count']==10 and not s['loading'] and any(a['characterId']==1052 for a in s['actors']),75)
 check(s['departures']>=1 and s['charactersLoaded']==loaded+1 and not queued(s),'Natural departure releases queued entry without an eleventh actor')
 report['Passed']=True
finally:
 stop();settings.write_bytes(original);report.update(checks=checks,RuntimeAssemblySha256=hashlib.sha256((b/'UmaDesktop_Data/Managed/umamusume.dll').read_bytes()).hexdigest().upper(),OriginalSettingsRestored=settings.read_bytes()==original,PhysicalMouseInjection=False)
 (out/'population-limit-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');(out/'population-observations.json').write_text(json.dumps(samples,ensure_ascii=False),encoding='utf-8')
