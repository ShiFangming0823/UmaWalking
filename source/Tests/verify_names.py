"""Name changes on real six-copy runtime, keeping original identities and outfit names."""
import argparse,json,time,subprocess,hashlib,traceback,ctypes
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--build-directory',default=str(Path(__file__).resolve().parents[2]/'build'/'UmaWalking'));p.add_argument('--evidence-directory',required=True);p.add_argument('--name-table',required=True);a=p.parse_args();b=Path(a.build_directory);out=Path(a.evidence_directory);out.mkdir(parents=True,exist_ok=True);config=b/'DesktopSettings.json';original=config.read_bytes();process=None;checks=[];samples=[];serial=0;names={c['id']:c for c in json.loads(Path(a.name_table).read_text(encoding='utf-8-sig'))};report={'Passed':False,'PhysicalMouseInjection':False}
user32=ctypes.WinDLL('user32',use_last_error=True);user32.GetWindowTextW.argtypes=[ctypes.c_void_p,ctypes.c_wchar_p,ctypes.c_int]
def title(handle):
 text=ctypes.create_unicode_buffer(512);user32.GetWindowTextW(handle,text,512);return text.value
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
 if s['count']>6 or not s['occupancyValid'] or not s['costumesUnique']:raise AssertionError('Occupancy')
 samples.append(s);return s
def wait(fn,seconds=60):
 end=time.monotonic()+seconds;s=None
 while time.monotonic()<end:
  s=read()
  if s and fn(s):return s
  time.sleep(.15)
 raise AssertionError('Timeout '+json.dumps(s,ensure_ascii=False))
try:
 cfg=json.loads(original.decode('utf-8-sig'));cfg.update(Ground={'mode':'Default','line':[],'surfaces':[]},UiLanguage='zh',MaxActors=6,InteractionProbability=0);config.write_text(json.dumps(cfg),encoding='utf-8')
 process=subprocess.Popen([str(b/'UmaDesktop.exe'),'-force-d3d11','-force-d3d11-bitblt-model','--desktop-test-mode','--desktop-test-ignore-occlusion','--desktop-test-stable-population','--desktop-test-long-gait','--desktop-no-controller','-logFile',str(out/'names-player.log')],cwd=b)
 s=wait(lambda s:s['count']>=1 and not s['loading']);send('filter',mode='Include',includeIds=[1001],excludeIds=[]);s=wait(lambda s:s['count']==6 and not s['loading'] and all(x['characterId']==1001 for x in s['actors']))
 identity={x['token']:(x['characterId'],x['name'],x['costumeId'],x['costumeName'])for x in s['actors']};native={x['token']:x['nativeWindow'] for x in s['actors']};clips={x['token']:x['clip']for x in s['actors']}
 for lang in ['en','ja','zh']:
  send('language',language=lang);s=wait(lambda s:s['language']==lang and all(x['displayName']==names[x['characterId']][lang]for x in s['actors']))
  check({x['token']:(x['characterId'],x['name'],x['costumeId'],x['costumeName'])for x in s['actors']}==identity,'Live language switch preserves resource identities and all Japanese outfit text',Language=lang)
  check(all(title(x['nativeWindow']).find(names[x['characterId']][lang])>=0 for x in s['actors']),'Every native actor window title follows the language',Language=lang)
  check({x['token']:x['nativeWindow']for x in s['actors']}==native and {x['token']:x['clip']for x in s['actors']}==clips,'Language switch keeps actor windows and animations alive',Language=lang)
  check(json.loads(config.read_text(encoding='utf-8-sig'))['UiLanguage']==lang,'Language selection persists',Language=lang)
 send('filter',mode='All',includeIds=[],excludeIds=[]);s=wait(lambda s:s['mode']=='All' and not s['loading']);selected=min(s['actors'],key=lambda x:x['ordinal']);catalog=json.loads((b/'DesktopLogs/costume-catalog.json').read_text(encoding='utf-8-sig'))['characters'];latest=next(c for c in catalog if c['id']==1149);outfit=next(c['id']for c in latest['costumes']if len(c['id'])<4)
 send('testCharacter',token=selected['token'],characterId=1149,costumeId=outfit);s=wait(lambda s:not s['loading'] and any(x['characterId']==1149 for x in s['actors']))
 check(next(x for x in s['actors']if x['characterId']==1149)['displayName']=='蝴蝶兰','Newest available model has its completed Chinese name')
 send('language',language='en');s=wait(lambda s:s['language']=='en');x=next(x for x in s['actors']if x['characterId']==1149)
 check(x['displayName']=='Phalaenopsis' and 'Phalaenopsis'in title(x['nativeWindow']),'Newest model English name appears without reloading')
 check(next(c for c in json.loads((b/'DesktopLogs/costume-catalog.json').read_text(encoding='utf-8-sig'))['characters']if c['id']==1149)==latest,'Raw catalog and costume names remain unchanged')
 report['Passed']=True
except Exception:
 report['Failure']=traceback.format_exc();raise
finally:
 if process and process.poll() is None:
  send('exit')
  try:process.wait(timeout=15)
  except subprocess.TimeoutExpired:process.kill();process.wait()
 config.write_bytes(original);report.update(checks=checks,OriginalSettingsRestored=config.read_bytes()==original,runtimeAssemblySha256=hashlib.sha256((b/'UmaDesktop_Data/Managed/umamusume.dll').read_bytes()).hexdigest().upper());(out/'names-runtime-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');(out/'names-runtime-observations.json').write_text(json.dumps(samples,ensure_ascii=False),encoding='utf-8')
