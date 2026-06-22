using System.Collections.Generic;
using UnityEngine;

namespace NetTank
{
    /// <summary>
    /// Collects scene spawn points and returns them in a loop for new players and respawns.
    /// </summary>
    public sealed class SpawnPointGroup : MonoBehaviour
    {
        public static SpawnPointGroup Instance { get; private set; }

        [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

        public int Count
        {
            get
            {
                RefreshIfEmpty();
                return spawnPoints.Count;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("More than one SpawnPointGroup exists. The newest one will be used.");
            }

            Instance = this;
            RefreshIfEmpty();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnValidate()
        {
            RefreshFromChildren();
        }

        public Transform GetSpawnPoint(int index)
        {
            RefreshIfEmpty();

            if (spawnPoints.Count == 0)
            {
                return transform;
            }

            int wrappedIndex = Mathf.Abs(index) % spawnPoints.Count;
            return spawnPoints[wrappedIndex];
        }

        public Transform GetNextSpawnPoint(ref int index)
        {
            Transform point = GetSpawnPoint(index);
            index++;
            return point;
        }

        private void RefreshIfEmpty()
        {
            if (spawnPoints == null)
            {
                spawnPoints = new List<Transform>();
            }

            if (spawnPoints.Count == 0)
            {
                RefreshFromChildren();
            }
        }

        private void RefreshFromChildren()
        {
            if (spawnPoints == null)
            {
                spawnPoints = new List<Transform>();
            }

            spawnPoints.Clear();

            foreach (Transform child in transform)
            {
                if (child != null)
                {
                    spawnPoints.Add(child);
                }
            }
        }
    }
}
