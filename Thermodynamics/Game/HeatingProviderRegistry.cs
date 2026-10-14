using System;
using System.Collections.Generic;

namespace Thermodynamics
{
    // Engine-independent callback boundary. Successful callbacks take no lock and allocate
    // nothing. Registration and failure handoff are cold paths; engine work belongs to the
    // caller draining failures after the grid workers have joined.
    internal sealed class HeatingProviderRegistry
    {
        private sealed class Provider
        {
            public readonly long Owner;
            public readonly Dictionary<string, Delegate> Methods;
            public readonly Func<float, float, float, float, float, float, float, float> Heat;
            public readonly Func<float, bool> Gate;
            public volatile bool Failed;

            public Provider(long owner, Dictionary<string, Delegate> methods,
                Func<float, float, float, float, float, float, float, float> heat)
            {
                Owner = owner;
                Methods = methods;
                Heat = heat;
                Delegate gate;
                Gate = methods.TryGetValue("SurfaceHeatingGate", out gate) ? gate as Func<float, bool> : null;
            }
        }

        private sealed class Failure
        {
            public Provider Provider;
            public string Context;
            public Exception Error;
        }

        private readonly object sync = new object();
        private readonly Queue<Failure> failures = new Queue<Failure>();
        private volatile Provider current;

        public bool Register(long owner, Dictionary<string, Delegate> supplied)
        {
            lock (sync)
            {
                Provider old = current;
                if (owner == 0 || (old != null && old.Owner != owner)) return false;
                if (supplied == null) { current = null; return true; }
                Dictionary<string, Delegate> copy = new Dictionary<string, Delegate>(supplied);
                Delegate method;
                if (!copy.TryGetValue("SurfaceHeating", out method)) return false;
                var heat = method as Func<float, float, float, float, float, float, float, float>;
                if (heat == null) return false;
                current = new Provider(owner, copy, heat);
                return true;
            }
        }

        public T Method<T>(string name) where T : class
        {
            Provider provider = current;
            Delegate method;
            return provider != null && !provider.Failed && provider.Methods.TryGetValue(name, out method)
                ? method as T : null;
        }

        public bool Active(float speed)
        {
            Provider provider = current;
            if (provider == null || provider.Failed) return false;
            try { return provider.Gate == null ? speed > 0f : provider.Gate(speed); }
            catch (Exception error) { Fail(provider, "ThermalApi.SurfaceHeatingGate", error); return false; }
        }

        public float Heat(float area, float density, float speed, float ambient, float temperature, float capacity, float seconds)
        {
            Provider provider = current;
            if (provider == null || provider.Failed) return 0f;
            try { return provider.Heat(area, density, speed, ambient, temperature, capacity, seconds); }
            catch (Exception error) { Fail(provider, "ThermalApi.SurfaceHeating", error); return 0f; }
        }

        private void Fail(Provider provider, string context, Exception error)
        {
            lock (sync)
            {
                // A late callback from a replaced/unregistered provider must not affect its successor.
                if (current != provider || provider.Failed) return;
                provider.Failed = true;
                failures.Enqueue(new Failure { Provider = provider, Context = context, Error = error });
            }
        }

        public bool TryTakeFailure(out string context, out Exception error, out bool unavailable)
        {
            lock (sync)
            {
                context = null;
                error = null;
                unavailable = false;
                if (failures.Count == 0) return false;
                Failure failure = failures.Dequeue();
                context = failure.Context;
                error = failure.Error;
                if (current == failure.Provider)
                {
                    current = null;
                    unavailable = true;
                }
                return true;
            }
        }

        public void Clear()
        {
            lock (sync)
            {
                current = null;
                failures.Clear();
            }
        }
    }
}
