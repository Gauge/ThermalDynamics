using Thermodynamics.Presentation;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>One planet snapshot tracks definition identity, supplied fields and mutable world overrides.</summary>
    public class ThermalVisionClimateCacheTests
    {
        [Fact]
        public void SkyClimateCacheTracksMutableInputsAndKeepsUnknownDefinitionsUnknown()
        {
            var cache=new ThermalVisionClimateCache<object>();
            var key=new ThermalVisionClimateCache<object>.Key {
                Definition=new object(),Supplied=35,DefinitionValues=new Vector3(280,310,40)
            };
            var first=new object(); object result;
            Assert.False(cache.TryGet(key,out result));
            cache.Store(key,first); Assert.True(cache.TryGet(key,out result)); Assert.Same(first,result);

            // In-place definition edits retain identity but invalidate the exact input snapshot.
            for(int component=0;component<3;component++)
            {
                var changed=key;
                if(component==0) changed.DefinitionValues.X++;
                if(component==1) changed.DefinitionValues.Y++;
                if(component==2) changed.DefinitionValues.Z++;
                Assert.False(cache.TryGet(changed,out result));
            }
            var flags=key; flags.Supplied=0; Assert.False(cache.TryGet(flags,out result));
            var replaced=key; replaced.Definition=new object(); Assert.False(cache.TryGet(replaced,out result));

            key.HasWorld=true; key.WorldValues=new Vector3(270,330,45);
            Assert.False(cache.TryGet(key,out result));
            var overridden=new object(); cache.Store(key,overridden);
            Assert.True(cache.TryGet(key,out result)); Assert.Same(overridden,result);
            for(int component=0;component<3;component++)
            {
                var changed=key;
                if(component==0) changed.WorldValues.X++;
                if(component==1) changed.WorldValues.Y++;
                if(component==2) changed.WorldValues.Z++;
                Assert.False(cache.TryGet(changed,out result));
            }
            var noWorld=key; noWorld.HasWorld=false; Assert.False(cache.TryGet(noWorld,out result));
            var unknown=key; unknown.Definition=null;
            Assert.False(cache.TryGet(unknown,out result));
            cache.Store(unknown,overridden); Assert.False(cache.TryGet(unknown,out result));
            Assert.False(cache.TryGet(key,out result));
        }
    }
}
