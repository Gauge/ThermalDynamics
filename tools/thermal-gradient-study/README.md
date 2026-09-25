# Thermal gradient study

**Offline studies of how the thermal-vision view should turn block temperatures into a picture.**
Each script is a lab, not a renderer: it reads temperature fields exported from the harness or
generated synthetically, draws block-bound proxies with Python, and writes images, HTML galleries or
JSON. None of it runs the game or measures the GPU. What the studies found, and the commands that
reproduce each figure, are in [thermal-vision-lab.md](../../docs/thermal-vision-lab.md).

| Script | What it studies |
| --- | --- |
| `study.py` | The temperature estimators on synthetic equal-volume blocks. |
| `corpus_render.py` | Real corpus solver snapshots rendered as block-bound proxies. |
| `mixed_render.py` | The shipped mixed-detail estimator on those proxies. |
| `soft_reference.py` | An appearance target: a normalised temperature blur over each ship's visible surfaces. |
| `continuous_surface.py` | World-space temperature interpolation on depth-tested block surfaces. |
| `optimize_surface.py` | Merging adjacent coplanar patches within a bounded temperature error. |
| `simplification_sweep.py` | An appearance ladder that relaxes the thermal error while keeping the geometry. |
| `distance_lod.py`, `distance_preview.py` | Projected-size level of detail, and a coarse geometry ladder built from corpus bounds; `distance_preview.html` is the gallery page it fills in. |
| `build_palette_textures.py` | Lookup textures generated from the mod's palette in `ThermalVisionPalette.cs`. |

The corpus inputs are written by the harness, `dotnet run --project tests/Thermodynamics.Sim --
thermal-corpus-export <dir>`, and every output directory belongs outside the mod folder. Run the
scripts from the repository root: several read the palette from
`Thermodynamics/Presentation/ThermalVision/ThermalVisionPalette.cs` by that relative path.

```bash
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tools/thermal-gradient-study -p 'test_*.py'
node tools/thermal-gradient-study/test_distance_preview.cjs <distance-lod-output>/index.html
```

The Python checks need no inputs — `test_continuous_surface.py`, `test_corpus_render.py`,
`test_distance_lod.py` and `test_optimize_surface.py`, one per script of the same name — and the
Node check, `test_distance_preview.cjs`, reads a gallery `distance_preview.py` wrote.

## Change log

| Date | Change |
| --- | --- |
| 2026-09-24 | Page written, which `EveryToolIsNamedByItsReadme` asked for. The palette path in eight scripts still named `Data/Scripts/…` from before the MDK2 move, so each would have failed to open its input; they read `Thermodynamics/Presentation/…` now, and the 21 Python checks pass. |
