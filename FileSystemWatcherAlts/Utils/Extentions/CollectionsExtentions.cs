using System.Collections.Generic;

namespace FileSystemWatcherAlts.Utils.Extentions
{
    internal static class CollectionsExtentions
    {
        /// <summary>
        /// Pushes a range of items into a stack
        /// </summary>
        /// <typeparam name="T">The type of items in the stack</typeparam>
        /// <param name="stack">The stack to push into</param>
        /// <param name="items">The items to push</param>
        internal static void PushRange<T>(this Stack<T> stack, IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                stack.Push(item);
            }
        }

        /// <summary>
        /// Addsa a range of items into a collection
        /// </summary>
        /// <typeparam name="T">The type of items in the collection</typeparam>
        /// <param name="collection">The collection to add the items into</param>
        /// <param name="items">The items to add</param>
        internal static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                collection.Add(item);
            }
        }
    }
}