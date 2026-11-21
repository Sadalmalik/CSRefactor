using Sample.Items;
using Sample.Units;

namespace Sample.Tools
{
    public static class Report
    {
        public static string Describe(Inventory inventory, IDamageable target)
        {
            inventory.Add(new Item { Title = "Sword" });
            inventory.Add(new Item { Title = "Shield" }, 2);
            target.TakeDamage(1);
            return $"Inventory total {inventory.Total}";
        }
    }
}
