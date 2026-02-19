using UnityEngine;
using OutOfPhase.Items;
using OutOfPhase.Items.ToolActions;
using OutOfPhase.Dimension;

namespace OutOfPhase.Interaction
{
    /// <summary>
    /// Mineable rock that requires a pickaxe and drops items when broken.
    /// </summary>
    public class MineableRock : MonoBehaviour, IToolTarget
    {
        [System.Serializable]
        public struct DropEntry
        {
            public ItemDefinition Item;
            public int Quantity;
            public GameObject DropPrefabOverride;
        }

        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private bool destroyOnBreak = true;

        [Header("Drops")]
        [SerializeField] private DropEntry[] drops;

        [Header("VFX/SFX")]
        [SerializeField] private GameObject breakVFXPrefab;
        [SerializeField] private AudioClip breakSound;

        private float _currentHealth;
        private bool _broken;

        private void Awake()
        {
            _currentHealth = maxHealth;
        }

        public bool AcceptsToolAction(System.Type actionType)
        {
            return actionType == typeof(PickaxeAction);
        }

        public bool ReceiveToolAction(ToolAction action, ToolUseContext context)
        {
            if (_broken) return false;

            var pickaxe = action as PickaxeAction;
            if (pickaxe == null) return false;

            ApplyDamage(pickaxe.Damage);
            return true;
        }

        private void ApplyDamage(float damage)
        {
            if (_broken) return;

            _currentHealth -= damage;
            if (_currentHealth <= 0f)
            {
                Break();
            }
        }

        private void Break()
        {
            _broken = true;

            SpawnDrops();

            if (breakVFXPrefab != null)
            {
                var vfx = Instantiate(breakVFXPrefab, transform.position, Quaternion.identity);
                Destroy(vfx, 2f);
            }

            if (breakSound != null)
                SFXPlayer.PlayAtPoint(breakSound, transform.position);

            if (destroyOnBreak)
                Destroy(gameObject);
            else
                gameObject.SetActive(false);
        }

        private void SpawnDrops()
        {
            if (drops == null) return;

            foreach (var drop in drops)
            {
                if (drop.Item == null || drop.Quantity <= 0) continue;

                GameObject prefab = drop.DropPrefabOverride != null
                    ? drop.DropPrefabOverride
                    : drop.Item.WorldPrefab;

                if (prefab == null) continue;

                var instance = Instantiate(prefab, transform.position + Vector3.up * 0.2f, Quaternion.identity);
                var pickup = instance.GetComponent<ItemPickup>();
                if (pickup == null)
                {
                    pickup = instance.AddComponent<ItemPickup>();
                }
                pickup.SetItem(drop.Item, drop.Quantity);
            }
        }
    }
}
