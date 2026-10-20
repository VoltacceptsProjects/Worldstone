using Diablerie.Engine.Datasheets;
using Diablerie.Engine.UI;
using UnityEngine;

namespace Diablerie.Engine
{
    public class Level : MonoBehaviour
    {
        public static LevelInfo Current => _current;
        
        private static LevelInfo _current;

        public static void ResetCurrent()
        {
            _current = null;
        }

        public LevelInfo info;

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.gameObject.tag != "Player")
                return;

            // The level title and music follow the main player, the co-op guest walks along
            var mainPlayer = Diablerie.Engine.World.WorldState.instance != null
                ? Diablerie.Engine.World.WorldState.instance.Player
                : null;
            if (mainPlayer != null && collision.gameObject != mainPlayer.gameObject)
                return;

            if (_current != null)
                LevelEntryTitle.Show("Entering " + info.levelName);

            var previous = _current;
            _current = info;
            Events.InvokeLevelChanged(_current, previous);
        }
    }
}
