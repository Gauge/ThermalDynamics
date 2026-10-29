using VRageMath;

namespace Thermodynamics.Presentation
{
    /// <summary>One sky-only climate result, invalidated by every scalar consumed by its surface shading.</summary>
    public sealed class ThermalVisionClimateCache<T>
    {
        public struct Key
        {
            public object Definition;
            public int Supplied;
            public Vector3 DefinitionValues,WorldValues;
            public bool HasWorld;

            public bool Equals(Key other)
            {
                return ReferenceEquals(Definition,other.Definition) && Supplied==other.Supplied
                    && DefinitionValues.Equals(other.DefinitionValues) && HasWorld==other.HasWorld
                    && WorldValues.Equals(other.WorldValues);
            }
        }

        private bool ready;
        private Key key;
        private T value;

        public bool TryGet(Key current,out T result)
        {
            result=default(T);
            if(current.Definition==null || !ready || !key.Equals(current)) return false;
            result=value; return true;
        }

        public void Store(Key current,T result)
        {
            key=current; ready=current.Definition!=null;
            value=ready?result:default(T);
        }
    }
}
