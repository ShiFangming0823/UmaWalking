import json,time,datetime,subprocess,hashlib,math,argparse
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
def send(action,token=0):
 name=str(time.time_ns());p=base/'DesktopCommands'/(name+'.tmp');p.write_text(json.dumps({'action':action,'token':token}),encoding='utf-8');p.replace(p.with_suffix('.json'))
def actor(s,token):return next(a for a in s['actors'] if a['token']==token)
def gap(a,b):return abs(a['clearanceX']-b['clearanceX'])-a['clearanceHalfWidth']-b['clearanceHalfWidth']
try:
 si=subprocess.STARTUPINFO();si.dwFlags=subprocess.STARTF_USESHOWWINDOW;si.wShowWindow=0
 process=subprocess.Popen([str(base/'UmaDesktop.exe'),'-force-d3d11','-force-d3d11-bitblt-model','--desktop-test-mode','--desktop-test-ignore-occlusion','--desktop-max-actors','2','--desktop-no-controller','--desktop-exit-seconds','160','-logFile',str(base/'DesktopLogs/Player-v6-spacing.log')],cwd=base,startupinfo=si)
 s=wait(lambda s:s['count']==2 and not s['loading'] and not s['paused'],60)
 first,second=s['actors'];token=first['token'];profiles={a['characterId']:(a['walkSpeedFactor'],a['runSpeedFactor']) for a in s['actors']}
 for a in s['actors']:
  check(.8<=a['walkSpeedFactor']<=1.2 and .8<=a['runSpeedFactor']<=1.2,'Per-character walk and run factors stay inside 80-120 percent',character=a['characterId'],walk=a['walkSpeedFactor'],run=a['runSpeedFactor'],halfWidth=a['clearanceHalfWidth'])
 check(profiles[first['characterId']]!=profiles[second['characterId']],'Different characters receive different sampled profiles')
 send('testSpacing');s=wait(lambda s:actor(s,token)['specialDeferrals']>=1 and actor(s,token)['specialPending'])
 a,b=s['actors'];check(a['state'] in ('Walk','Run') and gap(a,b)<20,'Blocked automatic action keeps walking/running and stays pending')
 check(a['specialRetryDistance']>0,'Blocked action retries after additional travel, not a standing pause')
 send('testNativeClick',token);s=wait(lambda s:s['clickSpecials']>=1 and actor(s,token)['state']=='Special')
 a=actor(s,token);b=next(x for x in s['actors'] if x['token']!=token)
 check(not a['automaticSpecial'] and gap(a,b)<20,'Native click callback immediately starts a special despite occlusion',gap=gap(a,b))
 previous=a['specialDeferrals'];send('testSpacing');wait(lambda s:actor(s,token)['specialDeferrals']>previous and actor(s,token)['specialPending'])
 s=wait(lambda s:actor(s,token)['automaticSpecial'],30);a=actor(s,token);b=next(x for x in s['actors'] if x['token']!=token)
 check(gap(a,b)>=19.9,'Pending automatic special starts only after both full-pose envelopes clear',gap=gap(a,b))
 count=a['automaticSpecials'];send('testApproachSpecial');minGap=1e9;sawReverse=False;seenSpecial=False;end=time.monotonic()+5
 while time.monotonic()<end:
  s=read();a=actor(s,token);b=next(x for x in s['actors'] if x['token']!=token);samples.append(s)
  if a['automaticSpecial'] and a['automaticSpecials']==count:
   seenSpecial=True;minGap=min(minGap,gap(a,b));sawReverse|=b['direction']==1
  if seenSpecial and not a['automaticSpecial']:break
  time.sleep(.05)
 check(seenSpecial and minGap>=19.9,'Approaching runner never enters a reserved automatic performance',minimumGap=minGap)
 check(sawReverse,'Approaching runner turns away before crossing the performer')
 check(s['trimmedSpecials']>=1 and a['specialElapsed']>=a['specialCutoff'],'Automatic performance still exits at its early cutoff')
 # Costume replacement keeps the character's sampled profile.
 character=a['characterId'];before=profiles[character];send('costume',token)
 s=wait(lambda s:any(x['characterId']==character and x['token']!=token for x in s['actors']) and not s['loading'],40)
 changed=next(x for x in s['actors'] if x['characterId']==character)
 check(before==(changed['walkSpeedFactor'],changed['runSpeedFactor']),'Changing clothes preserves this character speed profile')
 for state in samples:
  for x in state['actors']:
   if x['state'] in ('Walk','Run'):
    factor=x['walkSpeedFactor'] if x['state']=='Walk' else x['runSpeedFactor'];expected=(65 if x['state']=='Walk' else 260)*x['height']/380*factor
    if abs(x['speed']-expected)>.02:raise AssertionError('Speed/profile mismatch '+str(x))
 check(True,'Walk/run displacement uses sampled speed and preserves model size scaling')
 report['Passed']=True;report['minimumProtectedGap']=minGap
finally:
 if process and process.poll() is None:
  send('exit')
  try:process.wait(timeout=8)
  except subprocess.TimeoutExpired:process.terminate();process.wait()
 report.update({'checks':checks,'runtimeAssemblySha256':hashlib.sha256((base/'UmaDesktop_Data/Managed/umamusume.dll').read_bytes()).hexdigest().upper()})
 (out/'spacing-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
 (out/'spacing-snapshots.json').write_text(json.dumps(samples,ensure_ascii=False,indent=2),encoding='utf-8')
 print('FINAL '+json.dumps({'Passed':report['Passed'],'Checks':len(checks)}),flush=True)
