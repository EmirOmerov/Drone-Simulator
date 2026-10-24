using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DroneSim.EditorTools
{
    /// <summary>
    /// A fly-through factory hall: brick + corrugated-steel walls with big doors on both ends and a side door,
    /// skylights in the roof, steel columns and trusses, storage racks, a mezzanine to fly under, pipes,
    /// hanging lamps and a gate course through it all.
    ///
    /// Hall footprint: x ∈ [-18, 18], z ∈ [15, 75], height 12 m.
    /// </summary>
    static class FactoryBuilder
    {
        const float HalfWidth = 18f, ZStart = 15f, ZEnd = 75f, Height = 12f, WallThickness = 0.4f;

        static Material steel, darkSteel, rackBlue, cardboard;

        public static void Build()
        {
            steel = MaterialLibrary.Plain("SteelYellow", new Color(0.93f, 0.72f, 0.12f), 0.45f, 0.3f);
            darkSteel = MaterialLibrary.Plain("SteelDark", new Color(0.18f, 0.19f, 0.21f), 0.5f, 0.6f);
            rackBlue = MaterialLibrary.Plain("RackBlue", new Color(0.15f, 0.3f, 0.65f), 0.4f, 0.3f);
            cardboard = MaterialLibrary.Plain("Cardboard", new Color(0.62f, 0.47f, 0.3f), 0.15f);

            var factory = new GameObject("Factory").transform;
            BuildShell(factory);
            BuildStructure(factory);
            BuildMezzanine(factory);
            BuildRack(factory, "RackEast", 13.6f);
            BuildRack(factory, "RackWest", -13.6f);
            BuildConveyor(factory);
            BuildFloorStacks(factory);
            BuildPipes(factory);
            BuildLamps(factory);
        }

        // ------------------------------------------------------------ shell

        static void BuildShell(Transform factory)
        {
            var shell = Geo.Group(factory, "Shell");
            Material bricks = MaterialLibrary.Bricks, metal = MaterialLibrary.CorrugatedSteel;
            const float split = 4f; // bricks below, corrugated steel above

            // Openings are (u along the wall from its start, v height, width, height).
            var bigDoor = new Rect(12f, 0f, 12f, 9f);
            Wall(shell, "WallSouth", new Vector3(-HalfWidth, 0f, ZStart), new Vector3(HalfWidth, 0f, ZStart),
                split, bricks, metal, bigDoor);
            Wall(shell, "WallNorth", new Vector3(HalfWidth, 0f, ZEnd), new Vector3(-HalfWidth, 0f, ZEnd),
                split, bricks, metal, bigDoor);

            var eastOpenings = new List<Rect> { new Rect(23f, 0f, 10f, 7f) }; // side door facing the road
            var westOpenings = new List<Rect>();
            for (float u = 3f; u + 4f < ZEnd - ZStart; u += 6f)
            {
                var window = new Rect(u, 7f, 4f, 2.2f);
                if (!window.Overlaps(eastOpenings[0]))
                    eastOpenings.Add(window);
                westOpenings.Add(window);
            }
            Wall(shell, "WallEast", new Vector3(HalfWidth, 0f, ZStart), new Vector3(HalfWidth, 0f, ZEnd),
                split, bricks, metal, eastOpenings.ToArray());
            Wall(shell, "WallWest", new Vector3(-HalfWidth, 0f, ZEnd), new Vector3(-HalfWidth, 0f, ZStart),
                split, bricks, metal, westOpenings.ToArray());

            // Roof strips with open skylights between them: sunlight falls into the hall in bands.
            float[] skylights = { 27f, 43f, 59f };
            float z0 = ZStart - 0.5f;
            var roof = Geo.Group(shell, "Roof");
            foreach (float gap in skylights.Concat(new[] { ZEnd + 0.5f }))
            {
                float z1 = gap;
                Geo.Box(roof, "RoofStrip", new Vector3(0f, Height + 0.15f, (z0 + z1) * 0.5f),
                    new Vector3(HalfWidth * 2f + 1f, 0.3f, z1 - z0), metal);
                z0 = gap + 2f;
            }
        }

        /// <summary>
        /// Builds a wall from <paramref name="start"/> to <paramref name="end"/> out of boxes, leaving the openings
        /// empty. The wall is split into a grid at every opening edge and at <paramref name="split"/>; vertically
        /// adjacent solid cells of the same material are merged.
        /// </summary>
        static void Wall(Transform parent, string name, Vector3 start, Vector3 end, float split,
            Material lower, Material upper, params Rect[] openings)
        {
            Vector3 direction = (end - start).normalized;
            const float extend = WallThickness * 0.5f; // close the corners
            float length = Vector3.Distance(start, end) + extend * 2f;
            var wall = Geo.Group(parent, name, start - direction * extend, Quaternion.LookRotation(direction, Vector3.up));

            var shifted = openings.Select(o => new Rect(o.x + extend, o.y, o.width, o.height)).ToArray();
            var us = new SortedSet<float> { 0f, length };
            var vs = new SortedSet<float> { 0f, Height, split };
            foreach (Rect o in shifted)
            {
                us.Add(Mathf.Clamp(o.xMin, 0f, length));
                us.Add(Mathf.Clamp(o.xMax, 0f, length));
                vs.Add(Mathf.Clamp(o.yMin, 0f, Height));
                vs.Add(Mathf.Clamp(o.yMax, 0f, Height));
            }
            float[] u = us.ToArray(), v = vs.ToArray();

            for (int i = 0; i < u.Length - 1; i++)
            {
                float uc = (u[i] + u[i + 1]) * 0.5f, du = u[i + 1] - u[i];
                int runStart = -1;
                Material runMaterial = null;

                for (int j = 0; j <= v.Length - 1; j++)
                {
                    bool solid = false;
                    Material cellMaterial = null;
                    if (j < v.Length - 1)
                    {
                        float vc = (v[j] + v[j + 1]) * 0.5f;
                        solid = !shifted.Any(o => o.Contains(new Vector2(uc, vc)));
                        cellMaterial = v[j + 1] <= split + 1e-3f ? lower : upper;
                    }

                    bool continues = solid && runStart >= 0 && cellMaterial == runMaterial;
                    if (!continues && runStart >= 0)
                    {
                        float v0 = v[runStart], v1 = v[j];
                        Geo.Box(wall, "Panel", new Vector3(0f, (v0 + v1) * 0.5f, uc),
                            new Vector3(WallThickness, v1 - v0, du), runMaterial);
                        runStart = -1;
                    }
                    if (solid && runStart < 0)
                    {
                        runStart = j;
                        runMaterial = cellMaterial;
                    }
                }
            }
        }

        // ------------------------------------------------------------ steel structure

        static void BuildStructure(Transform factory)
        {
            var structure = Geo.Group(factory, "Structure");
            for (float z = 25f; z <= 65f; z += 10f)
            {
                foreach (float x in new[] { -10f, 10f })
                    Geo.Box(structure, "Column", new Vector3(x, 5.6f, z), new Vector3(0.5f, 11.2f, 0.5f), steel);
                Geo.Box(structure, "Truss", new Vector3(0f, 11.5f, z), new Vector3(HalfWidth * 2f, 0.6f, 0.3f), steel);
                Geo.Box(structure, "TrussBrace", new Vector3(0f, 10.9f, z), new Vector3(20f, 0.15f, 0.15f), steel);
            }
            foreach (float x in new[] { -10f, 10f })
                Geo.Box(structure, "CraneRail", new Vector3(x, 10.2f, 45f), new Vector3(0.4f, 0.6f, ZEnd - ZStart), steel);

            // Overhead crane bridge parked mid-hall.
            Geo.Box(structure, "CraneBridge", new Vector3(0f, 10.7f, 48f), new Vector3(20.4f, 0.8f, 0.8f),
                MaterialLibrary.RedPaintedMetal);
            Geo.Box(structure, "CraneHook", new Vector3(2f, 8.7f, 48f), new Vector3(0.6f, 0.8f, 0.6f), darkSteel);
            Geo.Box(structure, "CraneCable", new Vector3(2f, 9.8f, 48f), new Vector3(0.05f, 1.4f, 0.05f), darkSteel,
                collider: false);
        }

        static void BuildMezzanine(Transform factory)
        {
            // West side, z ∈ [50, 72], deck at 4.5 m: fly under it (gate 3) or over it.
            var mezzanine = Geo.Group(factory, "Mezzanine");
            const float x0 = -18f, x1 = -6f, z0 = 50f, z1 = 72f, deck = 4.5f;
            Geo.Box(mezzanine, "Deck", new Vector3((x0 + x1) * 0.5f, deck + 0.15f, (z0 + z1) * 0.5f),
                new Vector3(x1 - x0, 0.3f, z1 - z0), MaterialLibrary.MetalPlates);

            foreach (float z in new[] { z0 + 0.5f, (z0 + z1) * 0.5f, z1 - 0.5f })
            foreach (float x in new[] { x1 - 0.3f, x0 + 0.6f })
                Geo.Box(mezzanine, "Post", new Vector3(x, deck * 0.5f, z), new Vector3(0.3f, deck, 0.3f), darkSteel);

            float railX = x1 - 0.1f;
            Geo.Box(mezzanine, "RailTop", new Vector3(railX, deck + 1.3f, (z0 + z1) * 0.5f),
                new Vector3(0.06f, 0.06f, z1 - z0), steel);
            Geo.Box(mezzanine, "RailMid", new Vector3(railX, deck + 0.8f, (z0 + z1) * 0.5f),
                new Vector3(0.05f, 0.05f, z1 - z0), steel, collider: false);
            for (float z = z0; z <= z1; z += 2f)
                Geo.Box(mezzanine, "RailPost", new Vector3(railX, deck + 0.8f, z), new Vector3(0.06f, 1f, 0.06f),
                    steel, collider: false);

            // Crates stored on top of the deck.
            var random = new System.Random(21);
            for (int i = 0; i < 10; i++)
                Crate(mezzanine, new Vector3(x0 + 1.5f + (float)random.NextDouble() * 8f, deck + 0.3f,
                    z0 + 1.5f + (float)random.NextDouble() * 19f), random);
        }

        static void BuildRack(Transform factory, string name, float centreX)
        {
            var rack = Geo.Group(factory, name);
            const float z0 = 20f, z1 = 44f, depth = 1.2f, height = 6f, bay = 2.4f;
            float[] shelves = { 0.15f, 2f, 4f, 5.9f };

            for (float z = z0; z <= z1 + 0.01f; z += bay)
            foreach (float side in new[] { -depth * 0.5f, depth * 0.5f })
                Geo.Box(rack, "Upright", new Vector3(centreX + side, height * 0.5f, z), new Vector3(0.08f, height, 0.08f),
                    rackBlue);

            var random = new System.Random(name.GetHashCode());
            foreach (float y in shelves)
            {
                Geo.Box(rack, "Shelf", new Vector3(centreX, y, (z0 + z1) * 0.5f), new Vector3(depth, 0.08f, z1 - z0),
                    MaterialLibrary.MetalPlates);
                if (y > 5f)
                    continue; // nothing on the top shelf
                for (float z = z0 + bay * 0.5f; z < z1; z += bay)
                    if (random.NextDouble() < 0.7)
                        Crate(rack, new Vector3(centreX, y + 0.04f, z), random);
            }
        }

        static void BuildConveyor(Transform factory)
        {
            var conveyor = Geo.Group(factory, "Conveyor");
            const float x = 5.5f, z0 = 22f, z1 = 46f;
            Geo.Box(conveyor, "Frame", new Vector3(x, 0.85f, (z0 + z1) * 0.5f), new Vector3(1.3f, 0.2f, z1 - z0),
                MaterialLibrary.MetalPlates);
            Geo.Box(conveyor, "Belt", new Vector3(x, 0.97f, (z0 + z1) * 0.5f), new Vector3(1.1f, 0.04f, z1 - z0),
                MaterialLibrary.Plain("Rubber", new Color(0.06f, 0.06f, 0.06f), 0.2f), collider: false);
            for (float z = z0 + 1f; z < z1; z += 3f)
            foreach (float side in new[] { -0.55f, 0.55f })
                Geo.Box(conveyor, "Leg", new Vector3(x + side, 0.4f, z), new Vector3(0.08f, 0.8f, 0.08f), darkSteel);

            var random = new System.Random(5);
            for (float z = z0 + 2f; z < z1 - 1f; z += 4f + (float)random.NextDouble() * 2f)
                Geo.Box(conveyor, "Parcel", new Vector3(x, 1.2f, z), new Vector3(0.6f, 0.4f, 0.5f), cardboard);
        }

        static void BuildFloorStacks(Transform factory)
        {
            var stacks = Geo.Group(factory, "FloorStacks");
            var random = new System.Random(9);
            Vector3[] spots =
            {
                new Vector3(8f, 0f, 54f), new Vector3(8f, 0f, 60f), new Vector3(8f, 0f, 66f),
                new Vector3(-16.2f, 0f, 53f), new Vector3(-16f, 0f, 67f), new Vector3(14f, 0f, 52f),
                new Vector3(-6f, 0f, 20f), new Vector3(7f, 0f, 19f),
            };
            foreach (Vector3 spot in spots)
            {
                Geo.Box(stacks, "Pallet", spot + new Vector3(0f, 0.08f, 0f), new Vector3(1.2f, 0.15f, 1f), MaterialLibrary.Wood);
                int levels = 1 + random.Next(3);
                for (int level = 0; level < levels; level++)
                    Crate(stacks, spot + new Vector3(0f, 0.16f + level * 0.82f, 0f), random, 0.8f);
            }
        }

        static void Crate(Transform parent, Vector3 bottomCentre, System.Random random, float size = 0f)
        {
            float s = size > 0f ? size : 0.6f + (float)random.NextDouble() * 0.5f;
            bool wooden = random.NextDouble() < 0.5;
            var dimensions = new Vector3(s, s * 0.9f, s);
            Geo.Box(parent, wooden ? "Crate" : "Box", bottomCentre + Vector3.up * dimensions.y * 0.5f, dimensions,
                wooden ? MaterialLibrary.Wood : cardboard,
                Quaternion.Euler(0f, (float)(random.NextDouble() - 0.5) * 20f, 0f));
        }

        // ------------------------------------------------------------ services

        static void BuildPipes(Transform factory)
        {
            var pipes = Geo.Group(factory, "Pipes");
            var pipeMaterial = MaterialLibrary.Plain("PipeGrey", new Color(0.55f, 0.57f, 0.6f), 0.55f, 0.7f);
            Quaternion alongZ = Quaternion.Euler(90f, 0f, 0f);
            float length = ZEnd - ZStart - 1f;

            foreach (float x in new[] { HalfWidth - 0.7f, -HalfWidth + 0.7f })
            {
                Geo.Cylinder(pipes, "Pipe", new Vector3(x, 9.2f, 45f), 0.22f, length, pipeMaterial, alongZ);
                Geo.Cylinder(pipes, "Pipe", new Vector3(x, 9.8f, 45f), 0.14f, length, MaterialLibrary.RedPaintedMetal, alongZ);
                foreach (float z in new[] { 20f, 50f })
                    Geo.Cylinder(pipes, "Drop", new Vector3(x, 4.6f, z), 0.18f, 9.2f, pipeMaterial);
            }
        }

        static void BuildLamps(Transform factory)
        {
            var lamps = Geo.Group(factory, "Lamps");
            var housing = MaterialLibrary.Plain("LampHousing", new Color(0.12f, 0.12f, 0.13f), 0.4f, 0.5f);
            var glow = MaterialLibrary.Emissive("LampGlow", new Color(1f, 0.93f, 0.8f), 4f);

            for (float z = 20f; z <= 70f; z += 10f)
            foreach (float x in new[] { -5f, 5f })
            {
                var lamp = Geo.Group(lamps, "Lamp", new Vector3(x, 10.5f, z));
                Geo.Box(lamp, "Housing", Vector3.zero, new Vector3(0.6f, 0.15f, 1.6f), housing, collider: false);
                Geo.Box(lamp, "Panel", new Vector3(0f, -0.08f, 0f), new Vector3(0.5f, 0.02f, 1.5f), glow, collider: false);
                Geo.Box(lamp, "Cable", new Vector3(0f, 0.55f, 0f), new Vector3(0.02f, 1f, 0.02f), housing, collider: false);

                var light = new GameObject("Light").AddComponent<Light>();
                light.transform.SetParent(lamp, false);
                light.transform.localPosition = new Vector3(0f, -0.3f, 0f);
                light.type = LightType.Point;
                light.range = 16f;
                light.intensity = 2.2f;
                light.color = new Color(1f, 0.92f, 0.8f);
                light.shadows = LightShadows.None;
            }
        }

        // ------------------------------------------------------------ course

        /// <summary>The beginner course: in through the south door, under the mezzanine, out the north door.</summary>
        public static void BuildCourse(Transform parent)
        {
            var course = Geo.Group(parent, "Course");
            Gate(course, new Vector3(0f, 0f, 6f), 0f, 5f, 4f);
            Gate(course, new Vector3(0f, 0f, 32f), 0f, 4f, 3.5f);
            Gate(course, new Vector3(-12f, 0f, 58f), 0f, 4f, 3f);
            Gate(course, new Vector3(0f, 0f, 84f), 0f, 5f, 4f);

            Pad(course, "StartPad", new Vector3(0f, 0f, -5f), new Color(1f, 0.5f, 0.1f));
            Pad(course, "LandingPad", new Vector3(0f, 0f, 92f), new Color(0.2f, 0.75f, 0.3f));
        }

        static void Gate(Transform parent, Vector3 position, float yaw, float width, float height)
        {
            const float thickness = 0.22f;
            var gate = Geo.Group(parent, "Gate", position, Quaternion.Euler(0f, yaw, 0f));
            var paint = MaterialLibrary.RedPaintedMetal;
            var stripe = MaterialLibrary.Emissive("GateStripe", new Color(1f, 0.85f, 0.4f), 2.5f);

            Geo.Box(gate, "PostLeft", new Vector3(-width * 0.5f, height * 0.5f, 0f), new Vector3(thickness, height, thickness), paint);
            Geo.Box(gate, "PostRight", new Vector3(width * 0.5f, height * 0.5f, 0f), new Vector3(thickness, height, thickness), paint);
            Geo.Box(gate, "Top", new Vector3(0f, height, 0f), new Vector3(width + thickness, thickness, thickness), paint);
            Geo.Box(gate, "Stripe", new Vector3(0f, height - thickness * 0.5f - 0.02f, -thickness * 0.5f - 0.005f),
                new Vector3(width - 0.2f, 0.05f, 0.01f), stripe, collider: false);
        }

        static void Pad(Transform parent, string name, Vector3 position, Color colour)
        {
            var pad = Geo.Group(parent, name, position);
            var fill = MaterialLibrary.Plain(name + "Fill", colour, 0.35f);
            var ring = MaterialLibrary.Plain("PadRing", new Color(0.95f, 0.95f, 0.95f), 0.35f);
            // Stacked discs, each a few mm higher: coloured base, white ring, coloured centre.
            Geo.Cylinder(pad, "Base", new Vector3(0f, 0.09f, 0f), 1.4f, 0.1f, fill);
            Geo.Cylinder(pad, "Ring", new Vector3(0f, 0.092f, 0f), 1.1f, 0.1f, ring, collider: false);
            Geo.Cylinder(pad, "Centre", new Vector3(0f, 0.094f, 0f), 0.95f, 0.1f, fill, collider: false);
        }

        /// <summary>Height of the start pad's top surface, for spawning the drone.</summary>
        public const float PadTop = 0.144f;
        public static readonly Vector3 StartPadPosition = new Vector3(0f, 0f, -5f);
    }
}
