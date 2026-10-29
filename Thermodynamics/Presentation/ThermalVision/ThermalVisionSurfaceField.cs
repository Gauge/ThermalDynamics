using System;
using System.Collections.Generic;
using VRageMath;
using Region = Thermodynamics.Presentation.ThermalVisionRegionPartition.Region;

namespace Thermodynamics.Presentation
{
    public sealed class ThermalVisionSurfaceField
    {
        public readonly Region Bounds;
        public readonly Vector3I Steps;
        public readonly float[] Values;
        public int PairedFaceMask;

        public ThermalVisionSurfaceField(Region bounds, double spacing)
        {
            Bounds=bounds;
            Vector3D size=bounds.Max-bounds.Min;

            Steps=new Vector3I(Step(size.X,spacing),Step(size.Y,spacing),Step(size.Z,spacing));
            Values=new float[(Steps.X+1)*(Steps.Y+1)*(Steps.Z+1)];
        }

        private static int Step(double size,double spacing) { return Math.Max(1,Math.Min(4,(int)Math.Ceiling(size/Math.Max(.001,spacing)))); }

        public int Index(int x,int y,int z) { return (z*(Steps.Y+1)+y)*(Steps.X+1)+x; }

        public Vector3D Point(int x,int y,int z)
        { return Bounds.Min+(Bounds.Max-Bounds.Min)*new Vector3D((double)x/Steps.X,(double)y/Steps.Y,(double)z/Steps.Z); }
        public struct Patch
        {
            public Vector3D A, B, C, D;
            public Vector4 Temperatures, PreviousTemperatures;
            public bool AlternateDiagonal;
        }
        public int SavedPatches { get; private set; }
        public float Minimum { get; private set; }
        public float Maximum { get; private set; }
        private List<Patch>[] faces;

        public IList<Patch> Face(int axis,bool upper)
        {
            if(faces==null) faces=new List<Patch>[6];
            return faces[axis*2+(upper?1:0)];
        }
        private ThermalVisionSurfaceField transitionReference;

        /// <summary>Sequential face-planning scratch for one refresh job; never retained by a published field.</summary>
        public sealed class BuildScratch
        {
            private byte[] costs,cuts;

            internal void Prepare(out byte[] faceCosts,out byte[] faceCuts)
            {
                if(costs==null) { costs=new byte[625]; cuts=new byte[625]; }
                else Array.Clear(costs,0,costs.Length);
                faceCosts=costs; faceCuts=cuts;
            }
        }

        public void BuildFaces(float errorKelvin, bool minimizePatches = false, ThermalVisionSurfaceField previous = null, float leafErrorKelvin = 0)
        {
            BuildFaces(errorKelvin,minimizePatches,previous,leafErrorKelvin,null);
        }

        public void BuildFaces(float errorKelvin, bool minimizePatches, ThermalVisionSurfaceField previous, float leafErrorKelvin,
            BuildScratch scratch)
        {
            if(previous!=null && (previous.Steps!=Steps || previous.Bounds.Min!=Bounds.Min || previous.Bounds.Max!=Bounds.Max))
                throw new ArgumentException("Transition lattice must match surface bounds and subdivisions");
            if(faces==null) faces=new List<Patch>[6];
            transitionReference=previous;
            SavedPatches=0;
            Minimum=float.PositiveInfinity; Maximum=float.NegativeInfinity;
            foreach(float value in Values) { Minimum=Math.Min(Minimum,value); Maximum=Math.Max(Maximum,value); }
            // Uniform current and prior lattices cannot need a split. The normal path
            // rechecks every face vertex and cell centre (including the prior lattice).
            // Keep strict/special tolerances on that path to preserve its rounding rules.
            bool flat=errorKelvin>=.001f && Minimum==Maximum && Minimum>=0 && Minimum<=100000
                && (previous==null || previous.IsUniform());
            byte[] costs=null, cuts=null;
            for(int axis=0;axis<3;axis++) for(int side=0;side<2;side++)
            {

                var list=new List<Patch>(flat?1:0); faces[axis*2+side]=list;
                int nu=axis==0?Steps.Y:Steps.X, nv=axis==2?Steps.Y:Steps.Z;
                if(flat)
                {
                    var t=new Vector4(Minimum);
                    list.Add(new Patch { A=FacePoint(axis,side==1,0,0), B=FacePoint(axis,side==1,nu,0),
                        C=FacePoint(axis,side==1,nu,nv), D=FacePoint(axis,side==1,0,nv),
                        Temperatures=t, PreviousTemperatures=t });
                }
                else SplitFace(axis,side==1,0,nu,0,nv,errorKelvin,list);
                if(minimizePatches && list.Count>1 && list.Count*4<nu*nv*3)
                {
                    if(costs==null)
                    {
                        if(scratch==null) { costs=new byte[625]; cuts=new byte[625]; }
                        else scratch.Prepare(out costs,out cuts);
                    }
                    else Array.Clear(costs,0,costs.Length);
                    int count=PlanFace(axis,side==1,0,nu,0,nv,errorKelvin,costs,cuts);
                    if(count<list.Count)
                    { SavedPatches+=list.Count-count; list.Clear(); EmitFace(axis,side==1,0,nu,0,nv,cuts,list); }
                }
            }
            if(!flat && leafErrorKelvin>0) RefineLeafSaddles(leafErrorKelvin);
            transitionReference=null;
        }

        private bool IsUniform()
        {
            float value=Values[0];
            if(float.IsNaN(value) || value<0 || value>100000) return false;
            for(int i=1;i<Values.Length;i++) if(Values[i]!=value) return false;
            return true;
        }

        // Bounded-frame fallback: preserve the field's corner samples, but
        // approximate between them with one complete two-triangle face.
        public Patch WholeFace(int axis,bool upper,ThermalVisionSurfaceField previous)
        {
            int nu=axis==0?Steps.Y:Steps.X, nv=axis==2?Steps.Y:Steps.Z;
            var patch=new Patch { A=FacePoint(axis,upper,0,0), B=FacePoint(axis,upper,nu,0),
                C=FacePoint(axis,upper,nu,nv), D=FacePoint(axis,upper,0,nv),
                Temperatures=new Vector4(FaceValue(axis,upper,0,0),FaceValue(axis,upper,nu,0),
                    FaceValue(axis,upper,nu,nv),FaceValue(axis,upper,0,nv)) };
            patch.PreviousTemperatures=previous==null?patch.Temperatures:new Vector4(
                previous.Sample(patch.A),previous.Sample(patch.B),previous.Sample(patch.C),previous.Sample(patch.D));
            return patch;
        }

        private void RefineLeafSaddles(float error)
        {
            Vector3D step=(Bounds.Max-Bounds.Min)/new Vector3D(Steps.X,Steps.Y,Steps.Z);
            foreach(var face in faces)
            {
                int originalCount=face.Count;
                for(int i=0;i<originalCount;i++)
                {
                    var patch=face[i];var size=Vector3D.Abs(patch.C-patch.A);
                    if(size.X>step.X+1e-7 || size.Y>step.Y+1e-7 || size.Z>step.Z+1e-7) continue;

                    Vector4 old=transitionReference==null?patch.Temperatures:new Vector4(
                        transitionReference.Sample(patch.A),transitionReference.Sample(patch.B),
                        transitionReference.Sample(patch.C),transitionReference.Sample(patch.D));
                    double bend=Math.Max(SaddleError(patch.Temperatures),SaddleError(old));
                    int n=Math.Min(8,(int)Math.Ceiling(Math.Sqrt(bend/error)));
                    if(n<=1) continue;
                    for(int y=0;y<n;y++) for(int x=0;x<n;x++)
                    {
                        Vector3D a=patch.A+(patch.B-patch.A)*((double)x/n)+(patch.D-patch.A)*((double)y/n);
                        Vector3D u=(patch.B-patch.A)/n,v=(patch.D-patch.A)/n;
                        var refined=new Patch { A=a,B=a+u,C=a+u+v,D=a+v,

                            Temperatures=new Vector4(Sample(a),Sample(a+u),Sample(a+u+v),Sample(a+v)) };
                        refined.PreviousTemperatures=refined.Temperatures;
                        if(x==0 && y==0)face[i]=refined;else face.Add(refined);
                    }
                }
            }
        }

        private static double SaddleError(Vector4 t)
        { return Math.Abs((double)t.X-t.Y+t.Z-t.W)*.25; }

        private Vector3D FacePoint(int axis,bool upper,int u,int v)
        {
            if(axis==0) return Point(upper?Steps.X:0,u,v);
            if(axis==1) return Point(u,upper?Steps.Y:0,v);

            return Point(u,v,upper?Steps.Z:0);
        }

        private float FaceValue(int axis,bool upper,int u,int v)
        {
            if(axis==0) return Values[Index(upper?Steps.X:0,u,v)];
            if(axis==1) return Values[Index(u,upper?Steps.Y:0,v)];
            return Values[Index(u,v,upper?Steps.Z:0)];
        }

        private bool FaceNeedsSplit(int axis,bool upper,int u0,int u1,int v0,int v1,float error,bool alternate=false)
        {

            return FaceValuesNeedSplit(axis,upper,u0,u1,v0,v1,error,alternate)
                || (transitionReference!=null && transitionReference.FaceValuesNeedSplit(axis,upper,u0,u1,v0,v1,error,alternate));
        }

        private bool FaceValuesNeedSplit(int axis,bool upper,int u0,int u1,int v0,int v1,float error,bool alternate)
        {
            float ta=FaceValue(axis,upper,u0,v0), tb=FaceValue(axis,upper,u1,v0),
                tc=FaceValue(axis,upper,u1,v1), td=FaceValue(axis,upper,u0,v1);
            bool split=false;
            for(int v=v0;v<=v1 && !split;v++) for(int u=u0;u<=u1 && !split;u++)
            {
                double x=(double)(u-u0)/(u1-u0), y=(double)(v-v0)/(v1-v0);
                double estimate=alternate
                    ? (x+y<=1 ? ta*(1-x-y)+tb*x+td*y : tb*(1-y)+tc*(x+y-1)+td*(1-x))
                    : (x>=y ? ta*(1-x)+tb*(x-y)+tc*y : ta*(1-y)+tc*x+td*(y-x));
                if(Math.Abs(FaceValue(axis,upper,u,v)-estimate)>error) split=true;
            }
            for(int v=v0;v<v1 && !split;v++) for(int u=u0;u<u1 && !split;u++)
            {
                double x=(u+.5-u0)/(u1-u0), y=(v+.5-v0)/(v1-v0);
                double estimate=alternate
                    ? (x+y<=1 ? ta*(1-x-y)+tb*x+td*y : tb*(1-y)+tc*(x+y-1)+td*(1-x))
                    : (x>=y ? ta*(1-x)+tb*(x-y)+tc*y : ta*(1-y)+tc*x+td*(y-x));
                double midpoint=((double)FaceValue(axis,upper,u,v)+FaceValue(axis,upper,u+1,v)
                    +FaceValue(axis,upper,u,v+1)+FaceValue(axis,upper,u+1,v+1))*.25;
                if(Math.Abs(midpoint-estimate)>error) split=true;
            }
            return split;
        }

        private static int RectangleKey(int u0,int u1,int v0,int v1)
        { return ((u0*5+u1)*5+v0)*5+v1; }

        private int PlanFace(int axis,bool upper,int u0,int u1,int v0,int v1,float error,byte[] costs,byte[] cuts)
        {
            int key=RectangleKey(u0,u1,v0,v1);
            if(costs[key]!=0)return costs[key];
            cuts[key]=0;
            if((u1-u0==1 && v1-v0==1) || !FaceNeedsSplit(axis,upper,u0,u1,v0,v1,error))
            { costs[key]=1; return 1; }
            if(!FaceNeedsSplit(axis,upper,u0,u1,v0,v1,error,true))
            { costs[key]=1;cuts[key]=16;return 1; }
            int best=99,cut=0;
            for(int u=u0+1;u<u1;u++)
            {
                int count=PlanFace(axis,upper,u0,u,v0,v1,error,costs,cuts)+PlanFace(axis,upper,u,u1,v0,v1,error,costs,cuts);
                if(count<best){best=count;cut=u;}
                if(best==2){costs[key]=2;cuts[key]=(byte)cut;return 2;}
            }
            for(int v=v0+1;v<v1;v++)
            {
                int count=PlanFace(axis,upper,u0,u1,v0,v,error,costs,cuts)+PlanFace(axis,upper,u0,u1,v,v1,error,costs,cuts);
                if(count<best){best=count;cut=8+v;}
                if(best==2){costs[key]=2;cuts[key]=(byte)cut;return 2;}
            }
            costs[key]=(byte)best; cuts[key]=(byte)cut; return best;
        }

        private void EmitFace(int axis,bool upper,int u0,int u1,int v0,int v1,byte[] cuts,List<Patch> list)
        {
            int cut=cuts[RectangleKey(u0,u1,v0,v1)];
            if(cut==0 || cut==16)
            {

                Vector4 t=new Vector4(FaceValue(axis,upper,u0,v0),FaceValue(axis,upper,u1,v0),FaceValue(axis,upper,u1,v1),FaceValue(axis,upper,u0,v1));
                list.Add(new Patch { A=FacePoint(axis,upper,u0,v0),B=FacePoint(axis,upper,u1,v0),C=FacePoint(axis,upper,u1,v1),D=FacePoint(axis,upper,u0,v1),Temperatures=t,PreviousTemperatures=t,AlternateDiagonal=cut==16 });
            }

            else if(cut<8)
            { EmitFace(axis,upper,u0,cut,v0,v1,cuts,list); EmitFace(axis,upper,cut,u1,v0,v1,cuts,list); }
            else

            { cut-=8; EmitFace(axis,upper,u0,u1,v0,cut,cuts,list); EmitFace(axis,upper,u0,u1,cut,v1,cuts,list); }
        }

        private void SplitFace(int axis,bool upper,int u0,int u1,int v0,int v1,float error,List<Patch> list)
        {
            float ta=FaceValue(axis,upper,u0,v0), tb=FaceValue(axis,upper,u1,v0),
                tc=FaceValue(axis,upper,u1,v1), td=FaceValue(axis,upper,u0,v1);
            bool split=FaceNeedsSplit(axis,upper,u0,u1,v0,v1,error);
            if(split && (u1-u0>1 || v1-v0>1))
            {
                if(u1-u0>=v1-v0 && u1-u0>1)
                { int m=(u0+u1)/2; SplitFace(axis,upper,u0,m,v0,v1,error,list); SplitFace(axis,upper,m,u1,v0,v1,error,list); }
                else
                { int m=(v0+v1)/2; SplitFace(axis,upper,u0,u1,v0,m,error,list); SplitFace(axis,upper,u0,u1,m,v1,error,list); }
            }
            else
            {
                list.Add(new Patch { A=FacePoint(axis,upper,u0,v0), B=FacePoint(axis,upper,u1,v0),
                    C=FacePoint(axis,upper,u1,v1), D=FacePoint(axis,upper,u0,v1),

                    Temperatures=new Vector4(ta,tb,tc,td), PreviousTemperatures=new Vector4(ta,tb,tc,td) });
            }
        }
        public struct TemperatureTriangle
        {
            public Vector3D A,B,C;
            public Vector3 Temperatures;
        }

        public void AppendCapTriangles(Vector3D a,Vector3D b,Vector3D c,
            ThermalVisionSurfaceField previous,float blend,float error,List<TemperatureTriangle> output)
        {
            AppendCap(a,b,c,BlendSample(previous,a,blend),BlendSample(previous,b,blend),
                BlendSample(previous,c,blend),previous,blend,error,0,output);
        }

        private void AppendCap(Vector3D a,Vector3D b,Vector3D c,float ta,float tb,float tc,
            ThermalVisionSurfaceField previous,float blend,float error,int depth,List<TemperatureTriangle> output)
        {
            if(depth<3)
            {
                var ab=(a+b)*.5;var bc=(b+c)*.5;var ca=(c+a)*.5;
                float tab=BlendSample(previous,ab,blend),tbc=BlendSample(previous,bc,blend),tca=BlendSample(previous,ca,blend);
                if(Math.Abs(tab-(ta+tb)*.5f)>error || Math.Abs(tbc-(tb+tc)*.5f)>error
                    || Math.Abs(tca-(tc+ta)*.5f)>error || Math.Abs(BlendSample(previous,(a+b+c)/3,blend)-(ta+tb+tc)/3)>error)
                {
                    AppendCap(a,ab,ca,ta,tab,tca,previous,blend,error,depth+1,output);
                    AppendCap(ab,b,bc,tab,tb,tbc,previous,blend,error,depth+1,output);
                    AppendCap(ca,bc,c,tca,tbc,tc,previous,blend,error,depth+1,output);
                    AppendCap(ab,bc,ca,tab,tbc,tca,previous,blend,error,depth+1,output);
                    return;
                }
            }
            output.Add(new TemperatureTriangle { A=a,B=b,C=c,Temperatures=new Vector3(ta,tb,tc) });
        }

        public void PrepareTransition(ThermalVisionSurfaceField previous)
        {
            foreach(var face in faces)
                for(int i=0;i<face.Count;i++)
                {
                    var patch=face[i];

                    patch.PreviousTemperatures=new Vector4(previous.Sample(patch.A),previous.Sample(patch.B),previous.Sample(patch.C),previous.Sample(patch.D));
                    face[i]=patch;
                }
        }

        public static bool HasUniformEndpoints(Patch patch)
        { return Uniform(patch.Temperatures) && Uniform(patch.PreviousTemperatures); }

        private static bool Uniform(Vector4 t)
        { return !float.IsNaN(t.X) && !float.IsInfinity(t.X) && t.X==t.Y && t.X==t.Z && t.X==t.W; }


        public static Vector4 PatchTemperatures(Patch patch,float blend)
        {
            if(blend>=1) return patch.Temperatures;
            blend=Math.Max(0f,blend); float weight=blend*blend*(3-2*blend);
            return patch.PreviousTemperatures+(patch.Temperatures-patch.PreviousTemperatures)*weight;
        }

        public float BlendSample(ThermalVisionSurfaceField previous,Vector3D point,float blend)
        {
            float current=Sample(point);
            if(previous==null) return current;
            blend=Math.Max(0f,Math.Min(1f,blend));
            return MathHelper.Lerp(previous.Sample(point),current,blend*blend*(3-2*blend));
        }

        public float Sample(Vector3D point)
        {
            Vector3D t=Vector3D.Clamp((point-Bounds.Min)/(Bounds.Max-Bounds.Min),Vector3D.Zero,Vector3D.One)*new Vector3D(Steps.X,Steps.Y,Steps.Z);
            int x=Math.Min(Steps.X-1,(int)t.X), y=Math.Min(Steps.Y-1,(int)t.Y), z=Math.Min(Steps.Z-1,(int)t.Z);

            t-=new Vector3D(x,y,z);
            int row=Steps.X+1, layer=row*(Steps.Y+1), index=Index(x,y,z);
            double ix=1-t.X, iy=1-t.Y, iz=1-t.Z;
            double value=0;
            value+=Values[index]*ix*iy*iz;
            value+=Values[index+1]*t.X*iy*iz;
            value+=Values[index+row]*ix*t.Y*iz;
            value+=Values[index+row+1]*t.X*t.Y*iz;
            value+=Values[index+layer]*ix*iy*t.Z;
            value+=Values[index+layer+1]*t.X*iy*t.Z;
            value+=Values[index+layer+row]*ix*t.Y*t.Z;
            value+=Values[index+layer+row+1]*t.X*t.Y*t.Z;
            return (float)value;
        }
    }
}
