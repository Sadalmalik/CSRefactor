namespace CSR.Core.Config
{
    // Размер топа отчета (--top, ключ top): первые top элементов, 0 - все
    public static class TopLimit
    {
        public static IEnumerable<T> TakeTop<T>(this IEnumerable<T> items, int top)
            => top > 0 ? items.Take(top) : items;
    }
}
