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

        private Player _player;
        private Player _player2;
        private readonly List<Player> _players = new List<Player>(2);

        // The main player: mouse and keyboard, owns the UI, the quests and the save game
        public Player Player
        {
            get => _player;
            set { _player = value; RebuildPlayers(); }
        }

        // Couch co-op guest, null while playing alone
        public Player Player2
        {
            get => _player2;
            set { _player2 = value; RebuildPlayers(); }
        }

        // Everyone who is playing, the main player first. Read only, do not modify.
        public List<Player> Players => _players;

        public bool IsPlayerUnit(Entity entity)
        {
            for (int i = 0; i < _players.Count; ++i)
            {
                if (_players[i].unit == entity)
                    return true;
            }
            return false;
        }

        public Player PlayerOf(Unit unit)
        {
            if (unit == null)
                return null;
            for (int i = 0; i < _players.Count; ++i)
            {
                if (_players[i].unit == unit)
                    return _players[i];
            }
            return null;
        }

        private void RebuildPlayers()
        {
            _players.Clear();
            if (_player != null)
                _players.Add(_player);
            if (_player2 != null)
                _players.Add(_player2);
        }
    }
}
