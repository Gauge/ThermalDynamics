#!/usr/bin/env python3
"""Bounded, paired CPU comparison against the core at a frozen Git revision."""
import argparse
import io
import json
import hashlib
import os
from pathlib import Path
import platform
import subprocess
import tempfile
from xml.sax.saxutils import escape


def run(*args, **kwargs):
    return subprocess.check_output(args, text=True, **kwargs)


def extract_baseline(repo, baseline, destination_root):
    """Extract Core with one blob process, preserving the original receipt hash."""
    files = run("git", "ls-tree", "-r", "--name-only", baseline,
                "Thermodynamics/Core", cwd=repo).splitlines()
    files = [name for name in files if name.endswith(".cs")]
    requests = "".join(baseline + ":" + name + "\n" for name in files).encode()
    response = io.BytesIO(subprocess.check_output(
        ("git", "cat-file", "--batch"), input=requests, cwd=repo))
    baseline_hash = hashlib.sha256()
    for name in files:
        header = response.readline().split()
        if len(header) != 3 or header[1] != b"blob":
            raise ValueError("Cannot read frozen Core blob %s: %r" % (name, header))
        size = int(header[2])
        content = response.read(size)
        if len(content) != size or response.read(1) != b"\n":
            raise ValueError("Incomplete frozen Core blob: " + name)
        # Match the former check_output(text=True): platform text decoding and
        # universal newlines, followed by UTF-8 encoding for the receipt hash.
        with io.TextIOWrapper(io.BytesIO(content)) as source_stream:
            source = source_stream.read()
        baseline_hash.update(name.encode() + b"\0" + source.encode())
        destination = destination_root / Path(name).relative_to("Thermodynamics/Core")
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_text(source.replace("Thermodynamics.Core", "Thermodynamics.Before"))
    if response.read(1):
        raise ValueError("Unexpected data after frozen Core blobs")
    return baseline_hash.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline", required=True, help="Frozen pre-change Git commit")
    parser.add_argument("--game-bin", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--case", choices=("loop-rebuild", "node-preparation"), help="Run only this focused workload instead of the default step cases")
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[2]
    templates = Path(__file__).resolve().parent
    baseline = run("git", "rev-parse", "--verify", "--end-of-options",
                   args.baseline + "^{commit}", cwd=repo).strip()
    with tempfile.TemporaryDirectory(prefix="thermal-core-benchmark-") as temporary:
        root = Path(temporary)
        baseline_hash = extract_baseline(repo, baseline, root / "Before")
        template = (templates / "CoreRig.cs.txt").read_text()
        rebuild_template = (templates / "CoreRebuildRig.cs.txt").read_text()
        for label, namespace in (("Before", "Thermodynamics.Before"), ("After", "Thermodynamics.Core")):
            (root / (label + "Rig.cs")).write_text(template.replace("CLASS_NAME", label + "Rig")
                                                    .replace("CORE_NAMESPACE", namespace))
            (root / (label + "RebuildRig.cs")).write_text(rebuild_template.replace("CLASS_NAME", label + "RebuildRig")
                                                          .replace("CORE_NAMESPACE", namespace))
        program = (templates / "CoreBenchmark.cs.txt").read_text()
        (root / "Program.cs").write_text(program)
        (root / "Bench.csproj").write_text('''<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework>
<Optimize>true</Optimize><EnableDefaultCompileItems>false</EnableDefaultCompileItems>
<NuGetAudit>false</NuGetAudit></PropertyGroup>
<ItemGroup><Compile Include="Before/**/*.cs"/><Compile Include="*.cs"/>
<Compile Include="''' + escape(str(repo / "Thermodynamics/Core/**/*.cs"), {'"': '&quot;'}) + '''"/>
<Reference Include="VRage.Math"><HintPath>''' + escape(str(args.game_bin.resolve() / "VRage.Math.dll")) + '''</HintPath>
</Reference></ItemGroup></Project>''')
        build = run("dotnet", "build", str(root / "Bench.csproj"), "-c", "Release", "-m:1", "-nr:false", "-v:q")
        print(build, end="")
        environment = dict(os.environ, DOTNET_TieredCompilation="0")
        selected = [args.case] if args.case else []
        data = json.loads(run("dotnet", str(root / "bin/Release/net9.0/Bench.dll"), *selected, env=environment))
        data["baseline_commit"] = baseline
        data["baseline_core_sha256"] = baseline_hash
        current_hash = hashlib.sha256()
        for path in sorted((repo / "Thermodynamics/Core").rglob("*.cs")):
            current_hash.update(str(path.relative_to(repo)).encode() + b"\0" + path.read_bytes())
        data["current_core_sha256"] = current_hash.hexdigest()
        data["fixture_revision"] = "two-loop-rebuild-v1" if args.case == "loop-rebuild" else "active-coupling-v2"
        data["fixture_sha256"] = hashlib.sha256((template + rebuild_template + program).encode()).hexdigest()
        data["sdk"] = run("dotnet", "--version").strip()
        data["working_tree_head"] = run("git", "rev-parse", "HEAD", cwd=repo).strip()
        data["host"] = platform.platform()
        cpuinfo = Path("/proc/cpuinfo")
        if cpuinfo.exists():
            data["cpu"] = next((line.split(":", 1)[1].strip() for line in cpuinfo.read_text().splitlines()
                                if line.startswith("model name")), "unknown")
        data["scope"] = ("Two one-pipe loop replacements; includes SetLoops lookup/link rebuilding but excludes discovery, renderer, adapter and networking."
                         if args.case == "loop-rebuild" else
                         "Four ordinary-node preparation/full-step cases with flooring off/on; excludes coolant workloads, game renderer, adapter, networking and topology build."
                         if args.case == "node-preparation" else
                         "Synthetic CPU batches; excludes game renderer, adapter, networking and topology build.")
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(data, indent=2) + "\n")
        print("Wrote", args.output)
        for row in data["results"]:
            print("%s: median %.6f -> %.6f ms/batch, %.3fx; exact=%s" % (
                row["name"], row["old_median_ms"], row["new_median_ms"],
                row["new_median_ms"] / row["old_median_ms"], row["exact_state_match"]))


if __name__ == "__main__":
    main()
