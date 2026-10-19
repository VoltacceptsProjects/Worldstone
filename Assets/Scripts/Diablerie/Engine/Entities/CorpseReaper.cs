using System.Collections.Generic;
using UnityEngine;

namespace Diablerie.Engine.Entities
{
    // Dead units stay on screen for a while, but not forever: the oldest corpses are removed
    // once there are too many or they are old enough, so they do not pile up over a long session.
    public class CorpseReaper : MonoBehaviour
    {
        private const int MaxCorpses = 40;
        private const float MaxAge = 120f;
        private const float CheckInterval = 5f;

        private static CorpseReaper instance;
        private struct Corpse { public Unit unit; public float time; }
        private readonly Queue<Corpse> corpses = new Queue<Corpse>();
        private float nextCheck;

        public static void Add(Unit unit)
        {
            if (instance == null)
            {
                var go = new GameObject("CorpseReaper");
                instance = go.AddComponent<CorpseReaper>();
            }
            instance.corpses.Enqueue(new Corpse { unit = unit, time = Time.time });
            instance.Trim();
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void Update()
        {
            if (Time.time < nextCheck)
                return;
            nextCheck = Time.time + CheckInterval;
            Trim();
        }

        private void Trim()
        {
            while (corpses.Count > 0)
            {
                var oldest = corpses.Peek();
                bool gone = oldest.unit == null;
                bool tooMany = corpses.Count > MaxCorpses;
                bool tooOld = Time.time - oldest.time > MaxAge;
                if (!gone && !tooMany && !tooOld)
                    break;
                corpses.Dequeue();
                if (!gone)
                    Destroy(oldest.unit.gameObject);
            }
        }
    }
}
