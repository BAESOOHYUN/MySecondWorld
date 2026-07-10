using System;

namespace MySecondWorld.Inventory
{
    [Serializable]
    public struct StatModifier
    {
        public StatType Type;
        public ModifierType ModifierType;
        public float Value;
    }
}