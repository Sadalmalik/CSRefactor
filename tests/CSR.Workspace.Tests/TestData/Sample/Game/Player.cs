using Sample.Items;
using UnityEngine;

namespace Sample.Units
{
    public interface IDamageable
    {
        void TakeDamage(int amount);
    }

    public class Player : MonoBehaviour, IDamageable
    {
        [SerializeField]
        private float m_Speed = 5;

        public int Health = 100;

        private readonly Inventory m_Inventory = new();

        [field: SerializeField]
        public string Nick { get; private set; } = "";

        public float Speed
            => m_Speed;

        public void TakeDamage(int amount)
            => Health -= amount;

        public void Pick(Item item)
            => m_Inventory.Add(item);

        private void Update()
        {
            if (Health <= 0)
                Nick = nameof(m_Speed);
        }
    }
}
