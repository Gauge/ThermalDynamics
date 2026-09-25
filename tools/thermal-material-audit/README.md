# Model material audit

**`audit.py` lists the materials each installed block model carries, read straight from the `.mwm`
files.** It is read-only and executes no game code: the binary format is transcribed from the
game's `MyModelImporter`, `MyMeshPartInfo` and `MyMaterialDescriptor`, and it reads indexed MWM
versions 1066002 to 1157002 only.

```bash
python3 tools/thermal-material-audit/audit.py "$GAME/Content/Models/Cubes" /tmp/thermal-material-audit/cubes.json
```

`$GAME` is the Space Engineers install, and the output belongs outside the mod folder. It records
each model's material names, techniques, texture paths and geometry-asset references; it does not
resolve those references or translate glass aliases through the live material registry. A file with
no version header is rejected explicitly and the run exits 1. How the thermal-vision work used the
inventory is in [thermal-vision-lab.md](../../docs/thermal-vision-lab.md).

## Change log

| Date | Change |
| --- | --- |
| 2026-09-24 | Page written, which `EveryToolIsNamedByItsReadme` asked for. |
