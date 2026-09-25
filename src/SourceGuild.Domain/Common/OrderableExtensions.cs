namespace SourceGuild.Domain.Common;

internal static class OrderableExtensions
{
    public static void Reindex<T>(this IList<T> items) where T : IOrderable
    {
        for (int i = 0; i < items.Count; i++)
        {
            items[i].SetOrder(i);
        }
    }
}
