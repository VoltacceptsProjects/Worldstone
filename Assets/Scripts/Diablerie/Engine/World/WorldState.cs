using System.Collections.Generic;
using Diablerie.Engine.Entities;
using UnityEngine;

namespace Diablerie.Engine.World
{
    public class WorldState : MonoBehaviour
    {
        public static WorldState instance;
        // World size in map tiles. The collision map has Iso.SubTileCount x Iso.SubTileCount cells per tile
        // (plus a small border), so it has to be derived from the tile grid size.
        private const int GridSize = 1000;
        private const int CollisionMapSize = GridSize * Iso.SubTileCount + 16;

        private readonly CollisionMap _collisionMap = new CollisionMap(CollisionMapSize, CollisionMapSize);
        private readonly WorldGrid _grid = new WorldGrid(GridSize, GridSize);
        private readonly HashSet<Entity> _entities = new HashSet<Entity>();
        private readonly HashSet<Popup> _popups = new HashSet<Popup>();

        void Awake()
        {
            instance = this;
        }

        public void Add(Entity entity)
        {
            _entities.Add(entity);
        }

        public void Add(Popup popup)
        {
            _popups.Add(popup);
        }

        public void Remove(Entity entity)
        {
            _entities.Remove(entity);
        }

        public void Remove(Popup popup)
        {
            _popups.Remove(popup);
        }

        public CollisionMap CollisionMap => _collisionMap;

        public WorldGrid Grid => _grid;

        public HashSet<Entity> Entities => _entities;

        public HashSet<Popup> Popups => _popups;

        public Player Player { get; set; }
    }
}
