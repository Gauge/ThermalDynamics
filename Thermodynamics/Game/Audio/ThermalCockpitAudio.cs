using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using ThermalDynamics;
using VRage.Data.Audio;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Thermodynamics.Audio
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Cockpit), true)]
    public sealed class ThermalCockpitAudio : MyGameLogicComponent
    {
        
        private const float BASE_VOLUME_MULTIPLIER = 0.5f;
        private const float OPEN_COCKPIT_RADIUS = 7.5f;
        private const float ENCLOSED_COCKPIT_RADIUS = 5f;
        private const float COCKPIT_SCREEN_MARGIN = 0.25f;

        // Raw PCM voices radius falloff does not work properly with 3D spatialization,
        // this is just a ridiculous high number so we can control manually with volume
        private const float SPATIALIZATION_MAX_DISTANCE = 100000f;

        private static readonly List<ThermalCockpitAudio> Active =
            new List<ThermalCockpitAudio>();

        private MyCockpit _cockpit;
        private MyEntity3DSoundEmitter _emitter;
        private bool _playing2D;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            _cockpit = Entity as MyCockpit;
            if (_cockpit != null) Active.Add(this);
        }

        public override void UpdateAfterSimulation()
        {
            if (_emitter == null || !_emitter.IsPlaying)
            {
                NeedsUpdate &= ~MyEntityUpdateEnum.EACH_FRAME;
                return;
            }

            if (!_playing2D) ApplyDistanceGain();
        }

        public override void Close()
        {
            Active.Remove(this);
            CloseEmitter();
            _cockpit = null;
            base.Close();
        }

        public static int PlayOnGrid(IMyCubeGrid grid, byte[] pcm)
        {
            if (grid == null || pcm == null || pcm.Length == 0) return 0;

            int played = 0;
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                ThermalCockpitAudio audio = Active[i];
                if (!audio.IsAvailable)
                {
                    audio.CloseEmitter();
                    Active.RemoveAt(i);
                    continue;
                }

                if (audio._cockpit.CubeGrid.EntityId != grid.EntityId) continue;
                if (audio.Play(pcm)) played++;
            }

            return played;
        }

        public static bool IsPlayingOnGrid(IMyCubeGrid grid)
        {
            if (grid == null) return false;

            for (int i = Active.Count - 1; i >= 0; i--)
            {
                ThermalCockpitAudio audio = Active[i];
                if (!audio.IsAvailable)
                {
                    audio.CloseEmitter();
                    Active.RemoveAt(i);
                    continue;
                }

                if (audio._cockpit.CubeGrid.EntityId == grid.EntityId
                    && audio._emitter != null
                    && audio._emitter.IsPlaying)
                {
                    return true;
                }
            }

            return false;
        }

        public static void Reset()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                Active[i].CloseEmitter();
            }
            Active.Clear();
        }

        private bool IsAvailable =>
            _cockpit != null
            && !_cockpit.MarkedForClose
            && _cockpit.CubeGrid != null;

        private bool Play(byte[] pcm)
        {
            if (!IsAvailable || !CanPlayWarning()) return false;

            bool use2D = IsOccupiedByLocalPlayer();
            EnsureEmitter(use2D);
            if (_emitter == null || _emitter.IsPlaying) return false;

            byte[] samples = new byte[pcm.Length];
            Buffer.BlockCopy(pcm, 0, samples, 0, pcm.Length);

            if (use2D)
            {
                _emitter.VolumeMultiplier = 1f;
            }
            else
            {
                ApplyDistanceGain();
            }


            _emitter.Force2D = use2D;
            _emitter.Force3D = !use2D;
            
            _emitter.PlaySound(
                samples,
                volume: 1f,
                maxDistance: SPATIALIZATION_MAX_DISTANCE);

            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            return true;
        }

        private bool CanPlayWarning()
        {
            if (_cockpit.BlockDefinition != null && _cockpit.BlockDefinition.EnableShipControl)
                return true; // cockpits / control seats

            IMyTextSurfaceProvider provider = _cockpit;
            return provider.SurfaceCount > 0; // decorative console blocks
        }

        private void EnsureEmitter(bool use2D)
        {
            if (_emitter != null && _playing2D == use2D) return;

            CloseEmitter();
            _playing2D = use2D;

            _emitter = new MyEntity3DSoundEmitter(_cockpit, dopplerScaler: 0f)
            {
                Force2D = use2D,
                Force3D = !use2D,
                CustomMaxDistance = SPATIALIZATION_MAX_DISTANCE,
                CustomVolume = 1f,
                VolumeMultiplier = 1f
            };

            _emitter.EmitterMethods[(int)MyEntity3DSoundEmitter.MethodsEnum.CanHear].ClearImmediate();
            _emitter.EmitterMethods[(int)MyEntity3DSoundEmitter.MethodsEnum.ImplicitEffect].ClearImmediate();
        }

        private bool IsOccupiedByLocalPlayer()
        {
            IMyPlayer player = MyAPIGateway.Session == null
                ? null
                : MyAPIGateway.Session.LocalHumanPlayer;

            return player != null
                && player.Character != null
                && ReferenceEquals(_cockpit.Pilot, player.Character);
        }

        private void ApplyDistanceGain()
        {
            if (_emitter == null || _cockpit == null)
                return;

            bool enclosed = _cockpit.BlockDefinition != null
                            && _cockpit.BlockDefinition.IsPressurized;

            Vector3D sourcePosition = GetAudioPosition();
            _emitter.SetPosition(sourcePosition);

            float radius = enclosed
                ? ENCLOSED_COCKPIT_RADIUS
                : OPEN_COCKPIT_RADIUS;

            if (_cockpit.CubeGrid.GridSizeEnum == MyCubeSize.Small)
                radius *= 0.6f; // 2.5m for large grid, ~50cm (3*3) small grid

            double distance = 0.0;

            if (MyAPIGateway.Session != null &&
                MyAPIGateway.Session.Camera != null)
            {
                Vector3D listener = MyAPIGateway.Session.Camera.Position;
                distance = Vector3D.Distance(
                    listener,
                    sourcePosition
                );
            }

            float t = MathHelper.Clamp((float)(distance / radius), 0f, 1f);

            // Smoothstep falloff
            float smooth = t * t * (3f - 2f * t);

            // Slightly stronger rolloff toward the edge
            float gain = 1f - smooth;
            gain *= gain;

            _emitter.VolumeMultiplier = gain * BASE_VOLUME_MULTIPLIER;
        }

        private Vector3D GetAudioPosition()
        {
            BoundingBox localBounds = _cockpit.PositionComp.LocalAABB;
            MatrixD world = _cockpit.PositionComp.WorldMatrixRef;
            Vector3D center = Vector3D.Transform(localBounds.Center, world);
            
            // Assuming the cockpit is facing forward (it should to keep consistent the controls)
            // The player will be in the center of the block, with screens in front of then in the top OR bottom of the
            // block so make the audio come from that area, biased to the bottom to accommodated enclosed cockpits
            // (I know, I know, it is wrong on the "Suspended Control Seat" (and a little bit on the
            // "Industrial Cockpit"), C'est la vie).
            double bias = 1.0 - COCKPIT_SCREEN_MARGIN * 2.0;
            return center
                + world.Forward * (localBounds.HalfExtents.Z * bias)
                + world.Down * (localBounds.HalfExtents.Y * bias);
        }

        private void CloseEmitter()
        {
            if (_emitter == null) return;

            try
            {
                _emitter.StopSound(forced: true, cleanUp: true, cleanupSound: true);
            }
            catch (Exception error)
            {
                LogHelper.Log(
                    VRage.Utils.MyLogSeverity.Warning,
                    "ThermalCockpitAudio: emitter cleanup failed: " + error.Message);
            }

            _emitter = null;
        }
    }
}
