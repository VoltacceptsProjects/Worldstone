using Diablerie.Engine;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.Entities;
using Diablerie.Engine.SaveGame;
using Diablerie.Game.World;
using Diablerie.Engine.World;
using UnityEngine;

namespace Diablerie.Game.Coop
{
    // Owns the second player: joins and leaves, keeps both players on one screen and gets the fallen up again.
    // Added to the game scene by WorldBuilder.
    public class CoopManager : MonoBehaviour
    {
        public static CoopManager Instance { get; private set; }

        public const float ReviveDelay = 5f;
        private const float JoinHintSeconds = 20f;

        // The view is a fixed 800x600 pixels and the camera looks at the middle between the players. These are the
        // largest distances, measured in screen pixels, that still keep both players inside of the picture.
        // Soft: player 2 stops walking away. Hard: player 2 is brought back to player 1.
        private static readonly Vector2 SoftLimit = new Vector2(560f, 360f) / Iso.pixelsPerUnit;
        private static readonly Vector2 HardLimit = new Vector2(680f, 440f) / Iso.pixelsPerUnit;

        // Where a player appears next to the other one
        private static readonly Vector2 SideOffset = new Vector2(2f, -2f);

        private readonly float[] _deadSince = { -1f, -1f };
        private float _nextPullTime;
        private float _firstSeen = -1f;
        private CoopPlayerInput _player1Input;

        public bool ShowJoinHint =>
            WorldState.instance != null && WorldState.instance.Player2 == null &&
            _firstSeen >= 0f && Time.unscaledTime - _firstSeen < JoinHintSeconds;

        private void Awake()
        {
            Instance = this;
            gameObject.AddComponent<CoopHud>();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            var world = WorldState.instance;
            if (world == null || world.Player == null)
                return;

            if (_firstSeen < 0f)
                _firstSeen = Time.unscaledTime;

            if (_player1Input == null && world.Player.unit != null)
            {
                _player1Input = world.Player.gameObject.AddComponent<CoopPlayerInput>();
                _player1Input.InitMain(world.Player, 1);
            }

            if (world.Player2 == null)
            {
                bool loading = PlayerController.instance == null || !PlayerController.instance.enabled;
                if (loading || Time.timeScale <= 0f)
                    return;
                if (CoopSettings.Enabled || JoinPressed())
                    Join(null);
                return;
            }

            if (Input.GetKeyDown(KeyCode.F10))
            {
                Leave();
                return;
            }

            EnforceLeash(world);
            HandleDeaths(world);
        }

        private static bool JoinPressed()
        {
            return Input.GetKeyDown(KeyCode.F9) || CoopPads.ButtonDown(2, 7);
        }

        #region Join and leave

        // /coop in the command prompt
        public static void HandleCommand(string argument)
        {
            var manager = Instance;
            var world = WorldState.instance;
            if (manager == null || world == null || world.Player == null)
                return;

            if (string.IsNullOrEmpty(argument))
            {
                if (world.Player2 == null)
                    manager.Join(null);
                else
                    manager.Leave();
                return;
            }

            // A saved character wins over a class of the same name
            bool isSave = CoopSettings.ResolvePlayer2Save(world.Player.name, argument) != null;
            if (!isSave && CoopSettings.FindClass(argument) == null)
            {
                Debug.LogWarning("Unknown character or class " + argument);
                return;
            }

            if (world.Player2 != null)
                manager.Leave();
            manager.Join(argument);
        }

        // argument: the name of a saved character or a class for a new one, null for the remembered character
        private void Join(string argument)
        {
            var world = WorldState.instance;
            var player1 = world.Player;
            if (player1.unit == null)
                return;

            // The spawn tile is taken by player 1, InstantMove below finds a free spot next to it
            var spawn = Iso.Snap(player1.unit.iso.pos / Iso.SubTileCount);
            var save = CoopSettings.ResolvePlayer2Save(player1.name, argument);
            Player player2;
            if (save != null)
            {
                player2 = Player.CreateGuest(save, spawn);
            }
            else
            {
                string className = CoopSettings.FindClass(argument) ?? CoopSettings.ResolvePlayer2Class(player1.charStatInfo.className);
                player2 = Player.CreateGuest(className, CoopSettings.NewCharacterName(className), spawn);
                GiveItems(player2, "hp1", 4);
                GiveItems(player2, "mp1", 3);
            }

            world.Player2 = player2;
            player2.unit.InstantMove(player1.unit.iso.pos + SideOffset);
            CoopSettings.Player2Character = player2.name;

            var input = player2.gameObject.AddComponent<CoopPlayerInput>();
            input.Init(player2, 2, CoopSettings.LoadoutFor(player2.charStatInfo.className));

            _deadSince[0] = -1f;
            _deadSince[1] = -1f;
            CoopSettings.Enabled = true;
            SaveGuest(WorldBuilder.CurrentActNumber);
            Debug.Log("Player 2 joined as " + player2.name + " (" + player2.charStatInfo.className + ")");
        }

        // Player 2 is a real character, saved together with player 1
        public static void SaveGuest(int act)
        {
            var world = WorldState.instance;
            if (world == null || world.Player2 == null || world.Player2.unit == null)
                return;
            SaveManager.Save(world.Player2, act, guest: true);
        }

        private void Leave()
        {
            var world = WorldState.instance;
            var player2 = world.Player2;
            if (player2 == null)
                return;

            SaveGuest(WorldBuilder.CurrentActNumber);
            world.Player2 = null;
            CoopSettings.Enabled = false;
            if (player2.unit != null)
            {
                var unit = player2.unit;
                CollisionMap.SetPassable(Iso.Snap(unit.iso.pos), unit.size, unit.size, true, unit.gameObject);
            }
            Destroy(player2.gameObject);
            Debug.Log("Player 2 left");
        }

        private static void GiveItems(Player player, string code, int count)
        {
            var info = ItemInfo.Find(code);
            if (info == null)
                return;
            for (int i = 0; i < count; ++i)
                player.Take(new Item(info));
        }

        #endregion

        #region One screen for two players

        // Neither player walks off the screen: walking further away from the other one is simply not possible
        public Vector2 ClampMovementToLeash(Player mover, Vector2 worldDirection)
        {
            var world = WorldState.instance;
            if (world == null || world.Player == null || world.Player2 == null)
                return worldDirection;

            var other = mover == world.Player ? world.Player2 : world.Player;
            Vector3 apart = mover.transform.position - other.transform.position;
            if (Mathf.Abs(apart.x) >= SoftLimit.x && worldDirection.x * apart.x > 0f)
                worldDirection.x = 0f;
            if (Mathf.Abs(apart.y) >= SoftLimit.y && worldDirection.y * apart.y > 0f)
                worldDirection.y = 0f;
            return worldDirection;
        }

        // Player 1 can't be stopped (mouse click movement, doors, waypoints), so when the two end up too far
        // apart player 2 is simply brought along
        private void EnforceLeash(WorldState world)
        {
            var player1 = world.Player;
            var player2 = world.Player2;
            if (player1.unit == null || player2.unit == null)
                return;
            // A fallen player comes back next to the one who is still alive, see HandleDeaths
            if (player1.unit.IsDead || player2.unit.IsDead)
                return;

            Vector3 apart = player2.transform.position - player1.transform.position;
            if (Mathf.Abs(apart.x) <= HardLimit.x && Mathf.Abs(apart.y) <= HardLimit.y)
                return;
            if (Time.time < _nextPullTime)
                return;

            _nextPullTime = Time.time + 0.5f;
            player2.unit.StopMoving();
            player2.unit.InstantMove(player1.unit.iso.pos + SideOffset);
        }

        #endregion

        #region Death

        // Seconds until the player is back on his feet, or -1 while alive
        public float ReviveCountdown(Player player)
        {
            var world = WorldState.instance;
            if (world == null)
                return -1f;
            int index = world.Players.IndexOf(player);
            if (index < 0 || index >= _deadSince.Length || _deadSince[index] < 0f)
                return -1f;
            return Mathf.Max(0f, ReviveDelay - (Time.time - _deadSince[index]));
        }

        private void HandleDeaths(WorldState world)
        {
            var players = world.Players;
            if (players.Count < 2)
                return;

            for (int i = 0; i < 2; ++i)
            {
                var player = players[i];
                if (player.unit == null)
                    continue;

                if (!player.unit.IsDead)
                {
                    _deadSince[i] = -1f;
                    continue;
                }

                if (_deadSince[i] < 0f)
                    _deadSince[i] = Time.time;

                if (Time.time - _deadSince[i] >= ReviveDelay)
                {
                    Revive(player, players[1 - i]);
                    _deadSince[i] = -1f;
                }
            }
        }

        // The partner pulls the fallen one up: half of the life, right next to him. When both are down
        // everybody gets up where they fell, with full life.
        private static void Revive(Player player, Player partner)
        {
            bool partnerAlive = partner.unit != null && !partner.unit.IsDead;
            player.unit.Revive(partnerAlive ? 0.5f : 1f);
            if (partnerAlive)
                player.unit.InstantMove(partner.unit.iso.pos + SideOffset);
        }

        #endregion
    }
}
