using System.Collections;
using Diablerie.Engine;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.Entities;
using Diablerie.Engine.IO.D2Formats;
using Diablerie.Engine.Quest;
using Diablerie.Engine.SaveGame;
using Diablerie.Engine.World;
using Diablerie.Game.AI;
using Diablerie.Game.UI;
using UnityEngine;

namespace Diablerie.Game.World
{
    public class WorldBuilder : MonoBehaviour
    {
        public static string className = "Sorceress";
        public static string characterName;

        // Set by the menu when continuing an existing character, consumed on first act load
        public static CharacterSave pendingSave;
        
        private Act _currentAct;
        private int _currentActNumber = 1;
        private Waypoints.Entry _pendingWaypoint;
        private static WorldBuilder instance;

        public static int CurrentActNumber => instance != null ? instance._currentActNumber : 1;

        private void Awake()
        {
            instance = this;
            QuestManager.EnsureExists();
            if (GetComponent<Diablerie.Engine.Automap>() == null)
                gameObject.AddComponent<Diablerie.Engine.Automap>();
            if (GetComponent<UnitActivity>() == null)
                gameObject.AddComponent<UnitActivity>();
            if (GetComponent<Act1QuestSetup>() == null)
                gameObject.AddComponent<Act1QuestSetup>();
            if (GetComponent<Diablerie.Game.Coop.CoopManager>() == null)
                gameObject.AddComponent<Diablerie.Game.Coop.CoopManager>();
        }

        private void Start()
        {
            StartCoroutine(LoadActCoroutine(pendingSave != null ? pendingSave.act : 1));
        }

        private IEnumerator LoadActCoroutine(int actNumber)
        {
            PlayerController.instance.enabled = false;
            MouseSelection.instance.enabled = false;
            DCC.Unthrottled = true;
            LoadingScreen.Show(0.5f);  // It's not zero due to preceding Unity scene load
            ScreenFader.SetToBlack();
            yield return null;
            
            if (_currentAct != null)
                Destroy(_currentAct.root);
            Waypoints.ClearWorld();
            _currentAct = CreateAct(actNumber);
            _currentActNumber = actNumber;
            
            LoadingScreen.Show(0.75f);
            yield return null;

            if (WorldState.instance.Player == null)
            {
                var player = pendingSave != null
                    ? new Player(pendingSave, _currentAct.entry)
                    : new Player(className, string.IsNullOrEmpty(characterName) ? className : characterName, _currentAct.entry);
                WorldState.instance.Player = player;
                PlayerController.instance.SetPlayer(player);
                if (pendingSave != null)
                    PlayerController.instance.ReadSkills(pendingSave);
                pendingSave = null;
            }
            else
            {
                var playerUnit = WorldState.instance.Player.unit;
                Vector3 arrival = Iso.MapToIso(Iso.MapTileToWorld(_currentAct.entry));
                if (_pendingWaypoint != null && Waypoints.TryGetArrival(_pendingWaypoint, playerUnit.size, out var waypointArrival))
                    arrival = waypointArrival;
                playerUnit.InstantMove(arrival);
            }
            _pendingWaypoint = null;
            Waypoints.SyncVisuals(WorldState.instance.Player);

            LoadingScreen.Show(0.9f);
            yield return null; // Workaround to load first DCC while screen is black to avoid visible spikes
            
            LoadingScreen.Show(1.0f);
            yield return null;

            SaveCurrentPlayer();
            
            PlayerController.instance.enabled = true;
            MouseSelection.instance.enabled = true;
            LoadingScreen.Hide();
            ScreenFader.FadeToClear();
            DCC.Unthrottled = false;
        }

        public static void SaveCurrentPlayer()
        {
            if (instance == null)
                return;
            SaveManager.Save(WorldState.instance.Player, instance._currentActNumber);
        }

        static Act CreateAct(int actNumber)
        {
            WorldState.instance.Grid.Reset();
            Warp.ClearRegistry();
            if (actNumber == 1)
            {
                return new Act1();
            }
            if (actNumber == 2)
            {
                return new Act2();
            }
            if (actNumber == 3)
            {
                return new Act3();
            }
            if (actNumber == 4)
            {
                return new Act4();
            }
            if (actNumber == 5)
            {
                return new Act5();
            }

            return new Act1();
        }

        public static void TravelToWaypoint(Player player, Waypoints.Entry destination)
        {
            if (instance == null)
                return;

            int destinationAct = destination.act + 1;
            if (destinationAct == instance._currentActNumber)
            {
                if (!Waypoints.TryGetArrival(destination, player.unit.size, out var arrival))
                {
                    Debug.LogWarning("Waypoint " + destination.name + " is not present in the current world");
                    return;
                }

                ScreenFader.SetToBlack();
                ScreenFader.FadeToClear();
                player.unit.InstantMove(arrival);
                SaveCurrentPlayer();
                return;
            }

            instance._pendingWaypoint = destination;
            instance.StartCoroutine(instance.LoadActCoroutine(destinationAct));
        }

        public static void LoadAct(int actNumber)
        {
            AudioManager.instance.Play("player_townportal_enter");
            instance.StartCoroutine(instance.LoadActCoroutine(actNumber));
        }

        public static Unit SpawnMonster(string id, Vector3 pos, Transform parent = null, Unit summoner = null)
        {
            MonStat monStat = MonStat.Find(id);
            if (monStat == null)
            {
                Debug.LogWarning("Monster id not found: " + id);
                return null;
            }
            return SpawnMonster(monStat, pos, parent, summoner);
        }

        public static Unit SpawnMonster(MonStat monStat, Vector3 pos, Transform parent = null, Unit summoner = null)
        {
            pos = Iso.MapToIso(pos);
            if (!CollisionMap.Fit(pos, out pos, monStat.ext.sizeX))
            {
                return null;
            }

            bool hostile = summoner == null &&
                           !(monStat.ai == "Npc" || monStat.ai == "Towner" || monStat.ai == "Vendor" || monStat.ai == "Hireable") &&
                           monStat.ai != "Idle" && monStat.ai != "NpcStationary";

            var monster = new GameObject(monStat.nameStr);
            monster.transform.SetParent(parent);
            monster.transform.position = Iso.MapToWorld(pos);

            // Monsters created far from the player (streamed levels) stay inactive until the player gets close,
            // so they cost nothing at all in the meantime. UnitActivity wakes them up.
            if (hostile && UnitActivity.IsFar(pos))
                monster.SetActive(false);
        
            CollisionMap.Move(pos, pos, monStat.ext.sizeX, monster);

            var unit = monster.AddComponent<Unit>();
            unit.monStat = monStat;
            unit.title = monStat.name;
            unit.basePath = @"data\global\monsters";
            unit.token = monStat.code;
            unit.weaponClass = monStat.ext.baseWeaponClass;
            unit.run = false;
            unit.walkSpeed = monStat.speed;
            unit.runSpeed = monStat.runSpeed;
            unit.size = monStat.ext.sizeX;
            unit.killable = monStat.killable;

            var monLvl = MonLvl.Find(monStat.level[0]);
            if (monLvl != null && !monStat.noRatio)
                unit.health = Random.Range(monLvl.hp[0] * monStat.stats[0].minHP, monLvl.hp[0] * monStat.stats[0].maxHP + 1) / 100;
            else
                unit.health = Random.Range(monStat.stats[0].minHP, monStat.stats[0].maxHP + 1);
            unit.maxHealth = unit.health;

            monster.AddComponent<UnitRenderer>();
            var renderer = unit.GetComponent<COFRenderer>();
            renderer.equip = new string[monStat.ext.gearVariants.Length];
            for (int i = 0; i < renderer.equip.Length; ++i)
            {
                var variants = monStat.ext.gearVariants[i];
                if (variants == null)
                    continue;
                renderer.equip[i] = variants[Random.Range(0, variants.Length)];
            }

            if (summoner != null)
            {
                unit.party = summoner.party;
                var petController = monster.AddComponent<PetController>();
                petController.owner = summoner;
            }
            else if (monStat.ai == "Npc" || monStat.ai == "Towner" || monStat.ai == "Vendor" || monStat.ai == "Hireable")
            {
                unit.party = Party.Good;
                monster.AddComponent<NpcController>();
            }
            else if (monStat.ai != "Idle" && monStat.ai != "NpcStationary")
            {
                unit.party = Party.Evil;
                monster.AddComponent<MonsterController>();
            }

            if (hostile)
                UnitActivity.Register(unit);

            var body = monster.AddComponent<Rigidbody2D>();
            body.isKinematic = true;
            var collider = monster.AddComponent<CircleCollider2D>();
            collider.radius = monStat.ext.sizeX * Iso.tileSizeY;

            return unit;
        }

        public static StaticObject SpawnObject(ObjectInfo objectInfo, Vector3 pos, bool fit = false, Transform parent = null)
        {
            if (fit)
            {
                pos = Iso.MapToIso(pos);
                if (!CollisionMap.Fit(pos, out pos, objectInfo.sizeX))
                {
                    return null;
                }
                pos = Iso.MapToWorld(pos);
            }

            var gameObject = new GameObject(objectInfo.description);
            gameObject.transform.position = pos;

            var staticObject = gameObject.AddComponent<StaticObject>();
            staticObject.objectInfo = objectInfo;
            staticObject.title = objectInfo.name;

            gameObject.transform.SetParent(parent, true);

            if (objectInfo.draw)
                gameObject.AddComponent<StaticObjectRenderer>();

            if (objectInfo.operateFn == Waypoints.OperateFn)
                Waypoints.Register(staticObject);

            return staticObject;
        }

        public static StaticObject SpawnObject(string token, Vector3 worldPos, bool fit = false)
        {
            ObjectInfo objectInfo = ObjectInfo.Find(token);
            if (objectInfo == null)
            {
                Debug.LogWarning("ObjectInfo with token'" + token + "' not found");
                return null;
            }
            return SpawnObject(objectInfo, worldPos, fit: fit);
        }
    }
}
