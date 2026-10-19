using System;
using System.Collections;
using System.Collections.Generic;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.Utility;
using UnityEngine;

namespace Diablerie.Engine.World
{
    // Builds levels only when they are needed instead of all at once when an act is loaded.
    //  - "Proximity" levels (outdoor maps that touch each other) are built when the camera gets close
    //    to their footprint, so neighbouring maps are already there before the player walks into them.
    //  - Other levels (dungeons) are built on demand, when something asks for them (a warp,
    //    a waypoint, a portal), because they are packed close together and are only entered through those.
    // Built levels stay in the world, they are not unloaded.
    public class LevelStreamer : MonoBehaviour
    {
        public class Slot
        {
            public string name;
            public int levelId;
            public Vector2i offset;
            public Vector2i size;
            public bool proximity;
            public Func<LevelBuilder> create;
            public LevelBuilder builder;
            public GameObject instance;
            public bool loaded;
            internal IEnumerator routine;

            public bool Loaded => loaded;
            public bool Loading => routine != null && !loaded;

            public LevelBuilder Builder
            {
                get
                {
                    if (builder == null)
                        builder = create();
                    return builder;
                }
            }
        }

        // Distance in tiles from the camera to a level's footprint at which the level gets built
        public const int LoadMargin = 48;
        private const float CheckInterval = 0.2f;

        public static LevelStreamer Current { get; private set; }

        private readonly List<Slot> slots = new List<Slot>();
        private readonly Dictionary<int, Slot> slotsById = new Dictionary<int, Slot>();
        private float nextCheck;
        private Camera cachedCamera;

        private void Awake()
        {
            Current = this;
        }

        private void OnDestroy()
        {
            // The previous act is destroyed after the next one is created, do not clear the new one
            if (Current == this)
                Current = null;
        }

        public Slot Add(string levelName, Vector2i offset, Func<LevelBuilder> create, bool proximity)
        {
            var info = LevelInfo.Find(levelName);
            var slot = new Slot
            {
                name = levelName,
                levelId = info.id,
                offset = offset,
                size = LevelBuilder.Footprint(info),
                proximity = proximity,
                create = create
            };
            slots.Add(slot);
            slotsById[slot.levelId] = slot;
            return slot;
        }

        // Level that covers a map tile, 0 when the tile belongs to no built level
        public int LevelIdAt(int x, int y)
        {
            foreach (var slot in slots)
            {
                if (slot.Loaded && x >= slot.offset.x && x < slot.offset.x + slot.size.x &&
                    y >= slot.offset.y && y < slot.offset.y + slot.size.y)
                    return slot.levelId;
            }
            return 0;
        }

        public bool Has(int levelId)
        {
            return slotsById.ContainsKey(levelId);
        }

        public bool EnsureLoaded(int levelId)
        {
            return slotsById.TryGetValue(levelId, out var slot) && Load(slot);
        }

        // Milliseconds per frame spent on streaming a level in
        public const double FrameBudgetMs = 3.0;

        private Slot current;

        // Builds a level right now, finishing it in this frame (warps, waypoints, arrival level)
        public bool Load(Slot slot)
        {
            if (slot.Loaded)
                return true;

            if (slot.routine == null)
                slot.routine = LoadRoutine(slot);

            TimeSlice.End();
            while (slot.routine.MoveNext()) { }
            if (current == slot)
                current = null;
            return true;
        }

        private IEnumerator LoadRoutine(Slot slot)
        {
            yield return null;

            var builder = slot.Builder;
            yield return null;

            var prefetch = builder.PrefetchTiles();
            while (prefetch.MoveNext())
                yield return null;

            var levelObject = builder.BeginInstantiate(slot.offset);
            levelObject.transform.SetParent(transform);
            slot.instance = levelObject;
            yield return null;

            var content = builder.InstantiateContent(slot.offset, levelObject.transform);
            while (content.MoveNext())
            {
                if (TimeSlice.Exceeded)
                    yield return null;
            }

            slot.loaded = true;

            // Tiles of this level are created by the renderer in small slices over the next frames
            if (WorldRenderer.Instance != null)
                WorldRenderer.Instance.AddRegion(new RectInt(slot.offset.x, slot.offset.y, slot.size.x + 2, slot.size.y + 2));

            var player = WorldState.instance != null ? WorldState.instance.Player : null;
            if (player != null)
                Waypoints.SyncVisuals(player);
        }

        // Builds every proximity level close to a map tile position right now
        public void LoadNear(Vector2 tilePos)
        {
            foreach (var slot in slots)
            {
                if (slot.proximity && !slot.Loaded && Distance(slot, tilePos) <= LoadMargin)
                    Load(slot);
            }
        }

        private void Update()
        {
            // Continue the level that is being streamed in, within the frame budget
            if (current != null)
            {
                TimeSlice.Begin(FrameBudgetMs);
                try
                {
                    bool more = current.routine.MoveNext();
                    while (more && !TimeSlice.Exceeded)
                        more = current.routine.MoveNext();
                    if (!more)
                        current = null;
                }
                finally
                {
                    TimeSlice.End();
                }
                return;
            }

            if (Time.unscaledTime < nextCheck)
                return;
            nextCheck = Time.unscaledTime + CheckInterval;

            // Do not start another level while the previous one is still creating its tiles
            if (WorldRenderer.Instance != null && WorldRenderer.Instance.Busy)
                return;

            if (cachedCamera == null)
                cachedCamera = Camera.main;
            var camera = cachedCamera;
            if (camera == null)
                return;

            var tilePos = (Vector2)Iso.MapToIso(camera.transform.position) / Iso.SubTileCount;

            // Closest waiting level first
            Slot best = null;
            float bestDistance = float.MaxValue;
            foreach (var slot in slots)
            {
                if (!slot.proximity || slot.Loaded || slot.Loading)
                    continue;
                float distance = Distance(slot, tilePos);
                if (distance <= LoadMargin && distance < bestDistance)
                {
                    best = slot;
                    bestDistance = distance;
                }
            }

            if (best != null)
            {
                best.routine = LoadRoutine(best);
                current = best;
            }
        }

        private static float Distance(Slot slot, Vector2 tilePos)
        {
            float dx = Mathf.Max(slot.offset.x - tilePos.x, 0, tilePos.x - (slot.offset.x + slot.size.x));
            float dy = Mathf.Max(slot.offset.y - tilePos.y, 0, tilePos.y - (slot.offset.y + slot.size.y));
            return Mathf.Max(dx, dy);
        }
    }
}
