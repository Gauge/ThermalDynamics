using System;
using System.Globalization;
using System.Xml.Linq;
using VRageMath;

namespace Thermodynamics.Harness
{
    /// <summary>Explicit world pose for a blueprint grid; missing or malformed poses remain visible to flight labs.</summary>
    public sealed class BlueprintFrame
    {
        public readonly MatrixD World;
        public readonly string Error;
        public bool Valid { get { return Error == null; } }

        private BlueprintFrame(MatrixD world, string error) { World = world; Error = error; }

        public static BlueprintFrame Read(XElement pose)
        {
            if (pose == null) return new BlueprintFrame(MatrixD.Identity, "missing PositionAndOrientation");
            try
            {
                Vector3D position = Vector3D.Zero;
                Vector3 forward = Vector3.Zero, up = Vector3.Zero;
                bool hasPosition = false, hasForward = false, hasUp = false;
                foreach (XElement part in pose.Elements())
                {
                    switch (part.Name.LocalName)
                    {
                        case "Position":
                            position = new Vector3D(Component(part, "x"), Component(part, "y"), Component(part, "z"));
                            hasPosition = true;
                            break;
                        case "Forward":
                            forward = new Vector3((float)Component(part, "x"), (float)Component(part, "y"), (float)Component(part, "z"));
                            hasForward = true;
                            break;
                        case "Up":
                            up = new Vector3((float)Component(part, "x"), (float)Component(part, "y"), (float)Component(part, "z"));
                            hasUp = true;
                            break;
                        case "Orientation":
                            Quaternion q = new Quaternion((float)Component(part, "X"), (float)Component(part, "Y"),
                                (float)Component(part, "Z"), (float)Component(part, "W"));
                            float length = q.LengthSquared();
                            if (!Finite(length) || Math.Abs(length - 1f) > 1e-3f)
                                throw new FormatException("orientation quaternion is not unit length");
                            Matrix rotation = Matrix.CreateFromQuaternion(q);
                            forward = rotation.Forward; up = rotation.Up;
                            hasForward = hasUp = true;
                            break;
                    }
                }
                if (!hasPosition || !hasForward || !hasUp) throw new FormatException("incomplete grid pose");
                if (!forward.IsValid() || !up.IsValid() || forward.LengthSquared() < 1e-12f || up.LengthSquared() < 1e-12f
                    || Math.Abs(Vector3.Dot(Vector3.Normalize(forward), Vector3.Normalize(up))) > 1e-3f)
                    throw new FormatException("grid axes are nonfinite, zero or nonorthogonal");
                return new BlueprintFrame(MatrixD.CreateWorld(position, forward, up), null);
            }
            catch (FormatException error) { return new BlueprintFrame(MatrixD.Identity, error.Message); }
        }

        private static double Component(XElement element, string name)
        {
            string text = (string)element.Attribute(name) ?? (string)element.Element(name);
            double value;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || !Finite(value))
                throw new FormatException("missing or nonfinite " + element.Name.LocalName + "." + name);
            return value;
        }

        private static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
    }
}
