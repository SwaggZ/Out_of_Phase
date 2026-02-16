using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutOfPhase.Progression
{
    /// <summary>
    /// Persistent save data structure. Serialized to JSON via PlayerPrefs.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        // ── Header ──
        public string saveName = "Checkpoint";
        public string timestamp;

        // ── Player ──
        public float[] playerPosition = new float[3];
        public float[] playerRotation = new float[2]; // yaw, pitch

        // ── Progression ──
        public int currentSectionIndex;
        public int[] completedSections = Array.Empty<int>();

        // ── Dimension ──
        public int currentDimension;

        // ── Inventory ──
        public InventorySlotData[] inventorySlots = Array.Empty<InventorySlotData>();

        // ── Flags (generic key-value for quests, NPC state, etc.) ──
        public StringBoolPair[] flags = Array.Empty<StringBoolPair>();

        // ── Helpers ──
        public Vector3 GetPlayerPosition() =>
            new Vector3(playerPosition[0], playerPosition[1], playerPosition[2]);

        public void SetPlayerPosition(Vector3 pos)
        {
            playerPosition[0] = pos.x;
            playerPosition[1] = pos.y;
            playerPosition[2] = pos.z;
        }

        public void SetPlayerRotation(float yaw, float pitch)
        {
            playerRotation[0] = yaw;
            playerRotation[1] = pitch;
        }
    }

    [Serializable]
    public class InventorySlotData
    {
        public string itemId;   // ItemDefinition.name (asset name)
        public int quantity;
        public float durability;
    }

    [Serializable]
    public class StringBoolPair
    {
        public string key;
        public bool value;
    }
}
