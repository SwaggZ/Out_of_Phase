using UnityEngine;
using OutOfPhase.Items;
using OutOfPhase.Items.ToolActions;
using OutOfPhase.Dimension;

namespace OutOfPhase.Interaction
{
    /// <summary>
    /// Dig spot that requires a shovel to reveal loot or a path.
    /// </summary>
    public class DigSpot : MonoBehaviour, IToolTarget
    {
        [System.Serializable]
        public struct LootEntry
        {
            public ItemDefinition Item;
            public int Quantity;
            public GameObject DropPrefabOverride;
        }

        [Header("Dig Settings")]
        [SerializeField] private float requiredProgress = 3f;
        [SerializeField] private bool destroyOnComplete = true;

        [Header("Loot")]
        [SerializeField] private LootEntry[] loot;

        [Header("Reveal")]
        [SerializeField] private GameObject revealOnComplete;

        [Header("VFX/SFX")]
        [SerializeField] private GameObject digCompleteVFX;
        [SerializeField] private AudioClip digCompleteSound;

        private float _progress;
        private bool _completed;

        public bool AcceptsToolAction(System.Type actionType)
        {
            return actionType == typeof(ShovelAction);
        }

        public bool ReceiveToolAction(ToolAction action, ToolUseContext context)
        {
            if (_completed) return false;

            var shovel = action as ShovelAction;
            if (shovel == null) return false;

            _progress += shovel.DigPower;

            if (_progress >= requiredProgress)
            {
                CompleteDig();
            }

            return true;
        }

        private void CompleteDig()
        {
            _completed = true;

            SpawnLoot();

            if (revealOnComplete != null)
                revealOnComplete.SetActive(true);

            if (digCompleteVFX != null)
            {
                var vfx = Instantiate(digCompleteVFX, transform.position, Quaternion.identity);
                Destroy(vfx, 2f);
            }

            if (digCompleteSound != null)
                SFXPlayer.PlayAtPoint(digCompleteSound, transform.position);

            if (destroyOnComplete)
                Destroy(gameObject);
            else
                gameObject.SetActive(false);
        }

        private void SpawnLoot()
        {
            if (loot == null) return;

            foreach (var entry in loot)
            {
                if (entry.Item == null || entry.Quantity <= 0) continue;

                GameObject prefab = entry.DropPrefabOverride != null
                    ? entry.DropPrefabOverride
                    : entry.Item.WorldPrefab;

                if (prefab == null) continue;

                var instance = Instantiate(prefab, transform.position + Vector3.up * 0.2f, Quaternion.identity);
                var pickup = instance.GetComponent<ItemPickup>();
                if (pickup == null)
                {
                    pickup = instance.AddComponent<ItemPickup>();
                }
                pickup.SetItem(entry.Item, entry.Quantity);
            }
        }
    }
}
