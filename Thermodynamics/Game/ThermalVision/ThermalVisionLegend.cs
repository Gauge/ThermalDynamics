using Thermodynamics.Presentation;
using System.Collections.Generic;
using Sandbox.ModAPI;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using RichHudFramework.UI.Rendering;
using Thermodynamics.Core;
using VRageMath;
using HudMaterial = RichHudFramework.UI.Rendering.Material;

namespace Thermodynamics
{
    public static partial class ThermalVisionProbe
    {
        private static TexturedBox visionLegend, visionLegendRamp;
        private static Label visionLegendTitle;
        private static readonly Label[] visionLegendTicks = new Label[5];
        private static string legendTextKey;
        private static bool legendInputsKnown,legendAutomaticRange;
        private static ThermalVisionState.Mode legendPalette;
        private static float legendLow,legendHigh;
        private static int legendFrames;
        private static readonly List<ILegendElement> LegendElements = new List<ILegendElement>();

        private static readonly MatrixD[] LegendPlane = new MatrixD[1];
        private interface ILegendElement { bool Submit(Vector2 centre); }
        private sealed class LegendBox : TexturedBox, ILegendElement
        {
            public LegendBox(HudParentBase parent) : base(parent) { LegendElements.Add(this); }
            protected override void Draw() { }
            public bool Submit(Vector2 centre)
            {
                if (!Visible || !Registered) return false;
                Vector2 position=centre+(Parent==visionLegend?Offset:Vector2.Zero);
                var box=new CroppedBox { bounds=new BoundingBox2(position-Size*.5f,position+Size*.5f) };
                hudBoard.Draw(ref box,LegendPlane);
                return true;
            }
        }
        private sealed class LegendLabel : Label, ILegendElement
        {
            public LegendLabel(HudParentBase parent) : base(parent) { LegendElements.Add(this); }
            protected override void Draw() { }
            public bool Submit(Vector2 centre)
            {
                if (!Visible || !Registered) return false;
                Vector2 position=centre+Offset, half=TextBoard.TextSize*.5f;
                TextBoard.Draw(new BoundingBox2(position-half,position+half),CroppedBox.defaultMask,LegendPlane);
                return true;
            }
        }
        private static readonly HudMaterial LegendGrey = new HudMaterial("GaugeThermalLegendGrey",new Vector2(256,4));
        private static readonly HudMaterial LegendColour = new HudMaterial("GaugeThermalLegendColour",new Vector2(256,4));

        private static void BuildVisionLegend()
        {
            LegendElements.Clear();
            legendFrames=0;
            visionLegend=new LegendBox(HudMain.HighDpiRoot) {
                ParentAlignment=ParentAlignments.Top|ParentAlignments.Right|ParentAlignments.InnerV|ParentAlignments.InnerH,
                Offset=new Vector2(-24,-38), Size=new Vector2(420,112), Color=new Color(16,25,34,235),
                Visible=false, UseCursor=false
            };
            new LegendBox(visionLegend) { Size=new Vector2(420,2), Offset=new Vector2(0,55), Color=new Color(127,190,207), UseCursor=false };
            visionLegendTitle=new LegendLabel(visionLegend) { Offset=new Vector2(0,34), UseCursor=false,
                Format=new GlyphFormat(new Color(207,231,238),TextAlignment.Center,.72f) };
            new LegendBox(visionLegend) { Size=new Vector2(362,18), Offset=new Vector2(0,8), Color=new Color(108,145,157), UseCursor=false };
            visionLegendRamp=new LegendBox(visionLegend) { Size=new Vector2(360,16), Offset=new Vector2(0,8),
                Color=Color.White, MatAlignment=MaterialAlignment.StretchToFit, UseCursor=false, Material=LegendGrey };
            for(int i=0;i<5;i++)
            {
                float x=-180+90*i;
                new LegendBox(visionLegend) { Size=new Vector2(1,5), Offset=new Vector2(x,-4), Color=new Color(180,211,221), UseCursor=false };
                visionLegendTicks[i]=new LegendLabel(visionLegend) { Offset=new Vector2(x,-20), UseCursor=false,
                    Format=new GlyphFormat(new Color(222,235,240),TextAlignment.Center,.62f) };
            }
            new LegendLabel(visionLegend) { Offset=new Vector2(0,-42), UseCursor=false,
                Format=new GlyphFormat(new Color(159,189,198),TextAlignment.Center,.58f),
                Text=new RichText("COOLER                                      HOTTER") };
            legendTextKey=null;
            legendInputsKnown=false;
        }
        private static void UpdateVisionLegend()
        {
            if(visionLegend==null) BuildVisionLegend();
            visionLegend.Visible=State.Current!=ThermalVisionState.Mode.Off && !depthMode;
            if(!visionLegend.Visible) return;
            if(!legendInputsKnown || legendPalette!=State.Current || legendAutomaticRange!=automaticRange
                || legendLow!=lowKelvin || legendHigh!=highKelvin)
            {
                legendInputsKnown=true; legendPalette=State.Current; legendAutomaticRange=automaticRange;
                legendLow=lowKelvin; legendHigh=highKelvin;
                // Preserve the existing rounded-label refresh boundary, while
                // stable ranges need no enum boxing, formatting or string keys.
                string key=State.Current+"/"+automaticRange+"/"+lowKelvin.ToString("F1")+"/"+highKelvin.ToString("F1");
                if(key!=legendTextKey)
                {
                    legendTextKey=key;
                    visionLegendRamp.Material=State.Current==ThermalVisionState.Mode.Cividis?LegendColour:LegendGrey;
                    visionLegendTitle.Text=new RichText("THERMAL / "+(State.Current==ThermalVisionState.Mode.Cividis?"COLOUR":"WHITE HOT")
                        +" / "+(automaticRange?"AUTO RANGE":"LOCKED RANGE"));
                    for(int i=0;i<5;i++)
                    {
                        float temperature=lowKelvin+(highKelvin-lowKelvin)*(i/4f)-273.15f;
                        visionLegendTicks[i].Text=new RichText(temperature.ToString("0")+" C");
                    }
                }
            }
            var camera=MyAPIGateway.Session.Camera;
            float dpi=HudMain.ResScale>0?HudMain.ResScale:1;
            Vector2 screen=camera.ViewportSize/dpi;
            Vector2 centre=screen*.5f-visionLegend.Size*.5f+visionLegend.Offset;
            LegendPlane[0]=ThermalVisionHudProjection.Create(camera.WorldMatrix,camera.ProjectionMatrix,
                camera.ViewportSize,camera.NearPlaneDistance*1.01,dpi);
            int submitted=0;
            for(int i=0;i<LegendElements.Count;i++) if(LegendElements[i].Submit(centre)) submitted++;
            if(Telemetry.Enabled && legendFrames++%120==0)
                RecordEvent("vision legend: submitted="+submitted+"/"+LegendElements.Count
                    +" registered="+visionLegend.Registered+" visible="+visionLegend.Visible
                    +" parent-visible="+visionLegend.Parent.Visible+" position="+centre
                    +" size="+visionLegend.Size+" screen="+screen
                    +" range-K="+lowKelvin.ToString("F1")+".."+highKelvin.ToString("F1")
                    +" palette="+State.Current+" scene-billboards="+drawn+" order=after-thermal camera=current",false);
        }
        private static void HideVisionLegend() { if(visionLegend!=null) visionLegend.Visible=false; }
    }
}
