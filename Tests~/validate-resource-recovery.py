#!/usr/bin/env python3
"""Compile actual sources against sandbox references; execute managed tests. No Unity lifecycle claim."""
from pathlib import Path
import json, subprocess, tempfile, xml.etree.ElementTree as ET
root = Path(__file__).resolve().parents[3]
economy = root / 'LocalPackages/com.dreamy.feature.economy'
with tempfile.TemporaryDirectory(prefix='dreamy-recovery-') as temp:
    temp = Path(temp)
    refs = {p.stem: p for p in (root/'Library/ScriptAssemblies').glob('*.dll') if '.Editor' not in p.stem and '.Tests' not in p.stem}
    for generated in root.glob('*.csproj'):
        for ref in ET.parse(generated).findall('.//Reference'):
            hint = ref.find('HintPath')
            if hint is not None:
                path = Path(hint.text)
                if not path.is_absolute(): path = root/path
                if path.exists(): refs.setdefault(ref.attrib['Include'], path)
    def build(name, sources, dependencies, executable=False):
        directory = temp/name
        directory.mkdir()
        project = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
        props = ET.SubElement(project, 'PropertyGroup')
        for k,v in dict(TargetFramework='net10.0', OutputType='Exe' if executable else 'Library', AssemblyName=name,
                        EnableDefaultCompileItems='false', StartupObject='RecoveryHarness' if executable else '', NoWarn='0067;0649').items(): ET.SubElement(props,k).text=v
        group = ET.SubElement(project,'ItemGroup')
        for source in sources: ET.SubElement(group,'Compile',Include=str(source))
        for dep in dependencies:
            ref=ET.SubElement(group,'Reference',Include=dep)
            ET.SubElement(ref,'HintPath').text=str(refs[dep])
        path=directory/'Harness.csproj';ET.ElementTree(project).write(path)
        subprocess.run(['dotnet','build',str(path),'--verbosity','quiet'],check=True)
        refs[name]=directory/'bin/Debug/net10.0'/f'{name}.dll'
        if executable: subprocess.run(['dotnet',str(refs[name])],check=True)
    def assembly(folder):
        definition=json.loads(next(folder.glob('*.asmdef')).read_text())
        sources=[p for p in folder.rglob('*.cs') if not any(q != folder and list(q.glob('*.asmdef')) for q in p.parents if folder in q.parents)]
        deps=list(definition.get('references',[])) + list(definition.get('precompiledReferences',[]))
        deps=[x.removesuffix('.dll') for x in deps]
        if not definition.get('overrideReferences'): deps += ['Newtonsoft.Json']
        if not definition.get('noEngineReferences'): deps += [name for name in refs if name.startswith('UnityEngine.') or name=='UnityEngine']
        build(definition['name'],sources,deps)
    assembly(economy/'Runtime')
    assembly(economy/'Integration/UI')
    shop=root/'LocalPackages/com.dreamy.feature.shop'
    assembly(shop/'Runtime')
    if (shop/'Integration/UI').exists(): assembly(shop/'Integration/UI')
    assembly(shop/'Samples~/Shop Feature')
    build('RecoveryTests',[p for p in (economy/'Tests/Runtime').glob('*.cs') if p.name != 'FileBackedWalletTests.cs'] + [economy/'Tests/Editor/ResourceRecoveryPresentationRouterTests.cs', economy/'Tests/Editor/ResourceSuggestionPresenterTests.cs'] + list((economy/'Tests~').glob('*.cs')) + list((shop/'Tests/Recovery').glob('*.cs')) + list((shop/'Tests/Runtime').glob('*.cs')) + list((shop/'Tests~').glob('*.cs')),
          ['Dreamy.Economy.Runtime','Dreamy.Datasave.Runtime','Dreamy.Economy.UI.Integration','Dreamy.Shop.Runtime',
           'Dreamy.DataConfig.Runtime','Dreamy.UI.Presentation','Dreamy.Feature.Shop.Integration.Runtime','UnityEngine.CoreModule','UniTask','nunit.framework','Newtonsoft.Json'] + (['Dreamy.Shop.UI.Integration'] if (shop/'Integration/UI').exists() else []),True)
print('PASS managed transaction, migration, recovery and navigation checks. Unity asset/device gates are separate.')
