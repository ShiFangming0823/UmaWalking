"""Read-only verification of a prepared UmaWalking publication folder."""
import argparse, hashlib, json, re
from pathlib import Path
from urllib.parse import unquote

def digest(path):
    h=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(1024*1024),b''):h.update(block)
    return h.hexdigest().upper()

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('root',type=Path)
    root=parser.parse_args().root.resolve()
    manifest=json.loads((root/'release-manifest.json').read_text(encoding='utf-8'))
    records=manifest['Files']
    for entry in records:
        p=(root/entry['Path']).resolve()
        assert p.is_relative_to(root),entry['Path']
        assert p.is_file() and p.stat().st_size==entry['Bytes'],entry['Path']
        assert digest(p)==entry['SHA256'],entry['Path']
    required=['UmaDesktop.exe','UmaDesktopController.exe','UnityPlayer.dll','UnityCrashHandler64.exe',
              'UmaDesktop_Data/Managed/umamusume.dll','MonoBleedingEdge','README.md','AGENTS.md',
              'Launch-UmaWalking.cmd','Launch-Low-Power.cmd','licenses/NOTICE.txt']
    for name in required:assert (root/name).exists(),name
    settings=json.loads((root/'DesktopSettings.json').read_text(encoding='utf-8-sig'))
    assert settings['DataPath']==r'C:\DMMGames\Umamusume\umamusume_Data\Persistent'
    assert settings['MaxActors']==6 and settings['FrameRate']==30
    assert settings['Avoid3DCollisions'] is False
    assert settings['IncludeIds']==[] and settings['ExcludeIds']==[]
    paths=[p for p in root.rglob('*') if 'dist' not in p.relative_to(root).parts]
    for p in paths:
        assert p.name.isascii(),str(p.relative_to(root))
        assert p.name.lower() not in {'master.mdb','mini_catalog_audit.json','full-costume-catalog.json'},p
        assert not any(part in {'DesktopLogs','DesktopCommands','DesktopIPC','OriginalSourceBackup','__pycache__','dat'} for part in p.relative_to(root).parts),p
        if p.suffix.lower() in {'.md','.cs','.ps1','.py','.json','.cmd','.yml'}:
            text=p.read_text(encoding='utf-8-sig')
            assert not re.search(r'(?:D:[/\\](?:iAParameter|Uma|Codex)|C:[/\\]Users[/\\]iAPara)',text,re.I),p
        if p.suffix.lower()=='.md':
            for target in re.findall(r'!?\[[^\]]*\]\(([^)]+)\)',text)+re.findall(r'<img[^>]+src="([^"]+)"',text):
                if re.match(r'[a-z]+://|#|mailto:',target,re.I):continue
                target=unquote(target.split('#',1)[0].split(' "',1)[0].strip('<>'))
                assert (p.parent/target).exists(),f'{p.relative_to(root)} -> {target}'
    assert sorted(p.name for p in root.glob('*.md'))==['AGENTS.md','README.md']
    print(json.dumps({'Passed':True,'ManifestFiles':len(records),'MarkdownFiles':sum(p.suffix=='.md' for p in paths),
                      'GameDataIncluded':False,'PermissionConfirmed':False},indent=2))

if __name__=='__main__':main()
