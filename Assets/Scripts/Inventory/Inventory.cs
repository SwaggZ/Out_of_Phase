using System;
using UnityEngine;
using OutOfPhase.Items;

namespace OutOfPhase.Inventory
{
    /// <summary>
    /// Main inventory system managing N slots of items.
    /// Handles adding, removing, and organizing items.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Number of inventory slots (default 5 for hotbar)")]
        [SerializeField] private int slotCount = 5;

        /// <summary>The inventory slots array</summary>
        private InventorySlot[] _slots;

        // Events
        /// <summary>Fired when any slot changes (index, oldSlot, newSlot)</summary>
        public event Action<int, InventorySlot, InventorySlot> OnSlotChanged;
        
        /// <summary>Fired when an item is added (index, item, quantity)</summary>
        public event Action<int, ItemDefinition, int> OnItemAdded;
        
        /// <summary>Fired when an item is removed (index, item, quantity)</summary>
        public event Action<int, ItemDefinition, int> OnItemRemoved;
        
        /// <summary>Fired when inventory is cleared</summary>
        public event Action OnInventoryCleared;

        // Public accessors
        public int SlotCount => slotCount;
        public InventorySlot[] Slots => _slots;

        private void Awake()
        {
            InitializeSlots();
        }

        /// <summary>
        /// Initializes or reinitializes the slot array.
        /// </summary>
        public void InitializeSlots()
        {
            _slots = new InventorySlot[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                _slots[i] = new InventorySlot();
            }
        }

        /// <summary>
        /// Gets the slot at the specified index.
        /// </summary>
        public InventorySlot GetSlot(int index)
        {
            if (index < 0 || index >= slotCount) return null;
            return _slots[index];
        }

        /// <summary>
        /// Attempts to add an item to the inventory.
        /// Will try to stack with existing items first, then use empty slots.
        /// Returns the quantity that couldn't be added.
        /// </summary>
        public int TryAddItem(ItemDefinition item, int quantity = 1, float durability = -1f)
        {
            if (item == null || quantity <= 0) return quantity;

            int remaining = quantity;

            // First pass: try to stack with existing items
            if (item.IsStackable)
            {
                for (int i = 0; i < slotCount && remaining > 0; i++)
                {
                    if (_slots[i].Item == item && _slots[i].CanAccept(item))
                    {
                        var oldSlot = _slots[i].Clone();
                        int wasRemaining = remaining;
                        remaining = _slots[i].TryAddQuantity(remaining);
                        
                        if (remaining < wasRemaining)
                        {
                            OnSlotChanged?.Invoke(i, oldSlot, _slots[i]);
                            OnItemAdded?.Invoke(i, item, wasRemaining - remaining);
                        }
                    }
                }
            }

            // Second pass: use empty slots
            for (int i = 0; i < slotCount && remaining > 0; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    var oldSlot = _slots[i].Clone();
                    int toAdd = item.IsStackable ? Mathf.Min(remaining, item.MaxStackSize) : 1;
                    _slots[i].Set(item, toAdd, durability);
                    remaining -= toAdd;
                    
                    OnSlotChanged?.Invoke(i, oldSlot, _slots[i]);
                    OnItemAdded?.Invoke(i, item, toAdd);
                }
            }

            return remaining;
        }

        /// <summary>
        /// Adds an item to a specific slot. Returns false if slot is occupied with different item.
        /// </summary>
        public bool TryAddItemToSlot(int slotIndex, ItemDefinition item, int quantity = 1, float durability = -1f)
        {
            if (slotIndex < 0 || slotIndex >= slotCount) return false;
            if (item == null) return false;

            var slot = _slots[slotIndex];
            
            if (slot.IsEmpty)
            {
                var oldSlot = slot.Clone();
                slot.Set(item, quantity, durability);
                OnSlotChanged?.Invoke(slotIndex, oldSlot, slot);
                OnItemAdded?.Invoke(slotIndex, item, quantity);
                return true;
            }
            
            if (slot.Item == item && slot.CanAccept(item))
            {
                var oldSlot = slot.Clone();
                int added = quantity - slot.TryAddQuantity(quantity);
                if (added > 0)
                {
                    OnSlotChanged?.Invoke(slotIndex, oldSlot, slot);
                    OnItemAdded?.Invoke(slotIndex, item, added);
                }
                return added > 0;
            }

            return false;
        }

        /// <summary>
        /// Removes a quantity of an item from the inventory.
        /// Returns the actual quantity removed.
        /// </summary>
        public int RemoveItem(ItemDefinition item, int quantity = 1)
        {
            if (item == null || quantity <= 0) return 0;

            int toRemove = quantity;
            int totalRemoved = 0;

            for (int i = 0; i < slotCount && toRemove > 0; i++)
            {
                if (_slots[i].Item == item)
                {
                    var oldSlot = _slots[i].Clone();
                    int removed = _slots[i].RemoveQuantity(toRemove);
                    toRemove -= removed;
                    totalRemoved += removed;
                    
                    OnSlotChanged?.Invoke(i, oldSlot, _slots[i]);
                    OnItemRemoved?.Invoke(i, item, removed);
                }
            }

            return totalRemoved;
        }

        /// <summary>
        /// Removes item(s) from a specific slot.
        /// Returns the quantity actually removed.
        /// </summary>
        public int RemoveFromSlot(int slotIndex, int quantity = 1)
        {
            if (slotIndex < 0 || slotIndex >= slotCount) return 0;
            
            var slot = _slots[slotIndex];
            if (slot.IsEmpty) return 0;

            var item = slot.Item;
            var oldSlot = slot.Clone();
            int removed = slot.RemoveQuantity(quantity);
            
            if (removed > 0)
            {
                OnSlotChanged?.Invoke(slotIndex, oldSlot, slot);
                OnItemRemoved?.Invoke(slotIndex, item, removed);
            }

            return removed;
        }

        /// <summary>
        /// Clears a specific slot.
        /// </summary>
        public void ClearSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slotCount) return;
            
            var slot = _slots[slotIndex];
            if (slot.IsEmpty) return;

            var item = slot.Item;
            int quantity = slot.Quantity;
            var oldSlot = slot.Clone();
            
            slot.Clear();
            
            OnSlotChanged?.Invoke(slotIndex, oldSlot, slot);
            OnItemRemoved?.Invoke(slotIndex, item, quantity);
        }

        /// <summary>
        /// Clears all slots.
        /// </summary>
        public void ClearAll()
        {
            for (int i = 0; i < slotCount; i++)
            {
                _slots[i].Clear();
            }
            OnInventoryCleared?.Invoke();
        }

        /// <summary>
        /// Swaps contents of two slots.
        /// </summary>
        public void SwapSlots(int indexA, int indexB)
        {
            if (indexA < 0 || indexA >= slotCount) return;
            if (indexB < 0 || indexB >= slotCount) return;
            if (indexA == indexB) return;

            var oldA = _slots[indexA].Clone();
            var oldB = _slots[indexB].Clone();

            // Swap data
            var temp = _slots[indexA];
            _slots[indexA] = _slots[indexB];
            _slots[indexB] = temp;

            OnSlotChanged?.Invoke(indexA, oldA, _slots[indexA]);
            OnSlotChanged?.Invoke(indexB, oldB, _slots[indexB]);
        }

        /// <summary>
        /// Checks if the inventory contains at least the specified quantity of an item.
        /// </summary>
        public bool HasItem(ItemDefinition item, int quantity = 1)
        {
            return GetItemCount(item) >= quantity;
        }

        /// <summary>
        /// Gets the total count of an item across all slots.
        /// </summary>
        public int GetItemCount(ItemDefinition item)
        {
            if (item == null) return 0;

            int count = 0;
            for (int i = 0; i < slotCount; i++)
            {
                if (_slots[i].Item == item)
                {
                    count += _slots[i].Quantity;
                }
            }
            return count;
        }

        /// <summary>
        /// Finds the first slot containing the specified item.
        /// Returns -1 if not found.
        /// </summary>
        public int FindItem(ItemDefinition item)
        {
            for (int i = 0; i < slotCount; i++)
            {
                if (_slots[i].Item == item)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Finds the first empty slot. Returns -1 if inventory is full.
        /// </summary>
        public int FindEmptySlot()
        {
            for (int i = 0; i < slotCount; i++)
            {
                if (_slots[i].IsEmpty)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Checks if the inventory is full.
        /// </summary>
        public bool IsFull()
        {
            return FindEmptySlot() == -1;
        }

        /// <summary>
        /// Reduces durability on an item in a specific slot.
        /// Returns true if the item broke.
        /// </summary>
        public bool ReduceDurability(int slotIndex, float amount)
        {
            if (slotIndex < 0 || slotIndex >= slotCount) return false;
            
            var slot = _slots[slotIndex];
            if (!slot.HasDurability) return false;

            var oldSlot = slot.Clone();
            bool broke = slot.ReduceDurability(amount);
            
            OnSlotChanged?.Invoke(slotIndex, oldSlot, slot);
            
            if (broke)
            {
                var item = slot.Item;
                ClearSlot(slotIndex);
            }
            
            return broke;
        }

        /// <summary>
        /// Changes the slot count at runtime (use with caution).
        /// Items in removed slots will be lost.
        /// </summary>
        public void SetSlotCount(int newCount)
        {
            if (newCount < 1) newCount = 1;
            if (newCount == slotCount) return;

            var oldSlots = _slots;
            slotCount = newCount;
            InitializeSlots();

            // Copy over items from old slots
            int copyCount = Mathf.Min(oldSlots.Length, slotCount);
            for (int i = 0; i < copyCount; i++)
            {
                _slots[i] = oldSlots[i];
            }
        }
    }
}
