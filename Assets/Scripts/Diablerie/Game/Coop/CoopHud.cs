using Diablerie.Engine;
using Diablerie.Engine.World;
using UnityEngine;

namespace Diablerie.Game.Coop
{
    // Everything co-op that is drawn on top of the game: a small life bar and name over both players, the
    // life and mana of player 2 in the corner (the bulbs of the control panel belong to player 1),
    // the countdown for a fallen player and the invitation to join.
    public class CoopHud : MonoBehaviour
    {
        private static readonly Color Player1Color = new Color(1f, 0.82f, 0.3f);
        private static readonly Color Player2Color = new Color(0.35f, 0.8f, 1f);
        private static readonly Color Life = new Color(0.8f, 0.1f, 0.1f);
        private static readonly Color Mana = new Color(0.15f, 0.3f, 0.9f);
        private static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.65f);

        private GUIStyle _label;

        private void OnGUI()
        {
            var world = WorldState.instance;
            if (world == null || world.Player == null || Camera.main == null)
                return;

            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    wordWrap = false
                };
            }

            // Same scale as the 800x600 view of the game, so everything keeps its size relative to the picture
            float s = CameraController.UiScale;
            _label.fontSize = Mathf.Max(10, Mathf.RoundToInt(12f * s));

            if (world.Player2 == null)
            {
                if (CoopManager.Instance != null && CoopManager.Instance.ShowJoinHint)
                    DrawJoinHint(s);
                return;
            }

            DrawOverhead(world.Player, "P1", Player1Color, s);
            DrawOverhead(world.Player2, "P2", Player2Color, s);
            DrawPlayer2Panel(world.Player2, s);
            DrawReviveMessages(world, s);
        }

        // Top left corner of the 800x600 view, in window pixels
        private static Vector2 ViewOrigin(float s)
        {
            return new Vector2((Screen.width - CameraController.VirtualWidth * s) / 2f,
                (Screen.height - CameraController.VirtualHeight * s) / 2f);
        }

        private void DrawJoinHint(float s)
        {
            var origin = ViewOrigin(s);
            var rect = new Rect(origin.x, origin.y + 6f * s, CameraController.VirtualWidth * s, 20f * s);
            DrawLabel(rect, "Player 2: press START on controller 2 (or F9) to join", Color.white);
        }

        private void DrawOverhead(Player player, string tag, Color color, float s)
        {
            if (player.unit == null || player.unit.IsDead)
                return;

            Vector3 screen = Camera.main.WorldToScreenPoint(player.transform.position);
            float top = Screen.height - screen.y - player.unit.titleOffset.y * s;
            float width = 44f * s;
            float height = 4f * s;
            var bar = new Rect(screen.x - width / 2f, top - height - 2f * s, width, height);

            FillRect(bar, Backdrop);
            FillRect(new Rect(bar.x, bar.y, bar.width * LifeFraction(player), bar.height), color);
            DrawLabel(new Rect(bar.x - width / 2f, bar.y - 16f * s, width * 2f, 16f * s), tag, color);
        }

        private void DrawPlayer2Panel(Player player, float s)
        {
            if (player.unit == null)
                return;

            var origin = ViewOrigin(s);
            float width = 150f * s;
            float x = origin.x + (CameraController.VirtualWidth - 158f) * s;
            float y = origin.y + 8f * s;

            FillRect(new Rect(x - 4f * s, y - 2f * s, width + 8f * s, 50f * s), Backdrop);
            DrawLabel(new Rect(x, y, width, 16f * s),
                "P2  " + player.charStatInfo.className + "  Lv " + player.charStat.level, Player2Color);

            var lifeBar = new Rect(x, y + 20f * s, width, 9f * s);
            FillRect(lifeBar, Backdrop);
            FillRect(new Rect(lifeBar.x, lifeBar.y, lifeBar.width * LifeFraction(player), lifeBar.height), Life);

            var manaBar = new Rect(x, y + 33f * s, width, 9f * s);
            FillRect(manaBar, Backdrop);
            float mana = Mathf.Clamp01((float) player.unit.mana / Mathf.Max(1, player.unit.maxMana));
            FillRect(new Rect(manaBar.x, manaBar.y, manaBar.width * mana, manaBar.height), Mana);
        }

        private void DrawReviveMessages(WorldState world, float s)
        {
            if (CoopManager.Instance == null)
                return;

            var origin = ViewOrigin(s);
            float y = origin.y + 40f * s;
            for (int i = 0; i < world.Players.Count; ++i)
            {
                float seconds = CoopManager.Instance.ReviveCountdown(world.Players[i]);
                if (seconds < 0f)
                    continue;

                var rect = new Rect(origin.x, y, CameraController.VirtualWidth * s, 24f * s);
                DrawLabel(rect, "P" + (i + 1) + " is down - back in " + Mathf.CeilToInt(seconds), i == 0 ? Player1Color : Player2Color);
                y += 22f * s;
            }
        }

        private static float LifeFraction(Player player)
        {
            return Mathf.Clamp01((float) player.unit.health / Mathf.Max(1, player.unit.maxHealth));
        }

        private static void FillRect(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        // Text with a one pixel shadow, readable on any ground
        private void DrawLabel(Rect rect, string text, Color color)
        {
            var shadow = rect;
            shadow.x += 1f;
            shadow.y += 1f;
            _label.normal.textColor = Color.black;
            GUI.Label(shadow, text, _label);
            _label.normal.textColor = color;
            GUI.Label(rect, text, _label);
        }
    }
}
