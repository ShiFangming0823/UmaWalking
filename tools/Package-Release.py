"""Create local release assets from a sanitized, built UmaWalking publication tree."""
from pathlib import Path
import argparse, hashlib, json, re, subprocess, sys, zipfile

def sha(path):
    h=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(1048576),b''):h.update(block)
    return h.hexdigest().upper()

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[1])
    root=parser.parse_args().root.resolve()
    product=(root/'source/Assets/Scripts/DesktopPrototype/DesktopProduct.cs').read_text(encoding='utf-8-sig')
    version=re.search(r'Version="(\d+\.\d+\.\d+)"',product).group(1)
    defaults=json.loads((root/'tools/Default-Settings.json').read_text(encoding='utf-8-sig'))
    assert json.loads((root/'DesktopSettings.json').read_text(encoding='utf-8-sig'))==defaults,'Do not package personal settings. Use the sanitized publication tree.'
    allowed_dirs={'source','tools','docs','licenses','.github','UmaDesktop_Data','MonoBleedingEdge'}
    allowed_files={'README.md','AGENTS.md','DesktopSettings.json','Launch-UmaWalking.cmd','Launch-Low-Power.cmd',
        'UmaDesktop.exe','UmaDesktopController.exe','UnityPlayer.dll','UnityCrashHandler64.exe','.gitignore','.gitattributes','publication-status.json'}
    files=sorted(p for p in root.rglob('*') if p.is_file() and
        (p.relative_to(root).parts[0] in allowed_dirs or p.relative_to(root).as_posix() in allowed_files))
    assert not any('__pycache__' in p.parts or p.suffix=='.pyc' for p in files),'Remove generated Python caches from the publication first.'
    manifest={'Project':'UmaWalking','Version':version,'GameDataIncluded':False,
        'UpstreamRevision':'07f82e9fa08f23c1cda3a9be3045a09372eae8bb','RuntimeBuild':'Windows x64 Mono; release version '+version,
        'RedistributionPermissionConfirmed':False,
        'Files':[{'Path':p.relative_to(root).as_posix(),'Bytes':p.stat().st_size,'SHA256':sha(p)} for p in files]}
    (root/'release-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    subprocess.run([sys.executable,str(root/'tools/Verify-Publication.py'),str(root)],check=True)
    files.append(root/'release-manifest.json')
    dist=root/'dist';dist.mkdir(exist_ok=True);sums=[]
    runtime_dirs={'UmaDesktop_Data','MonoBleedingEdge'}
    runtime_names={'UmaDesktop.exe','UmaDesktopController.exe','UnityPlayer.dll','UnityCrashHandler64.exe'}
    for kind in ['Windows-x64','Source','Publication']:
        name=f'UmaWalking-v{version}-{kind}.zip';path=dist/name;selected=[]
        for p in files:
            first=p.relative_to(root).parts[0]
            # Omitting the live config protects users who unpack over an older
            # installation. A fresh launch creates it, or restores the profile.
            if kind=='Windows-x64' and first in {'source','tools','.github','.gitignore','.gitattributes','DesktopSettings.json'}:continue
            if kind=='Source' and (first in runtime_dirs or first in runtime_names or first=='release-manifest.json'):continue
            selected.append(p)
        with zipfile.ZipFile(path,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
            for p in selected:
                relative=p.relative_to(root)
                if kind=='Windows-x64' and p.name=='release-manifest.json':
                    scoped=dict(manifest);names={q.relative_to(root).as_posix() for q in selected}
                    scoped['Files']=[item for item in manifest['Files'] if item['Path'] in names]
                    archive.writestr('UmaWalking/release-manifest.json',json.dumps(scoped,indent=2))
                else:archive.write(p,Path('UmaWalking')/relative)
        with zipfile.ZipFile(path) as archive:
            assert archive.testzip() is None
            assert all(i.filename.isascii() for i in archive.infolist())
            if kind=='Windows-x64':assert 'UmaWalking/DesktopSettings.json' not in archive.namelist()
            if kind=='Source':assert not any('/UmaDesktop_Data/' in name or name.endswith('.exe') for name in archive.namelist())
            count=len(archive.infolist())
        sums.append(f'{sha(path)}  {name}')
        print(json.dumps({'Archive':name,'Bytes':path.stat().st_size,'Files':count,'CRCVerified':True}),flush=True)
    (dist/'SHA256SUMS.txt').write_text('\n'.join(sums)+'\n',encoding='ascii')

if __name__=='__main__':main()
