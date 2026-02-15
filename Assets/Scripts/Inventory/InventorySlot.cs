using System;
using OutOfPhase.Items;

namespace OutOfPhase.Inventory
{
    /// <summary>
    /// Represents a single inventory slot that can hold an item with quantity and durability.
    /// </summary>
    [Serializable]
    public class InventorySlot
    {
        /// <summary>The item definition in this slot (null if empty)</summary>
        public ItemDefinition Item;
        
        /// <summary>Stack count for stackable items</summary>
        public int Quantity;
        
        /// <summary>Current durability (for tools). -1 means no durability tracking.</summary>
        public float Durability;

        /// <summary>Whether this slot contains an item</summary>
        public bool HasItem => Item != null && Quantity > 0;

        /// <summary>Whether this slot is empty</summary>
        public bool IsEmpty => Item == null || Quantity <= 0;

        /// <summary>Whether the item in this slot has durability tracking</summary>
        public bool HasDurability => Item != null && Item.HasDurability && Durability >= 0;

        /// <summary>Durability as a 0-1 ratio</summary>
        public float DurabilityRatio => HasDurability && Item.MaxDurability > 0 
            ? Durability / Item.MaxDurability 
            : 1f;

        public InventorySlot()
        {
            Clear();
        }

        public InventorySlot(ItemDefinition item, int quantity = 1, float durability = -1f)
        {
            Set(item, quantity, durability);
        }

        /// <summary>
        /// Sets this slot to contain the specified item.
        /// </summary>
        public void Set(ItemDefinition item, int quantity = 1, float durability = -1f)
        {
            Item = item;
            Quantity = quantity;
            
            // Auto-set durability for tools if not specified
            if (item != null && item.HasDurability && durability < 0)
            {
                Durability = item.MaxDurability;
            }
            else
            {
                Durability = durability;
            }
        }

        /// <summary>
        /// Clears this slot.
        /// </summary>
        public void Clear()
        {
            Item = null;
            Quantity = 0;
            Durability = -1f;
        }

        /// <summary>
        /// Creates a copy of this slot.
        /// </summary>
        public InventorySlot Clone()
        {
            return new InventorySlot(Item, Quantity, Durability);
        }

        /// <summary>
        /// Attempts to add quantity to this slot. Returns the amount that couldn't be added.
        /// </summary>
        public int TryAddQuantity(int amount)
        {
            if (Item == null || !Item.IsStackable)
                return amount;

            int maxCanAdd = Item.MaxStackSize - Quantity;
            int toAdd = Math.Min(amount, maxCanAdd);
            Quantity += toAdd;
            return amount - toAdd;
        }

        /// <summary>
        /// Removes quantity from this slot. Returns the amount actually removed.
        /// Clears the slot if quantity reaches 0.
        /// </summary>
        public int RemoveQuantity(int amount)
        {
            int toRemove = Math.Min(amount, Quantity);
            Quantity -= toRemove;
            
            if (Quantity <= 0)
            {
                Clear();
            }
            
            return toRemove;
        }

        /// <summary>
        /// Reduces durability. Returns true if the item broke (durability <= 0).
        /// </summary>
        public bool ReduceDurability(float amount)
        {
            if (!HasDurability) return false;

            Durability -= amount;
            if (Durability <= 0)
            {
                Durability = 0;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if this slot can accept the given item (for stacking).
        /// </summary>
        public bool CanAccept(ItemDefinition item)
        {
            if (IsEmpty) return true;
            if (Item != item) return false;
            if (!Item.IsStackable) return false;
            return Quantity < Item.MaxStackSize;
        }

        /// <summary>
        /// Gets how many more of this item can be added to the slot.
        /// </summary>
        public int GetSpaceRemaining()
        {
            if (IsEmpty) return int.MaxValue; // Empty slot can take any amount
            if (!Item.IsStackable) return 0;
            return Item.MaxStackSize - Quantity;
        }

        public override string ToString()
        {
            if (IsEmpty) return "[Empty]";
            string durStr = HasDurability ? $" ({Durability:F0}/{Item.MaxDurability:F0})" : "";
            return $"[{Item.ItemName} x{Quantity}{durStr}]";
        }
    }
}
