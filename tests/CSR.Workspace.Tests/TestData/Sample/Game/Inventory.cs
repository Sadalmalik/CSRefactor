namespace Sample.Items
{
    [Serializable]
    public class Item
    {
        public string Title = "";
        public int Count;

        [NonSerialized]
        public int Cache;

        public int Width, Height;
    }

    public class Inventory
    {
        private readonly List<Item> m_Items = new();

        public int Total
            => m_Items.Sum(item => item.Count);

        public void Add(Item item)
            => Add(item, 1);

        public void Add(Item item, int count)
        {
            item.Count += count;
            m_Items.Add(item);
        }

        public Item? Find(string title)
        {
            var item = m_Items.FirstOrDefault(candidate => candidate.Title == title);
            if (item == null)
                Console.WriteLine("Inventory: no Title " + title);

            return item;
        }
    }
}
