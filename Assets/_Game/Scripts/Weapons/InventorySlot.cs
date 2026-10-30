using System;
using Unity.Netcode;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Weapons
{
    /// <summary>One inventory slot as it crosses the network: item index, magazine, reserve / stack.</summary>
    public struct InventorySlot : INetworkSerializable, IEquatable<InventorySlot>
    {
        public int item;
        public int magazine;
        /// <summary>Reserve ammo for guns, count for gadgets.</summary>
        public int reserve;

        public static readonly InventorySlot Empty = new InventorySlot { item = -1 };
        public bool IsEmpty => item < 0;

        public void NetworkSerialize<T>(BufferSerializer<T> pSerializer) where T : IReaderWriter
        {
            pSerializer.SerializeValue(ref item);
            pSerializer.SerializeValue(ref magazine);
            pSerializer.SerializeValue(ref reserve);
        }

        public bool Equals(InventorySlot pOther) => item == pOther.item && magazine == pOther.magazine && reserve == pOther.reserve;
    }
}
