using System;
using System.Collections.Generic;
using VRageMath;

namespace Thermodynamics.Presentation
{
    public static class ThermalVisionOcclusion
    {
        public const int MaxCandidateGrids=64;
        public const int MaxFleetTests=2048;
        public const int MaxSubtreeTests=4096;

        // A clipped front face may leave no native depth even though its solid
        // inset intersects every ray. Test the full cube before using that inset.
        public static bool InFrontOfNearPlane(BoundingBoxD fullBounds,Vector3D eye,Vector3D forward,double near)
        {
            double centre=Vector3D.Dot(fullBounds.Center-eye,forward);
            double radius=Vector3D.Dot(fullBounds.HalfExtents,Vector3D.Abs(forward));
            return centre-radius>near;
        }

        // Candidates only: the game adapter must revalidate the live block before
        // using a position as an occluder. A stale candidate never hides geometry.
        public sealed class CandidateCache
        {
            public readonly List<Vector3I> Positions=new List<Vector3I>(32);
            private bool ready;
            private int scannedFrame;
            private long revision;
            private Vector3I centre;

            public bool CanReuse(int frame,long currentRevision,Vector3I currentCentre)
            {
                return ready && frame>=scannedFrame && frame-scannedFrame<15
                    && revision==currentRevision && centre==currentCentre;
            }

            public void Begin(int frame,long currentRevision,Vector3I currentCentre)
            {
                Positions.Clear(); ready=true; scannedFrame=frame;
                revision=currentRevision; centre=currentCentre;
            }
        }

        // One target/owner pair often tests 32 armor cubes with the same inverse
        // transform. Lazily transform each corner once, without a heap allocation.
        // The uncached Hidden overload below remains the independent reference.
        public struct PreparedTarget
        {
            private Vector3D eye, c0,c1,c2,c3,c4,c5,c6,c7;
            private BoundingBoxD world;
            private MatrixD inverse;
            private int mask;
            public int CornerTransforms { get; private set; }

            public PreparedTarget(Vector3D eyeWorld,BoundingBoxD targetWorld,MatrixD worldToBlocker)
            {
                this=new PreparedTarget();
                eye=Vector3D.Transform(eyeWorld,worldToBlocker);
                world=targetWorld; inverse=worldToBlocker;
            }

            private Vector3D Corner(int index)
            {
                if((mask&(1<<index))==0)
                {
                    var corner=new Vector3D((index&1)==0?world.Min.X:world.Max.X,
                        (index&2)==0?world.Min.Y:world.Max.Y,(index&4)==0?world.Min.Z:world.Max.Z);
                    var local=Vector3D.Transform(corner,inverse);
                    switch(index)
                    {
                        case 0:c0=local;break;case 1:c1=local;break;
                        case 2:c2=local;break;case 3:c3=local;break;
                        case 4:c4=local;break;case 5:c5=local;break;
                        case 6:c6=local;break;case 7:c7=local;break;
                    }
                    mask|=1<<index; CornerTransforms++;
                }
                switch(index)
                {
                    case 0:return c0;case 1:return c1;case 2:return c2;case 3:return c3;
                    case 4:return c4;case 5:return c5;case 6:return c6;default:return c7;
                }
            }

            public bool Hidden(BoundingBoxD solid)
            {
                if(solid.Contains(eye)!=ContainmentType.Disjoint) return false;
                for(int i=0;i<8;i++)
                {
                    Vector3D end=Corner(i);
                    if(solid.Contains(end)!=ContainmentType.Disjoint) return false;
                    Vector3D delta=end-eye; double enter=0,exit=1;
                    if(!Slab(eye.X,delta.X,solid.Min.X,solid.Max.X,ref enter,ref exit)
                        || !Slab(eye.Y,delta.Y,solid.Min.Y,solid.Max.Y,ref enter,ref exit)
                        || !Slab(eye.Z,delta.Z,solid.Min.Z,solid.Max.Z,ref enter,ref exit)
                        || enter<=0 || exit>=1 || exit-enter<1e-8) return false;
                }
                return true;
            }
        }

        public static bool Hidden(Vector3D eye, BoundingBoxD targetWorld, MatrixD worldToBlocker,
            BoundingBoxD solid)
        {
            Vector3D localEye=Vector3D.Transform(eye,worldToBlocker);
            if(solid.Contains(localEye)!=ContainmentType.Disjoint) return false;
            for(int i=0;i<8;i++)
            {

                var corner=new Vector3D((i&1)==0?targetWorld.Min.X:targetWorld.Max.X,
                    (i&2)==0?targetWorld.Min.Y:targetWorld.Max.Y,
                    (i&4)==0?targetWorld.Min.Z:targetWorld.Max.Z);
                Vector3D end=Vector3D.Transform(corner,worldToBlocker);
                if(solid.Contains(end)!=ContainmentType.Disjoint) return false;
                Vector3D delta=end-localEye;
                double enter=0, exit=1;
                if(!Slab(localEye.X,delta.X,solid.Min.X,solid.Max.X,ref enter,ref exit)
                    || !Slab(localEye.Y,delta.Y,solid.Min.Y,solid.Max.Y,ref enter,ref exit)
                    || !Slab(localEye.Z,delta.Z,solid.Min.Z,solid.Max.Z,ref enter,ref exit)
                    || enter<=0 || exit>=1 || exit-enter<1e-8) return false;
            }
            return true;
        }

        public static bool HiddenLocal(Vector3D eye,BoundingBoxD target,BoundingBoxD solid)
        {
            if(solid.Contains(eye)!=ContainmentType.Disjoint) return false;
            for(int i=0;i<8;i++)
            {

                var end=new Vector3D((i&1)==0?target.Min.X:target.Max.X,
                    (i&2)==0?target.Min.Y:target.Max.Y,(i&4)==0?target.Min.Z:target.Max.Z);
                if(solid.Contains(end)!=ContainmentType.Disjoint) return false;
                var delta=end-eye;double enter=0,exit=1;
                if(!Slab(eye.X,delta.X,solid.Min.X,solid.Max.X,ref enter,ref exit)
                    || !Slab(eye.Y,delta.Y,solid.Min.Y,solid.Max.Y,ref enter,ref exit)
                    || !Slab(eye.Z,delta.Z,solid.Min.Z,solid.Max.Z,ref enter,ref exit)
                    || enter<=0 || exit>=1 || exit-enter<1e-8)return false;
            }
            return true;
        }

        private static bool Slab(double origin,double delta,double min,double max,ref double enter,ref double exit)
        {
            if(Math.Abs(delta)<1e-12) return origin>min && origin<max;
            double a=(min-origin)/delta, b=(max-origin)/delta;
            if(a>b) { double swap=a; a=b; b=swap; }
            enter=Math.Max(enter,a); exit=Math.Min(exit,b);
            return enter<exit;
        }
    }
}
