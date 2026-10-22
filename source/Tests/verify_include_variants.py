"""Real Include 1/2/5/6-model costume instances and transitions; no OS mouse injection."""
import argparse,json,hashlib,time,subprocess,traceback,collections
from PIL import Image
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--build-directory',default=str(Path(__file__).resolve().parents[2]/'build'/'UmaWalking'));p.add_argument('--evidence-directory',required=True);args=p.parse_args();b=Path(args.build_directory);out=Path(args.evidence_directory);out.mkdir(parents=True,exist_ok=True);file=b/'DesktopSettings.json';original=file.read_bytes();process=None;checks=[];samples=[];serial=0;report={'Passed':False,'PhysicalMouseInjection':False}
def check(value,text,**details):
 checks.append(dict(Passed=bool(value),Check=text,**details));print(json.dumps(checks[-1]),flush=True)
 if not value:raise AssertionError(text)
def send(action,**kw):
 global serial
 serial=max(serial+1,time.time_ns());f=b/'DesktopCommands'/(str(serial)+'.tmp');f.write_text(json.dumps(dict(action=action,**kw)),encoding='utf-8');f.replace(f.with_suffix('.json'))
def read():
 try:s=json.loads((b/'DesktopLogs/state.json').read_text(encoding='utf-8-sig'))
 except(OSError,json.JSONDecodeError):return None
 if s['processId']!=process.pid:return None
 if s['error']:raise RuntimeError(s['error'])
 if s['count']>6 or not s['occupancyValid'] or not s['costumesUnique']:raise AssertionError('Occupancy invariant '+json.dumps(s))
 samples.append(s);return s
def wait(fn,seconds=70):
 end=time.monotonic()+seconds;s=None
 while time.monotonic()<end:
  s=read()
  if s and fn(s):return s
  if process.poll() is not None:raise AssertionError('Player exited')
  time.sleep(.15)
 raise AssertionError('Timeout: '+json.dumps(s,ensure_ascii=False))
def actor(s,token):return next((a for a in s['actors']if a['token']==token),None)
def phase_filter(ids):
 send('filter',mode='Include',includeIds=ids,excludeIds=[]);return wait(lambda s:s['mode']=='Include' and s['includeIds']==ids)
def stop():
 global process
 if process and process.poll() is None:
  send('exit')
  try:process.wait(timeout=15)
  except subprocess.TimeoutExpired:process.kill();process.wait()
def launch(normal=False):
 global process
 flags=['--desktop-no-controller']
 if not normal:flags+=['--desktop-test-mode','--desktop-test-ignore-occlusion','--desktop-test-stable-population','--desktop-test-long-gait']
 process=subprocess.Popen([str(b/'UmaDesktop.exe'),'-force-d3d11','-force-d3d11-bitblt-model',*flags,'-logFile',str(out/('include-player-'+str(time.time_ns())+'.log'))],cwd=b)
def uniform(s,ids,count=6):return s['count']==count and not s['loading'] and all(a['characterId']in ids and a['phase']!='Exiting' for a in s['actors'])
def keep_one_per_name(s):
 kept=set()
 for a in sorted(s['actors'],key=lambda a:(not a['manualEntry'],a['ordinal'])):
  if a['name']in kept:send('retire',token=a['token'])
  else:kept.add(a['name'])
try:
 cfg=json.loads(original.decode('utf-8-sig'));cfg.update(Ground={'mode':'Line','line':[{'x':.08,'y':.9},{'x':.92,'y':.9}],'surfaces':[]},MaxActors=6,InteractionProbability=0,SpawnMinSeconds=1,SpawnMaxSeconds=1.2,FullHoldMinSeconds=600,FullHoldMaxSeconds=600,EmptyGapMinSeconds=1,EmptyGapMaxSeconds=1.1,CharacterMinSeconds=600,CharacterMaxSeconds=600,SelectionMode='All')
 file.write_text(json.dumps(cfg),encoding='utf-8');launch();s=wait(lambda s:s['count']>=1 and not s['loading']);phase_filter([1001]);s=wait(lambda s:uniform(s,[1001]));
 check(s['includeVariants'] and s['targetCount']==6,'Include one character fills six slots')
 check(len({a['costumeId']for a in s['actors']})==6 and not s['namesUnique'],'All six copies share the character and have different outfits',Costumes=[a['costumeId']for a in s['actors']])
 check(len({a['token']for a in s['actors']})==6 and len({a['nativeWindow']for a in s['actors']})==6 and all(a['desktopAttached'] and a['presentedFrames']>0 for a in s['actors']),'Copies have independent rendering windows and tokens')
 send('capture');time.sleep(1)
 for a in s['actors']:
  image=b/'DesktopLogs'/('capture-'+str(a['token'])+'.png')
  with Image.open(image) as pixels:check(pixels.convert('RGBA').getchannel('A').getbbox() is not None,'Rendered repeated model contains visible pixels',Token=a['token'],Costume=a['costumeId'])
  (out/('include-six-'+str(a['token'])+'.png')).write_bytes(image.read_bytes())
 check(all(a['expressionMatchesPreset'] for a in s['actors']),'Repeated characters retain independent valid expressions')
 tokens={a['token']for a in s['actors']};a=s['actors'][0];other=s['actors'][1];loads=s['charactersLoaded'];send('costume',token=a['token'],costumeId=other['costumeId']);s=wait(lambda s:'该服装已被' in s['message'])
 check(s['charactersLoaded']==loads and {a['token']for a in s['actors']}==tokens and not s['loading'],'Explicit occupied outfit is rejected without losing or reloading a model')
 selected=a['token'];ordinal=a['ordinal'];old=a['costumeId'];send('costume',token=selected);s=wait(lambda s:uniform(s,[1001]) and actor(s,selected) is None);a=next(a for a in s['actors']if a['ordinal']==ordinal)
 check(a['costumeId']!=old and len({a['costumeId']for a in s['actors']})==6,'Random outfit change avoids other copies and its previous outfit')
 check({a['token']for a in s['actors']} - {a['token']}==tokens-{selected},'Changing an outfit preserves the other five instances')
 clicked=a['token'];count=s['clickSpecials'];send('special',token=clicked);s=wait(lambda s:s['clickSpecials']==count+1 and actor(s,clicked)['state']=='Special')
 check(sum(a['state']=='Special' for a in s['actors'])==1,'Click/special command targets only the selected copy')
 before={a['token']for a in s['actors']};send('enter',characterId=1001);s=wait(lambda s:[e['characterId']for e in s['pendingEntries']]==[1001]);time.sleep(.6);s=read()
 check(s['count']==6 and {a['token']for a in s['actors']}==before,'Manual repeated entry queues at six without exceeding the cap')
 exit_token=next(a['token']for a in s['actors']if a['token']!=clicked);send('retire',token=exit_token);s=wait(lambda s:actor(s,exit_token) is not None and actor(s,exit_token)['phase']=='Exiting')
 check(s['count']==6,'Selected copy walks to the exit before its slot is released')
 s=wait(lambda s:uniform(s,[1001]) and not s['pendingEntries'] and actor(s,exit_token) is None and any(a['manualEntry']for a in s['actors']))
 check(s['count']==6 and len({a['costumeId']for a in s['actors']})==6,'Queued copy enters with a distinct outfit after the selected exit')
 phase_filter([1001,1002]);s=read();keep_one_per_name(s);s=wait(lambda s:uniform(s,[1001,1002]) and len({a['characterId']for a in s['actors']})==2)
 counts=collections.Counter(a['characterId']for a in s['actors']);check(sorted(counts.values())==[3,3],'Include two characters distributes new copies evenly',Counts=dict(counts))
 phase_filter([1001,1002,1003,1004,1006]);s=read();keep_one_per_name(s);s=wait(lambda s:uniform(s,[1001,1002,1003,1004,1006]) and len({a['characterId']for a in s['actors']})==5)
 check(sorted(collections.Counter(a['characterId']for a in s['actors']).values())==[1,1,1,1,2],'Include five characters fills the sixth slot with a new outfit')
 phase_filter([1001,1002,1003,1004,1006,1008]);s=wait(lambda s:uniform(s,[1001,1002,1003,1004,1006,1008]) and s['namesUnique'])
 check(not s['includeVariants'] and len({a['characterId']for a in s['actors']})==6,'Include six characters returns to unique names, with excess copies walking out')
 phase_filter([]);s=read()
 for a in s['actors']:
  if a['manualEntry']:send('retire',token=a['token'])
 s=wait(lambda s:s['count']==0 and s['targetCount']==0 and not s['loading']);time.sleep(1.5)
 check(read()['count']==0 and not s['includeVariants'],'Empty Include stays empty after manual choices exit')
 send('filter',mode='Exclude',includeIds=[],excludeIds=[1001,1002]);s=wait(lambda s:s['mode']=='Exclude' and s['count']==6 and not s['loading'])
 check(s['namesUnique'] and not s['includeVariants'] and not any(a['characterId']in [1001,1002]for a in s['actors']),'Exclude preserves unique names and excluded IDs')
 send('filter',mode='All',includeIds=[],excludeIds=[]);s=wait(lambda s:s['mode']=='All');check(not s['includeVariants'] and s['namesUnique'],'All-random still prevents duplicate characters')
 check(all(s['count']<=6 and s['costumesUnique'] and s['occupancyValid'] for s in samples),'All observed transitions preserve the six-model cap and distinct outfits',Samples=len(samples))
 stop();cfg.update(SelectionMode='Include',IncludeIds=[1001],ExcludeIds=[]);file.write_text(json.dumps(cfg),encoding='utf-8');launch(True);s=wait(lambda s:s['includeVariants'] and s['mode']=='Include' and s['targetCount']==6 and(s['count']>0 or s['desktopPaused']))
 check(not s['testIgnoresOcclusion'] and all(a['characterId']==1001 for a in s['actors']),'Saved single-character Include is recognized at normal startup')
 report['Passed']=True
except Exception:
 report['Failure']=traceback.format_exc();raise
finally:
 stop();file.write_bytes(original);report.update(checks=checks,OriginalSettingsRestored=file.read_bytes()==original,runtimeAssemblySha256=hashlib.sha256((b/'UmaDesktop_Data/Managed/umamusume.dll').read_bytes()).hexdigest().upper())
 (out/'include-variants-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');(out/'include-variants-observations.json').write_text(json.dumps(samples,ensure_ascii=False),encoding='utf-8')
