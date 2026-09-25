"""Build opaque lookup textures from the mod's canonical palette (no authored artwork)."""
from pathlib import Path
import re
from PIL import Image
source=Path('Thermodynamics/Presentation/ThermalVision/ThermalVisionPalette.cs').read_text()
colours=[tuple(round(float(c)*255) for c in row)+(255,) for row in re.findall(r'new Vector3\(([0-9.]+)f, ([0-9.]+)f, ([0-9.]+)f\)',source)[:256]]
assert len(colours)==256
for name,pixels in [('Colour',colours),('Grey',[(round(18+225*i/255),)*3+(255,) for i in range(256)])]:
    image=Image.new('RGBA',(256,4));image.putdata(pixels*4)
    image.save(Path('Textures/Particles')/('GaugeThermalRamp'+name+'.dds'))
