using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Specialized;
using System.Diagnostics;

﻿namespace grok;

public static class EnumHelpers
{
    public static IEnumerable < (T item, int idx) > WithIdxs<T>(this IEnumerable<T> obj)
    {
        return obj.Select((obj, idx) => (obj, idx));
    }

    public static HashSet<string> Get(this Dictionary<string, HashSet<string>> obj, string nonterm)
    {
        HashSet<string>? fol;
        if (obj.TryGetValue(nonterm, out fol) == false)
        {
            fol = new HashSet<string>();
            obj.Add(nonterm, fol);
        }
        return fol;
    }
}
