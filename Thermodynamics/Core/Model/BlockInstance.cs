using System;
using System.Collections.Generic;
using VRageMath;

namespace Thermodynamics.Core
{
    public class BlockInstance
    {
        public BlockModel Model;

        public Vector3I Position;

        public Vector3I Min;

        public Vector3I MaxExclusive;

        public BlockOrientation Orientation;

        public float Mass;

        public DragProfile Drag;


        public float PowerProducedWatts;

        public float PowerConsumedWatts;

        public float ThrustWatts;

        public bool IsSealedByDoorState = true;

        public long Key;

        public int NodeIndex = -1;

        public int GridSlot = -1;

        private int[] gridSurfaces;
        private int[] gridStructuralSurfaces;
        private Vector3I[] gridCells;

        private BlockModel.FaceFractions fractions;


        public BlockInstance(BlockModel model, Vector3I min, BlockOrientation orientation)
        {
            if (model == null) throw new ArgumentNullException("model");

            Model = model;
            Orientation = orientation;
            Min = min;
            Mass = model.Mass;

            Vector3I rotatedSize = Vector3I.Abs(orientation.Rotate(model.Size));
            MaxExclusive = min + rotatedSize;
            Position = min;
            Key = GridMath.Key(Position);

            BuildGridSurfaces();
        }

        public Vector3I[] Cells
        {
            get { return gridCells; }
        }

        public int[] SelfSurfaces
        {
            get { return gridSurfaces; }
        }

        public int[] StructuralSurfaces
        {
            get { return gridStructuralSurfaces; }
        }

        public bool HasStateDependentSealing
        {
            get { return Model.HasOpenState; }
        }


        public bool IsPortalFace(int gridFace)
        {
            if (!HasStateDependentSealing) return false;
            if (gridFace < 0 || gridFace >= Face.Count) return false;

            return fractions.SealClosed[gridFace] > 0f && fractions.SealOpen[gridFace] <= 0f;
        }

        public Vector3I Extents
        {
            get { return MaxExclusive - Min; }
        }


        public float MountFraction(int gridFace)
        {
            return (gridFace >= 0 && gridFace < Face.Count) ? fractions.Mount[gridFace] : 0f;
        }


        public float SealFraction(int gridFace)
        {
            if (gridFace < 0 || gridFace >= Face.Count) return 0f;
            return IsSealedByDoorState ? fractions.SealClosed[gridFace] : fractions.SealOpen[gridFace];
        }


        public int FaceAreaCells(int gridFace)
        {
            return BoxGeometry.FaceAreaCells(Extents, gridFace);
        }

        public int CellCount
        {
            get { return gridCells.Length; }
        }

        public BlockThermalProperties Thermal
        {
            get { return Model.Thermal; }
        }

        public string Name
        {
            get { return Model.Name; }
        }


        public Vector3I LocalToGrid(Vector3I localCell)
        {
            Vector3I rotated = Orientation.Rotate(localCell);
            return Min + (rotated - RotatedLocalMin());
        }


        public Vector3I LocalDirectionToGrid(Vector3I localDirection)
        {
            return Orientation.Rotate(localDirection);
        }


        private Vector3I RotatedLocalMin()
        {
            Vector3I a = Orientation.Rotate(Vector3I.Zero);
            Vector3I b = Orientation.Rotate(Model.Size - Vector3I.One);
            return Vector3I.Min(a, b);
        }


        private void BuildFaceFractions()
        {
            fractions = Model.FractionsFor(Orientation);
        }


        private void BuildGridSurfaces()
        {
            BuildFaceFractions();

            int count = Model.CellCount;
            gridCells = new Vector3I[count];

            bool shared = IsSealedByDoorState || !Model.HasOpenState;

            if (count == 1)
            {
                gridCells[0] = Min;


                gridStructuralSurfaces = OneCellSurfaces(true);

                gridSurfaces = shared ? gridStructuralSurfaces : OneCellSurfaces(false);
                return;
            }

            gridStructuralSurfaces = new int[count];
            gridSurfaces = shared ? gridStructuralSurfaces : new int[count];

            BuildGridSurfacesWalkingTheCells();
        }


        public void BuildGridSurfacesWalkingTheCells()
        {
            DetachInternedSurfaces();


            bool shared = ReferenceEquals(gridSurfaces, gridStructuralSurfaces);
            int i = 0;
            foreach (Vector3I local in Model.LocalCells())
            {

                gridCells[i] = LocalToGrid(local);

                gridStructuralSurfaces[i] = RotateSurface(Model.LocalSurfaceState(local, true));
                if (!shared)
                {
                    gridSurfaces[i] = IsSealedByDoorState
                        ? gridStructuralSurfaces[i]

                        : RotateSurface(Model.LocalSurfaceState(local, false));
                }
                i++;
            }
        }


        private void DetachInternedSurfaces()
        {
            if (Model.CellCount != 1) return;


            bool shared = ReferenceEquals(gridSurfaces, gridStructuralSurfaces);
            int[] structural = { gridStructuralSurfaces[0] };
            int[] live = shared ? structural : new[] { gridSurfaces[0] };

            gridStructuralSurfaces = structural;
            gridSurfaces = live;
        }


        private int[] OneCellSurfaces(bool structural)
        {
            int[] known = Model.OneCellSurfaces(Orientation, structural);
            if (known != null) return known;

            return Model.StoreOneCellSurfaces(Orientation, structural,
                RotateSurface(Model.LocalSurfaceState(Vector3I.Zero, structural)));
        }


        private int RotateSurface(int localState)
        {
            if (localState == (CellSurface.SelfAirtightMask | CellSurface.SelfMountMask)
                || localState == 0)
            {
                return localState;
            }

            int rotated = 0;
            for (int face = 0; face < Face.Count; face++)
            {
                int gridFace = Orientation.RotateFace(face);
                if (gridFace < 0) continue;

                if (CellSurface.SelfAirtight(localState, face))
                {
                    rotated = CellSurface.WithSelfAirtight(rotated, gridFace, true);
                }
                if (CellSurface.SelfMount(localState, face))
                {
                    rotated = CellSurface.WithSelfMount(rotated, gridFace, true);
                }
            }
            return rotated;
        }


        public void RefreshSurfaces()
        {
            BuildGridSurfaces();
        }


        public List<GridPort> CoolantLinkPorts()
        {

            return ToGridPorts(Model.Coolant == null ? null : Model.Coolant.LinkPorts);
        }


        public List<GridPort> CoolantSinkPorts()
        {

            return ToGridPorts(Model.Coolant == null ? null : Model.Coolant.SinkPorts);
        }


        public bool TryHeatPumpCells(out Vector3I coldCell, out Vector3I hotCell)
        {
            HeatPumpShape shape = Model.HeatPump;
            if (shape == null)
            {
                coldCell = Vector3I.Zero;
                hotCell = Vector3I.Zero;
                return false;
            }


            coldCell = LocalToGrid(shape.ColdCell) + LocalDirectionToGrid(shape.ColdDirection);

            hotCell = LocalToGrid(shape.HotCell) + LocalDirectionToGrid(shape.HotDirection);
            return true;
        }


        private List<GridPort> ToGridPorts(CoolantPort[] ports)
        {

            List<GridPort> result = new List<GridPort>();
            if (ports == null) return result;

            for (int i = 0; i < ports.Length; i++)
            {
                result.Add(new GridPort(
                    LocalToGrid(ports[i].LocalCell),
                    LocalDirectionToGrid(ports[i].LocalDirection)));
            }
            return result;
        }


        public override string ToString()
        {
            return Model.Name + "@" + Min;
        }
    }

    public struct GridPort
    {
        public Vector3I Cell;
        public Vector3I Direction;


        public GridPort(Vector3I cell, Vector3I direction)
        {
            Cell = cell;
            Direction = direction;
        }

        public Vector3I Target
        {
            get { return Cell + Direction; }
        }
    }
}
