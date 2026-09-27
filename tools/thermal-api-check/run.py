"""Run selected thermal-material API snippets through the installed game's analyzer.

Requires dotnet 9 and ilspycmd. Generates decompiled bootstrap/build output outside the mod.
Does not execute the snippets or alter a running game. See docs/thermal-vision-lab.md.
"""
import argparse
import hashlib
import pathlib
import re
import shutil
import struct
import subprocess
import tempfile
from xml.sax.saxutils import escape


# managed operation.
def managed(path):
    try:
        data = path.read_bytes()
        offset = struct.unpack_from("<I", data, 60)[0] + 24
        magic = struct.unpack_from("<H", data, offset)[0]
        directory = offset + (112 if magic == 523 else 96)
        return struct.unpack_from("<I", data, directory + 14 * 8)[0] != 0
    except (struct.error, IndexError):
        return False


# replace once operation.
def replace_once(text, old, new):
    if text.count(old) != 1:
        raise RuntimeError("Installed whitelist source changed; inspect bootstrap before adapting: " + old)
    return text.replace(old, new, 1)


def verify_aero_contract(source):
    """Fail if the inspected engine's force/torque units differ from the adapter's contract."""
    def body(name):
        match = re.search(r"private void " + name + r"\([^\n]+\)\s*\{(.*?)\n\t\}", source, re.S)
        if not match:
            raise RuntimeError("Missing physics implementation: " + name)
        return re.sub(r"\s+", "", match.group(1))

    force = body("ApplyForceWorld")
    angular = body("ApplyImplusesWorld")
    dispatch = body("AddForceInternal")
    if "rigidBody.ApplyForce(1f/60f,force.Value);" not in force:
        raise RuntimeError("World force no longer has the expected engine timestep")
    if "rigidBody.ApplyPointImpulse(force.Value,position.Value-Offset);" not in angular:
        raise RuntimeError("World linear impulse conversion changed")
    if "rigidBody.ApplyAngularImpulse(torque.Value*(1f/60f)*MyFakes.SIMULATION_SPEED);" not in angular:
        raise RuntimeError("World angular argument is no longer the expected torque")
    world_case = re.search(r"caseMyPhysicsForceType.APPLY_WORLD_FORCE:(.*?)break;", dispatch)
    if not world_case or "ApplyForceWorld(force,position,RigidBody);" not in world_case.group(1) or "torque" in world_case.group(1):
        raise RuntimeError("World-force dispatch changed; inspect torque handling")
    print("Aerodynamic force contract: expected force/torque units verified by static inspection; native execution not tested.", flush=True)


def profile_framework_bootstrap(source):
    """Preserve only the inspected game's explicit permissions needed by profile snapshots."""
    match = re.search(r"private static void AllowDefaultNamespaces\([^\n]+\).*?\n\t}", source, re.S)
    if not match:
        raise RuntimeError("Missing default framework whitelist")
    body = match.group(0)
    declarations = " ".join(re.findall(r"handle.AllowTypes\(MyWhitelistTarget.Both,.*?;", body, re.S))
    needed = ["object", "string", "bool", "byte", "int", "long", "float", "double", "Array", "Func<, >", "Func<, , , >"]
    for name in needed:
        if "typeof(" + name + ")" not in declarations:
            raise RuntimeError("Game no longer explicitly admits framework type: " + name)
    delegate = re.search(r'handle.AllowMembers\(MyWhitelistTarget.Both, \(from m in AllDeclaredMembers\(typeof\(Delegate\)\)\s+where m.Name != "CreateDelegate"\s+select m\).ToArray\(\)\);', body)
    if not delegate:
        raise RuntimeError("Game delegate permission changed; inspect before adapting")
    return ("handle.AllowTypes(MyWhitelistTarget.Both, " + ", ".join("typeof(" + t + ")" for t in needed)
            + ");\nhandle.AllowMembers(MyWhitelistTarget.Both, typeof(Delegate).GetMethod(\"GetInvocationList\"));")


# main operation.
def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("game", type=pathlib.Path, help="SpaceEngineers installation directory")
    parser.add_argument("--ilspy", default=shutil.which("ilspycmd") or str(pathlib.Path.home() / ".dotnet/tools/ilspycmd"))
    args = parser.parse_args()
    game_bin = (args.game / "Bin64").resolve(strict=True)
    work = pathlib.Path(tempfile.mkdtemp(prefix="thermal-api-check-"))
    print("Artifacts:", work, flush=True)
    for path in [game_bin / "Sandbox.Game.dll", game_bin / "VRage.Game.dll", game_bin / "SpaceEngineers.Game.dll",
                 pathlib.Path(__file__).resolve(), pathlib.Path(__file__).with_name("Program.cs").resolve()]:
        print("SHA256:", path.name, hashlib.sha256(path.read_bytes()).hexdigest(), flush=True)
    physics = subprocess.check_output([args.ilspy, "-t", "Sandbox.Engine.Physics.MyPhysicsBody",
                                       str(game_bin / "Sandbox.Game.dll")], text=True)
    (work / "PhysicsBody.cs.txt").write_text(physics)
    verify_aero_contract(physics)
    source = subprocess.check_output([args.ilspy, "-t", "SpaceEngineers.Game.MySpaceGameDefaultIlChecker",
                                      str(game_bin / "SpaceEngineers.Game.dll")], text=True)
    source = replace_once(source, "public class MySpaceGameDefaultIlChecker : MySandboxGame.IGameCustomIlChecker",
                          "public class GameWhitelistBootstrap")
    source = replace_once(source, "MyVRage.Platform.Scripting.OpenWhitelistBatch()",
                          "MyScriptCompiler.Static.Whitelist.OpenBatch()")
    source = replace_once(source, "AllowDefaultNamespaces(handle);",
                          profile_framework_bootstrap(source) + "\n"
                          "handle.AllowTypes(MyWhitelistTarget.Both, typeof(System.IO.BinaryReader), typeof(System.IO.Stream));\n"
                          "CompatibleNamespaces(handle, MyWhitelistTarget.Both, typeof(Dictionary<,>));")
    source = source.replace("typeof(Task)", "typeof(ParallelTasks.Task)")
    source = source.replace("handle.AllowNamespaceOfTypes(", "CompatibleNamespaces(handle, ")
    source = source.replace("handle.AllowMembers(", "CompatibleMembers(handle, ")
    helpers = '''
    private static readonly Dictionary<string, MyWhitelistTarget> namespaces = new Dictionary<string, MyWhitelistTarget>();
    private static void CompatibleNamespaces(IMyWhitelistBatch handle, MyWhitelistTarget target, params Type[] types)
    {
        foreach (Type type in types)
        {
            string key = type.Namespace + "," + type.Assembly.FullName;
            MyWhitelistTarget previous;
            if (namespaces.TryGetValue(key, out previous)) {
                if ((previous & target) != target) throw new InvalidOperationException("Unexpected target upgrade: " + key);
                continue;
            }
            namespaces.Add(key, target);
            handle.AllowNamespaceOfTypes(target, type);
        }
    }
    private static void CompatibleMembers(IMyWhitelistBatch handle, MyWhitelistTarget target, params MemberInfo[] members)
    {
        if (members.Any(x => x == null)) throw new InvalidOperationException("Missing game whitelist member; inspect runtime compatibility");
        handle.AllowMembers(target, members);
    }
'''
    source = replace_once(source, "\n\tpublic void InitIlChecker()", helpers + "\n\tpublic void InitIlChecker()")
    (work / "Bootstrap.cs").write_text(source)
    shutil.copyfile(pathlib.Path(__file__).with_name("Program.cs"), work / "Program.cs")
    references = []
    for dll in sorted(game_bin.glob("*.dll")):
        if dll.name.startswith(("System.", "Microsoft.")) and dll.stem not in ("Microsoft.CodeAnalysis", "Microsoft.CodeAnalysis.CSharp"):
            continue
        if "XmlSerializers" in dll.name or not managed(dll):
            continue
        references.append('<Reference Include="' + escape(dll.stem) + '"><HintPath>' + escape(str(dll)) + '</HintPath></Reference>')
    (work / "check.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
        '<TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><NuGetAudit>false</NuGetAudit>'
        '</PropertyGroup><ItemGroup>' + ''.join(references) + '</ItemGroup></Project>')
    with (work / "report.log").open("w") as report:
        result = subprocess.run(["dotnet", "run", "--project", str(work / "check.csproj"), "--verbosity", "quiet", "--", str(game_bin)],
                                stdout=report, stderr=subprocess.STDOUT)
    print((work / "report.log").read_text())
    return result.returncode


if __name__ == "__main__":
    raise SystemExit(main())
