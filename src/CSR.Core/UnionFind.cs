namespace CSR.Core
{
    // Связные компоненты элементов 0..count-1: объединение и корень компоненты (со сжатием путей)
    public class UnionFind
    {
        private readonly int[] m_Parents;

        public UnionFind(int count)
        {
            m_Parents = Enumerable.Range(0, count).ToArray();
        }

        public int Find(int index)
        {
            while (m_Parents[index] != index)
            {
                m_Parents[index] = m_Parents[m_Parents[index]];
                index = m_Parents[index];
            }

            return index;
        }

        // Компонента right присоединяется к компоненте left
        public void Union(int left, int right)
        {
            int leftRoot = Find(left);
            int rightRoot = Find(right);
            if (leftRoot != rightRoot)
                m_Parents[rightRoot] = leftRoot;
        }
    }
}
