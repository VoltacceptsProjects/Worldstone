using System.Collections.Generic;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.Entities;
using UnityEngine;

namespace Diablerie.Engine.World
{
    public class Warp : Entity
    {
        LevelWarpInfo info;
        LevelInfo sourceLevel;
        LevelInfo targetLevel;
        new Transform transform;
        Vector3 selectOffset;
        Vector3 selectSize;

        // LevelWarpInfo is shared by every level that uses the same warp art, so the warp a level
        // leads to is looked up by (level the warp is in, level it leads to) instead
        static readonly Dictionary<(int from, int to), Warp> registry = new Dictionary<(int from, int to), Warp>();

        public static void ClearRegistry()
        {
            registry.Clear();
        }

        public static Warp Create(int x, int y, LevelWarpInfo warpInfo, LevelInfo sourceLevel, LevelInfo targetLevel, Transform parent)
        {
            var offset = new Vector3(warpInfo.offsetX, warpInfo.offsetY);
            var pos = new Vector3(x, y) * Iso.SubTileCount - new Vector3(2, 2) + offset;
            pos = Iso.MapToWorld(pos);

            var warpObject = new GameObject(targetLevel.levelWarp);
            warpObject.transform.position = pos;
            warpObject.transform.SetParent(parent);
            var warp = warpObject.AddComponent<Warp>();
            warp.sourceLevel = sourceLevel;
            warp.targetLevel = targetLevel;
            warp.info = warpInfo;
            warp.transform = warpObject.transform;
            warp.selectSize = new Vector3(warpInfo.selectDX, warpInfo.selectDY) / Iso.pixelsPerUnit;
            warp.selectOffset = new Vector3(warpInfo.selectX, -warpInfo.selectY) / Iso.pixelsPerUnit;
            warp.selectOffset += new Vector3(warp.selectSize.x, -warp.selectSize.y) / 2;
            warpInfo.instance = warp;
            registry[(sourceLevel.id, targetLevel.id)] = warp;
            return warp;
        }

        public override Bounds bounds
        {
            get
            {
                return new Bounds(transform.position + selectOffset, selectSize);
            }
        }

        public override bool selected
        {
            get { return false; }
            set { }
        }

        public override Vector2 titleOffset
        {
            get { return new Vector2(info.selectX + info.selectDX / 2, info.selectDY / 2); }
        }

        private Warp FindTargetWarp()
        {
            // The warp in the target level that leads back to the level this one is in
            if (registry.TryGetValue((targetLevel.id, sourceLevel.id), out var warp) && warp != null)
                return warp;

            return null;
        }

        public override void Operate(Unit unit)
        {
            var targetWarp = FindTargetWarp();
            if (targetWarp == null && LevelStreamer.Current != null && LevelStreamer.Current.EnsureLoaded(targetLevel.id))
            {
                // The target level is built on demand, the first time something leads into it
                targetWarp = FindTargetWarp();
            }

            if (targetWarp == null)
            {
                Debug.LogError("Target warp wasn't found");
                return;
            }

            ScreenFader.SetToBlack();
            ScreenFader.FadeToClear();
            var target = Iso.MapToIso(targetWarp.transform.position);
            unit.InstantMove(target);
            unit.GoTo(target + new Vector3(targetWarp.info.exitWalkX, targetWarp.info.exitWalkY));
        }
    }
}
