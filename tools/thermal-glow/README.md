# Heat glow texture

**`build_texture.py` generates the one texture the overheating glow draws, and `test_texture.py`
checks the shipped file against it.** The texture is
`Thermodynamics/Content/Textures/Particles/GaugeHeatGlow.dds`: a 128×128 premultiplied radial
falloff with a full mip chain, computed from a formula with the Python standard library alone. No
artwork goes in. What the glow is and why it looks the way it does is
[thermal-glow.md](../../docs/thermal-glow.md).

```bash
PYTHONDONTWRITEBYTECODE=1 python3 tools/thermal-glow/build_texture.py                      # rewrite the texture
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tools/thermal-glow -p 'test_*.py'  # check it
```

The checks read the shipped bytes rather than the formula: that the file regenerates byte for
byte, that every mip has a zero border, that the falloff is radially symmetric and monotone, that
the colour channels carry the falloff and not only alpha, and that the transparent material in
`TransparentMaterials.sbc` names this texture and is depth-tested and unlit. Rewriting the texture without
re-running them is how a shipped asset drifts from the code that claims to make it.

## Change log

| Date | Change |
| --- | --- |
| 2026-09-24 | Page written. The tool had no README, which `EveryToolIsNamedByItsReadme` reports. The five checks pass against the shipped texture. |
