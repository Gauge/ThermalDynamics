using System.Collections.Generic;
using VRageMath;
using Thermodynamics.Core;
namespace Thermodynamics
{
    public partial class ThermalGrid
    {
        private uint geometryRevision;
        internal void InvalidateGeometry() { geometryRevision++; }
        internal long GeometryVersion { get { return ((long)Model.Version << 32) | geometryRevision; } }
        internal Dictionary<string, object> CopyGeometry()
        {
            return GridGeometrySnapshot.Copy(Model, GeometryVersion);
        }
    }
}
