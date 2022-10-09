using System;
using System.Collections.Generic;
using System.Linq;

namespace FaceLookup.Common
{
    public class CollectionHelper
    {
        public static List<List<T>> Split<T>(IEnumerable<T> source, int batchSize)
        {
            return source
                .Select((x, i) => new { Index = i, Value = x })
                .GroupBy(x => x.Index / batchSize)
                .Select(x => x.Select(v => v.Value).ToList())
                .ToList();
        }
    }
}
