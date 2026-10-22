"""Real Unity lifecycle, queue and portal-fade tests through local commands.
Controller button handlers are covered by its independent self-test.
"""
import argparse,datetime,hashlib,json,subprocess,time
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--build-directory',default=str(Path(__file__).resolve().parents[2]/'build'/'UmaWalking'));p.add_argument('--evidence-directory',required=True);args=p.parse_args()
b=Path(args.build_directory);out=Path(args.evidence_directory);out.mkdir(parents=True,exist_ok=True);settings=b/'DesktopSettings.json';original=settings.read_bytes();checks=[];samples=[];last=0;process=None;started=None;report={'Passed':False,'PhysicalMouseInjection':False}
plane={'mode':'Planes','line':[],'surfaces':[{'points':[{'x':.12,'y':.4},{'x':.88,'y':.4},{'x':.88,'y':.9},{'x':.12,'y':.9}],'entrance':{'edge':3,'start':.35,'end':.65},'exit':{'edge':3,'start':.35,'end':.65}}]}
def send(action,token=0,**values):
 global last
 last=max(last+1,time.time_ns());f=b/'DesktopCommands'/(str(last)+'.tmp');f.write_text(json.dumps(dict(action=action,token=token,**values)),encoding='utf-8');f.replace(f.with_suffix('.json'))
def read():
 try:
  s=json.loads((b/'DesktopLogs/state.json').read_text(encoding='utf-8-sig'))
  if datetime.datetime.fromisoformat(s['utc'].replace('Z','+00:00'))<started:return None
  if s['error']:raise RuntimeError(s['error'])
  if s['count']>6 or not s['namesUnique']:raise AssertionError('Population invariant failed')
  samples.append(s);return s
 except(OSError,json.JSONDecodeError):return None
def wait(fn,seconds=65):
 end=time.monotonic()+seconds;s=None
 while time.monotonic()<end:
  s=read()
  if s and fn(s):return s
  if process.poll() is not None:raise AssertionError('Player exited')
  time.sleep(.12)
 raise AssertionError('Timeout '+json.dumps(s))
def check(ok,label,**values):
 checks.append(dict(Passed=bool(ok),Check=label,**values));print(json.dumps(checks[-1]),flush=True)
 if not ok:raise AssertionError(label)
def actor(s,token):return next((a for a in s['actors']if a['token']==token),None)
def character(s,id):return next((a for a in s['actors']if a['characterId']==id),None)
def queued(s):return [e['characterId']for e in s.get('pendingEntries',[])]
def observe(seconds):
 end=time.monotonic()+seconds;rows=[]
 while time.monotonic()<end:
  s=read()
  if s:rows.append(s)
  time.sleep(.2)
 return rows
def stop():
 global process
 if process and process.poll() is None:
  send('exit')
  try:process.wait(timeout=15)
  except subprocess.TimeoutExpired:process.kill();process.wait()
 process=None
def launch(stable,fade):
 global process,started
 data=json.loads(original.decode('utf-8-sig'));data['Ground']=plane;data['FadeSeconds']=fade;data['ModelSizeScale']=1
 settings.write_text(json.dumps(data),encoding='utf-8');started=datetime.datetime.now(datetime.timezone.utc)
 si=subprocess.STARTUPINFO();si.dwFlags|=subprocess.STARTF_USESHOWWINDOW;si.wShowWindow=0
 flags=['--desktop-test-stable-population','--desktop-test-long-gait']if stable else []
 process=subprocess.Popen([str(b/'UmaDesktop.exe'),'-force-d3d11','-force-d3d11-bitblt-model','--desktop-test-mode','--desktop-test-ignore-occlusion','--desktop-no-controller',*flags,'-logFile',str(b/f'DesktopLogs/Player-manual-{stable}.log')],cwd=b,startupinfo=si)
 return wait(lambda s:s['count']>0 and not s['loading'])
def retire_and_observe(token):
 before=wait(lambda s:actor(s,token)is not None);send('retire',token);s=wait(lambda s:actor(s,token)is None or actor(s,token)['phase']=='Exiting')
 a=actor(s,token);check(a is not None,'Selected actor walks to exit rather than disappearing immediately')
 check(a['state']!='Special','Explicit exit interrupts the selected special immediately')
 rows=[];end=time.monotonic()+65
 while time.monotonic()<end:
  s=read()
  if not s:continue
  a=actor(s,token)
  if a is None:break
  rows.append(a);time.sleep(.12)
 else:raise AssertionError('Selected actor did not finish exit')
 check(any(0.02<a['portalOpacity']<.98 for a in rows),'Visible exit contains intermediate fade values')
 check(max((abs(a['screenX']-rows[0]['screenX'])for a in rows),default=0)>10,'Exiting actor really moves before removal')
 return s
try:
 s=launch(True,1.2)
 # Empty Include isolates manual choices and proves they do not edit the random list.
 send('filter',mode='Include',includeIds=[],excludeIds=[]);wait(lambda s:s['count']==0 and not s['loading'])
 begin=len(samples);send('enter',characterId=1026);s=wait(lambda s:character(s,1026)is not None and not s['loading']);bourbon=character(s,1026)['token']
 check(s['count']==1 and character(s,1026)['manualEntry'],'Named actor enters immediately when there is a slot')
 check(s['mode']=='Include' and s['includeIds']==[],'Manual choice leaves the random filter unchanged')
 check(s['nextEventIn']>590,'Successful immediate entry resets population timer')
 wait(lambda s:actor(s,bourbon)['phase']=='Active');entryRows=[actor(s,bourbon)for s in samples[begin:]if actor(s,bourbon)]
 check(any(0.02<a['portalOpacity']<.98 for a in entryRows),'Visible entry contains intermediate fade values')
 send('enter',characterId=1052);s=wait(lambda s:character(s,1052)is not None and not s['loading']);urara=character(s,1052)['token']
 check(s['count']==2 and s['nextEventIn']>590,'Another successful entry restarts the entire timer interval')
 wait(lambda s:actor(s,urara)['phase']=='Active');send('special',bourbon);wait(lambda s:actor(s,bourbon)['state']=='Special');retire_and_observe(bourbon)
 send('retire',bourbon);s=observe(.8)[-1];check(actor(s,urara)is not None and actor(s,urara)['phase']=='Active','A stale selected token never retires a different actor')
 send('pause');wait(lambda s:s['manualPaused']);send('enter',characterId=1008);wait(lambda s:queued(s)==[1008]);rows=observe(1.1)
 check(all(s['count']==1 and queued(s)==[1008]for s in rows),'Manual entry waits safely during global pause')
 send('pause');s=wait(lambda s:character(s,1008)is not None and not s['loading']);bakushin=character(s,1008)['token'];check(True,'Queued entry starts after resuming')
 wait(lambda s:actor(s,urara)['phase']=='Active');version=actor(read(),urara)['poseVersion'];send('testGroundPose',urara,groundU=.85,groundV=.85);wait(lambda s:actor(s,urara)['poseVersion']>version)
 send('retire',urara);wait(lambda s:actor(s,urara)['phase']=='Exiting');send('enter',characterId=1002);s=wait(lambda s:character(s,1002)is not None and not s['loading'])
 check(actor(s,urara)is not None,'A vacant slot admits a manual actor while another is still leaving')
 wait(lambda s:actor(s,urara)is None);s=read();check(s['nextEventIn']>570,'Completing an older departure does not replace the reset entry timer with an empty gap')
 # Both screen-bottom and drawn-line paths use gradual entry/exit too.
 for mode in ['Default','Line']:
  config={'mode':mode,'line':[{'x':.2,'y':.8},{'x':.8,'y':.8}],'surfaces':[]};begin=len(samples);send('ground',ground=config)
  s=wait(lambda s:s['ground']['mode']==mode and actor(s,bakushin)is not None and actor(s,bakushin)['phase']=='Active')
  check(any(actor(z,bakushin)and .02<actor(z,bakushin)['portalOpacity']<.98 for z in samples[begin:]),'Entry fade also works on '+mode)
  if mode=='Default':retire_and_observe(bakushin);bakushin=next(a['token']for a in read()['actors'])
  else:retire_and_observe(bakushin)
 stop()
 # Real natural FIFO rotation, with six present and two named requests waiting.
 s=launch(False,.5);s=wait(lambda s:s['count']==6 and not s['loading']);oldest=min(s['actors'],key=lambda a:a['ordinal'])['token'];deadline=s['simulationSeconds']+s['nextEventIn'];loads=s['charactersLoaded']
 for id in [1026,1052,1026,s['actors'][0]['characterId'],999999]:send('enter',characterId=id)
 s=wait(lambda s:queued(s)==[1026,1052]);check(abs(s['simulationSeconds']+s['nextEventIn']-deadline)<.6,'Enqueueing at full capacity does not reset or accelerate natural retirement')
 rows=observe(1.2);check(all(s['count']==6 and queued(s)==[1026,1052] and all(a['phase']!='Exiting'for a in s['actors'])for s in rows),'Full screen queues requests without evicting actors or creating a seventh')
 s=wait(lambda s:any(a['phase']=='Exiting'for a in s['actors']));check(next(a['token']for a in s['actors']if a['phase']=='Exiting')==oldest,'The ordinary FIFO actor retires naturally')
 s=wait(lambda s:character(s,1026)is not None and not s['loading'],80);check(s['count']==6 and queued(s)==[1052]and character(s,1052)is None,'First released slot goes to the first manual request')
 check(s['nextEventIn']>10,'Dequeued successful entry resets next rotation timer')
 s=wait(lambda s:character(s,1052)is not None and not s['loading'],100);check(s['count']==6 and queued(s)==[]and s['charactersLoaded']==loads+2,'Second request gets the next slot before random replacement')
 check(s['nextEventIn']>10 and s['namesUnique'],'Final entry resets timer and preserves unique names')
 report['Passed']=True
finally:
 stop();settings.write_bytes(original);report.update(checks=checks,runtimeAssemblySha256=hashlib.sha256((b/'UmaDesktop_Data/Managed/umamusume.dll').read_bytes()).hexdigest().upper(),OriginalSettingsRestored=settings.read_bytes()==original)
 (out/'manual-entries-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8');(out/'manual-entries-observations.json').write_text(json.dumps(samples,indent=2),encoding='utf-8')
