using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Specialized;
using System.Diagnostics;

﻿namespace grok;

public class ProdRule
{
    public List<string> derivation = new List<string>();
    public int hash = string.Empty.GetHashCode();

    public int[] SymbolReferences(string symbol) =>
        (from si in derivation.WithIdxs()
         where si.item == symbol
         select si.idx).ToArray();

    public ProdRule Clone()
    {
        var p = new ProdRule();
        p.derivation = new List<string>(derivation);
        return p;
    }

    void MakeHash()
    {
        hash = string.Join(' ', derivation).GetHashCode();
    }

    public override string ToString()
    {
        return $"{string.Join(' ', derivation)}";
    }
}

public class Production
{
    public string nonterminal = string.Empty;
    public List<ProdRule> rules = new List<ProdRule>();

    public void AddProduction(IEnumerable<string> derivation)
    {
        int hash = string.Join(' ', from s in derivation select s).GetHashCode();
        if ((from r in rules where r.hash == hash select r).Count() == 0)
        {
            rules.Add(new ProdRule { derivation = derivation.ToList(), hash = hash } );
        }
    }

    public void RemoveProduction(int productionIdx)
    {
        if (rules.Count() > productionIdx)
        {
            rules.RemoveAt(productionIdx);
        }
    }

    public Production Clone()
    {
        var p = new Production();
        p.nonterminal = nonterminal;
        p.rules = new List<ProdRule>(rules);
        return p;
    }

    public override string ToString()
    {
        var productions = string.Join(" | ",
            from r in rules
            select r.ToString());
        return $"{nonterminal} -> {productions}";
    }

    public string ToString(int ruleIdx)
    {
        return $"{nonterminal} -> {rules[ruleIdx]}";
    }
}

public class ProductionSet
{
    List<Production> productions = new List<Production>();
    Dictionary<string, int> productionIndices = new Dictionary<string, int>();
    public int cavepersonDebugging = 0;

    public IEnumerable<string> Nonterminals => productionIndices.Keys;

    public IEnumerable<(string nonterminal, int idx, ProdRule prodRule)> Productions =>
        from p in productions
        from di in p.rules.WithIdxs()
        select (p.nonterminal, di.idx, di.item);

    public IEnumerable<(string nonterminal, int idx, ProdRule prodRule)> ProductionsOf(string nonterminal) =>
        from di in productions[productionIndices[nonterminal]].rules.WithIdxs()
        select (nonterminal, di.idx, di.item);

    public Production ProductionOf(string nonterminal) => productions[productionIndices[nonterminal]];

    public IEnumerable<string> DerivationsOf(string nonterminal, int ruleIdx) => ProductionOf(nonterminal).rules[ruleIdx].derivation;

    public (string nonterminal, int idx) FirstNonterminalWithEmptyRule =>
        (from p in productions
         from di in p.rules.WithIdxs()
         where (di.item.derivation.Count() == 1
             && di.item.derivation[0] == string.Empty)
         select (p.nonterminal, di.idx)).FirstOrDefault((string.Empty, -1));

    public (string nonterminal, int idx) FirstNonterminalWithUnitRule =>
        (from p in productions
         from di in p.rules.WithIdxs()
         where (di.item.derivation.Count() == 1
             && Nonterminals.Contains(di.item.derivation[0]))
         select (p.nonterminal, di.idx)).FirstOrDefault((string.Empty, -1));

    public IEnumerable<(string nonterminal, int idx, ProdRule prodRule)> NullProductions =>
        from p in productions
        from di in p.rules.WithIdxs()
        where (di.item.derivation.Count() == 1
            && di.item.derivation[0] == string.Empty)
        select (p.nonterminal, di.idx, di.item);

    public IEnumerable<(string nonterminal, int idx, ProdRule prodRule)> UnitProductions =>
        from p in productions                                   // A -> {x1|x2|x3}
        from di in p.rules.WithIdxs()                           // for each rule A -> xn
        where (di.item.derivation.Count() == 1                  // where xn == B is unitary
            && Nonterminals.Contains(di.item.derivation[0]))    // where B is a nonterminal
        select (p.nonterminal, di.idx, di.item);

    public IEnumerable<(string nonterminal, int idx, ProdRule prodRule)> ProductionsReferencing(string symbol)
    {
        return
            from p in productions
            from di in p.rules.WithIdxs()
            from s in di.item.derivation
            where s == symbol
            select (p.nonterminal, di.idx, di.item);
    }

    public IEnumerable<(string nonterminal, int idx, ProdRule prodRule)> ProductionsReferencing(IEnumerable<string> symbols)
    {
        var hs = new HashSet<string>(symbols);
        return
            from p in productions
            from di in p.rules.WithIdxs()
            from s in di.item.derivation
            where hs.Contains(s)
            select (p.nonterminal, di.idx, di.item);
    }

    private void Report(string s, int level = 1)
    {
        if (cavepersonDebugging >= level)
            { Console.WriteLine(s); }
    }

    public ProductionSet Clone()
    {
        var ps = new ProductionSet();
        ps.productions = (from p in productions select p.Clone()).ToList();
        ps.productionIndices = new Dictionary<string, int>(productionIndices);
        ps.cavepersonDebugging = cavepersonDebugging;
        return ps;
    }

    public void Add(string nonterminal, IEnumerable<string> derivation)
    {
        Report($"ADD RULE: {nonterminal} -> {string.Join(' ', derivation)}", 3);

        Production prod;
        int nonterminalIdx = -1;
        if (productionIndices.TryGetValue(nonterminal, out nonterminalIdx) == false)
        {
            nonterminalIdx = productions.Count;
            prod = new Production();
            prod.nonterminal = nonterminal;
            productions.Add(prod);
            productionIndices.Add(nonterminal, nonterminalIdx);
        }
        else
        {
            prod = productions[nonterminalIdx];
        }

        prod.AddProduction(derivation);
    }

    public void Remove(string nonterminal, int idx)
    {
        int nonterminalIdx = -1;
        if (productionIndices.TryGetValue(nonterminal, out nonterminalIdx))
        {
            Production prod = productions[nonterminalIdx];

            Report($"REMOVE RULE {nonterminal} -> {string.Join(' ', prod.rules[idx].derivation)}", 3);

            prod.RemoveProduction(idx);
            if (prod.rules.Count() == 0)
            {
                productions.RemoveAt(nonterminalIdx);
                productionIndices.Remove(nonterminal);
                foreach (var pi in new Dictionary<string, int>(productionIndices))
                {
                    if (pi.Value > nonterminalIdx)
                        { productionIndices[pi.Key] = pi.Value - 1; }
                }
            }
        }
    }

    public void MoveToFront(string nonterminal)
    {
        int idx = productionIndices[nonterminal];
        var prod = productions[idx];
        for (int i = idx - 1; i >= 0; --i)
            { productions[i + 1] = productions[i]; }
        productions[0] = prod;

        foreach(var (nonterm, i) in productionIndices)
        {
            if (i < idx)
            {
                productionIndices[nonterm] = i + 1;
            }
            else if (i == idx)
            {
                productionIndices[nonterm] = 0;
            }
        }
    }

    public override string ToString()
    {
        var sb = new StringBuilder();

        int maxNonterminalNameLen = (from t in Nonterminals select t.Length).Max();

        foreach (var prod in productions)
        {
            var t = prod.nonterminal;
            sb.Append(t).Append(new string(' ', maxNonterminalNameLen - t.Length));

            foreach (var (prodRule, idx) in prod.rules.WithIdxs())
            {
                if (idx == 0)
                    { sb.Append(": "); }
                else
                    { sb.Append("| "); }

                var ps  = prodRule.derivation;
                if (ps == null || ps.Count() == 0)
                    { sb.Append("\n"); }
                else
                    { sb.Append(string.Join(' ', ps)).Append('\n'); }

                sb.Append(new string(' ', maxNonterminalNameLen));
            }

            sb.Append(";\n\n");
        }

        return sb.ToString();
    }
}

public class ContextFreeGrammar
{
    HashSet<string> terminals = new HashSet<string>();
    ProductionSet productions = new ProductionSet();
    string startSymbol = string.Empty;
    HashSet<string> usedSymbols = new HashSet<string>();
    int cavepersonDebugging = 0;
    Dictionary<string, HashSet<string>> first = new Dictionary<string, HashSet<string>>();
    Dictionary<string, HashSet<string>> follow = new Dictionary<string, HashSet<string>>();

    string eofString = string.Empty;

    public ContextFreeGrammar(IEnumerable<string> terminals, int cavepersonDebugging = 0)
    {
        this.terminals = new HashSet<string>(terminals);
        this.cavepersonDebugging = cavepersonDebugging;
        this.productions.cavepersonDebugging = cavepersonDebugging;
        this.eofString = GetNewSymbol("EOF");
    }

    public ContextFreeGrammar(IEnumerable<string> terminals, ContextFreeGrammar template)
    {
        this.terminals = new HashSet<string>(terminals);
        this.productions = template.productions.Clone();
        this.startSymbol = template.startSymbol;
        this.cavepersonDebugging = template.cavepersonDebugging;
        this.eofString = GetNewSymbol("EOF");
    }

    public ContextFreeGrammar(ContextFreeGrammar template)
    {
        this.terminals = new HashSet<string>(template.terminals);
        this.productions = template.productions.Clone();
        this.startSymbol = template.startSymbol;
        this.usedSymbols.UnionWith(this.Terminals);
        this.usedSymbols.UnionWith(this.Nonterminals);
        this.cavepersonDebugging = template.cavepersonDebugging;
        this.eofString = GetNewSymbol("EOF");
    }

    private void Report(string s, int level = 1)
    {
        if (cavepersonDebugging >= level)
            { Console.WriteLine(s); }
    }

    public void Prod(string nonterminal, IEnumerable<string> derivation, bool startSymbol = false)
    {
        bool needNewEof = nonterminal == eofString;

        productions.Add(nonterminal, derivation);
        if (startSymbol)
        {
            this.startSymbol = nonterminal;
            productions.MoveToFront(this.startSymbol);
        }
        this.usedSymbols.Add(nonterminal);

        if (needNewEof)
            { eofString = GetNewSymbol("EOF"); }
    }

    public string StartSymbol => startSymbol;

    public HashSet<string> Terminals => terminals;

    public string EofString => eofString;

    public IEnumerable<string> Nonterminals => productions.Nonterminals;

    public IEnumerable<(string nonterminal, int idx, ProdRule prodRule)> Productions => productions.Productions;

    public IEnumerable<string> DerivationsOf(string nonterminal, int ruleIdx) => productions.DerivationsOf(nonterminal, ruleIdx);

    public override string ToString() => $"Start: {startSymbol}\n{productions}";

#region Frist

    HashSet<string> First_rec(string symbol, HashSet<string> visited)
    {
        HashSet<string>? f;
        if (this.first.TryGetValue(symbol, out f))
            { return f; }

        f = new HashSet<string>();

        // if symbol is a terminal, return { symbol }
        if (terminals.Contains(symbol))
        {
            f.Add(symbol);
            return f;
        }

        // symbol is a nonterminal (hopefully)

        if (visited.Contains(symbol))
            { throw new Exception("Grammar is left-recursive on nonterminal '{symbol}'. First() can't run."); }
        visited.Add(symbol);

        var prod = productions.ProductionOf(symbol);
        foreach (var (rule, idxs) in prod.rules.WithIdxs())
        {
            // if symbol-*>null, add null to first
            if (rule.derivation.Count() == 1 &&
                rule.derivation[0] == string.Empty)
            {
                f.Add(string.Empty);
            }
            else
            {
                f.UnionWith(First_pvt(rule.derivation, visited));
            }
        }

        this.first.Add(symbol, f);
        return f;
    }

    HashSet<string> First_pvt(IEnumerable<string> stringOfSymbols, HashSet<string> visited)
    {
        var f = new HashSet<string>();

        foreach (var (sym, symIdx) in stringOfSymbols.WithIdxs())
        {
            var firstOfSym = new HashSet<string>(First_rec(sym, visited));
            var ruleDerivesNull = firstOfSym.Contains(string.Empty);
            if (ruleDerivesNull)
                { firstOfSym.Remove(string.Empty); }

            f.UnionWith(firstOfSym);

            // if this sym does not derive null, we're done
            if (ruleDerivesNull == false)
                { break; }

            // if we're on the last sym and we have always derived null for
            // each sym in the rule, make sure to add null to f
            if (symIdx == stringOfSymbols.Count() - 1)
                { f.Add(string.Empty); }
        }

        return f;
    }

    private void ComputeFirsts()
    {
        first.Clear();
        var visited = new HashSet<string>();

        first.Add(string.Empty, new HashSet<string>(new [] { string.Empty }));

        foreach (var t in Terminals)
            { First_rec(t, visited); }

        foreach (var n in Nonterminals)
            { First_rec(n, visited); }
    }

    public HashSet<string> First(string symbol)
    {
        return first[symbol];
    }

    public HashSet<string> First(IEnumerable<string> stringOfSymbols)
    {
        var visited = new HashSet<string>();
        return First_pvt(stringOfSymbols, visited);
    }

#endregion

#region Follow

    private void ComputeFollows()
    {
        follow.Clear();

        //  follow(startSymbol) unionwith {EOF}
        //  for each production rule A->x0x1x2..xn:
        //      for each symbol xi in [x0..xn-1):
        //          if xi is a nonterminal:
        //              f = first(xi+1..xn) - null
        //              follow(xi) unionwith f
        //  for each production rule A->x0x1x2..xn:
        //      for each symbol xi in [xn-1..x0]:
        //          if xi is a nonterminal:
        //              follow(xi) unionwith follow(A)
        //          if first(xi+1..xn) does not contain null:
        //              break

        //  follow(startSymbol) unionwith {EOF}
        follow.Add(startSymbol, new HashSet<string>(new [] { eofString }));

        Report("phase 1", 2);

        //  for each production rule A->x0x1x2..xn:
        foreach (var (A, ruleIdx, rule) in productions.Productions)
        {
            Report($"Examining {A} -> {string.Join(' ', rule.derivation)}", 2);
            //  for each symbol xi in [x0..xn-1):
            foreach (var (xi, symIdx) in rule.derivation.Take(rule.derivation.Count() - 1).WithIdxs())
            {
                //  if xi is a nonterminal:
                if (Nonterminals.Contains(xi))
                {
                    var f = new HashSet<string>(First(rule.derivation.Skip(symIdx + 1)));

                    Report($" - B == {xi}; first({string.Join(' ', rule.derivation.Skip(symIdx + 1))}) == {{{string.Join(',', f)}}}", 2);
                    //  f = first(xi+1..xn) - null
                    if (f.Contains(string.Empty))
                        { f.Remove(string.Empty); }

                    //  follow(xi) unionwith f
                    follow.Get(xi).UnionWith(f);
                    Report($" - recorded: follow({xi}).UnionWith({string.Join(',', f)}) == {{{string.Join(',', follow[xi])}}}", 2);
                }
            }
        }

        Report($"follow so far: ", 2);
        foreach (var kvp in follow)
        {
            Report($" - {kvp.Key}: {{{string.Join(',', kvp.Value)}}}", 2);
        }

        Report("phase 2", 2);

        //  for each production rule A->x0x1x2..xn:
        foreach (var (A, ruleIdx, rule) in productions.Productions)
        {
            Report($"Examining {A} -> {string.Join(' ', rule.derivation)}", 2);
            //  for each symbol xi in [xn-1..x0]:
            foreach (var (xi, i) in (from s in rule.derivation select s).WithIdxs().Reverse())
            {
                Report($" - i == {i}; B == {xi}", 2);
                //  if xi is a nonterminal:
                if (Nonterminals.Contains(xi))
                {
                    //  follow(xi) unionwith follow(A)
                    var fa = new HashSet<string>(follow[A]);
                    follow.Get(xi).UnionWith(fa);
                    Report($" - recorded: follow({xi}).UnionWith({string.Join(',', fa)}) == {{{string.Join(',', follow[xi])}}}", 2);

                    //  if first(xi..xn) does not contain null:
                    //      break
                    Report($" - checking if {string.Join(' ', rule.derivation.Skip(i))} derives null", 2);

                    var f = First(rule.derivation.Skip(i));
                    if (f.Contains(string.Empty) == false)
                    {
                        Report($" - - no, stop checking", 2);
                        break;
                    }
                }
                else
                    { break; }
            }
        }
    }

    public HashSet<string> Follow(string nonterminal)
    {
        return follow[nonterminal];
    }

#endregion

    public void ComputeFirstsAndFollows()
    {
        if (eofString == string.Empty)
            { eofString = GetNewSymbol("EOF"); }

        ComputeFirsts();
        ComputeFollows();
    }

    public string GetNewSymbol(string baseName = "")
    {
        if (baseName == string.Empty)
            { baseName = "NewNonterminal"; }

        for (int i = 0; i < int.MaxValue; ++i)
        {
            var name = $"{baseName}{i}";
            if (usedSymbols.Contains(name) == false)
            {
                Report($"MAKING NEW NONTERMINAL {name}", 3);
                usedSymbols.Add(name);
                return name;
            }
        }

        throw new Exception("Could not find a nonconflicting nonterminal name. Maybe I should have tried harder. But I didn't. womp womp");
    }

    public IEnumerable<string> CheckIntegrity()
    {
        // empty means no errors
        // TODO: Do some errors
        return new string[] {};
    }

    public (int numTerms, int numNonterms) CountDerivationSymbols(List<string> derivation)
    {
        int numTerms = 0;
        int numNonterms = 0;

        foreach (var s in derivation)
        {
            if (terminals.Contains(s))
                { numTerms += 1; }
            else
                { numNonterms += 1; }
        }

        return (numTerms, numNonterms);
    }

    public ContextFreeGrammar ReduceBottomUp()
    {
        Report("Reducing useless productions: bottom-up", 1);

        ContextFreeGrammar g = new ContextFreeGrammar(this);
        g.productions = productions.Clone();

        HashSet<string> searchSymbols = new HashSet<string>(g.Terminals);
        HashSet<string> searchSymbolsToAdd = new HashSet<string>(g.Terminals);

        // searchSymbols = get all nonterminals that reference terminals,
        // and all nonterminals that reference those, etc
        do {
            searchSymbolsToAdd.Clear();
            foreach(var th in g.productions.ProductionsReferencing(searchSymbols.ToArray()))
            {
                if (searchSymbols.Contains(th.nonterminal) == false)
                    { searchSymbolsToAdd.Add(th.nonterminal); }
            }
            searchSymbols.UnionWith(searchSymbolsToAdd);
        } while (searchSymbolsToAdd.Count() > 0);

        // now remove all productions that contain nonterminals which aren't in searchSymbols
        var removals = new List<(string nonterminal, int ruleIdx)>();
        foreach (var (nonterminal, idx, prodRule) in g.productions.Productions)
        {
            foreach (var s in prodRule.derivation)
            {
                if (searchSymbols.Contains(s) == false)
                    { removals.Add((nonterminal, idx)); }
            }
        }

        // Removals are in index-ascending order, which won't do for sequential
        // deleting by index. We can reverse the works though, and be safe.
        removals.Reverse();
        foreach (var (nonterminal, idx) in removals)
            { g.productions.Remove(nonterminal, idx); }

        return g;
    }

    public ContextFreeGrammar ReduceTopDown()
    {
        Report("Reducing useless productions: top-down", 1);

        ContextFreeGrammar g = new ContextFreeGrammar(terminals, cavepersonDebugging);

        HashSet<string> W = new HashSet<string>();
        HashSet<string> wToAdd = new HashSet<string>();
        HashSet<string> nontermsToFind = new HashSet<string>();

        W.Add(startSymbol);

        do
        {
            wToAdd.Clear();

            foreach (var symbol in W)
            {
                if (Terminals.Contains(symbol))
                    { continue; }

                foreach (var (nonterminal, idx, rule) in this.productions.ProductionsOf(symbol))
                {
                    g.Prod(nonterminal, rule.derivation, nonterminal == startSymbol);
                    foreach (var s in rule.derivation)
                    {
                        if (W.Contains(s) == false)
                            { wToAdd.Add(s); }
                    }
                }
            }

            W.UnionWith(wToAdd);
        } while (wToAdd.Count() > 0);

        return g;
    }

    public ContextFreeGrammar EliminateUselessProductions()
    {
        Report("Eliminating useless productions", 1);

        return ReduceBottomUp()
              .ReduceTopDown();
    }

    public ContextFreeGrammar EliminateUnitProductions_old()
    {
        Report("Eliminating unit productions", 1);

        ContextFreeGrammar g = new ContextFreeGrammar(this);

        // find all unit pairs
        var unitRules = g.productions.UnitProductions;
        var addedProds = new List<(string nonterminal, string [] derivation)>();

        // add all nonunit productions as appropriate
        foreach (var (nonterm, ruleIdx, prodRule) in unitRules)
        {
            string unitProd = prodRule.derivation[0];
            if (unitProd == nonterm)
                { continue; }       // Do not copy my own rules to myself. I have them already.

            Report($"Found unit production: {nonterm}:{ruleIdx} -> {unitProd}", 2);

            foreach (var (nontermRep, ruleIdxRep, prodRuleRep) in g.productions.ProductionsOf(unitProd))
            {
                addedProds.Add((nonterm, prodRuleRep.derivation.ToArray()));
            }
        }

        foreach (var (nonterm, deriv) in addedProds)
            { g.Prod(nonterm, deriv); }

        // remove all unit productions of any kind
        // Run this op again because the above may have added new unit rules. (They're still removable though.)
        var unitRulesArr = g.productions.UnitProductions.ToList();
        unitRulesArr.Reverse();
        var s = string.Join('\n',
                            from prod in unitRulesArr
                            select $"{prod.nonterminal}:{prod.idx} -> " + string.Join(' ', prod.prodRule.derivation));
        foreach (var (nonterm, ruleIdx, prodRule) in unitRulesArr)
        {
            g.productions.Remove(nonterm, ruleIdx);
        }

        return g;
    }

    public ContextFreeGrammar EliminateUnitProductions()
    {
        Report("Eliminating unit productions", 1);

        ContextFreeGrammar g = new ContextFreeGrammar(this);

        // find all unit pairs
        var unitRules = g.productions.UnitProductions.ToList();

        while (unitRules.Count() > 0)
        {
            var addedProds = new List<(string nonterminal, string [] derivation)>();

            // add all nonunit productions as appropriate
            foreach (var (nonterm, ruleIdx, prodRule) in unitRules)
            {
                string unitProd = prodRule.derivation[0];
                if (unitProd == nonterm)
                    { continue; }       // Do not copy my own rules to myself. I have them already.

                Report($"Found unit production: {nonterm}:{ruleIdx} -> {unitProd}", 2);

                foreach (var (nontermRep, ruleIdxRep, prodRuleRep) in g.productions.ProductionsOf(unitProd))
                {
                    addedProds.Add((nonterm, prodRuleRep.derivation.ToArray()));
                }
            }

            foreach (var (nonterm, deriv) in addedProds)
                { g.Prod(nonterm, deriv); }

            unitRules.Reverse();

            foreach (var (nonterm, ruleIdx, prodRule) in unitRules)
            {
                g.productions.Remove(nonterm, ruleIdx);
            }

            unitRules = g.productions.UnitProductions.ToList();
        }

        return g;
    }

    public ContextFreeGrammar EliminateNullProductions()
    {
        Report("Eliminating null productions", 1);

        ContextFreeGrammar g = new ContextFreeGrammar(this);

        var nullProds = g.productions.NullProductions.GetEnumerator();
        // This search is started over every iteration, just taking the first null prod each time.
        while (nullProds.MoveNext())
        {
            // Find the first null production that isn't S -> e.
            if (nullProds.Current.nonterminal == g.StartSymbol)
            {
                if (nullProds.MoveNext() == false)
                    { return g; }
            }

            var (nonterm, ruleIdx, _) = nullProds.Current;

            Report($"Found null production: {nonterm}:{ruleIdx} -> ", 2);

            // eliminate the empty prod
            g.productions.Remove(nonterm, ruleIdx);

            var addedProds = new List<(string nonterminal, string [] derivation)>();

            // Now visit every prod that references this nonterminal, and replace
            // them with versions with and without this nonterminal in the production.
            foreach (var (nontermRep, ruleIdxRep, prodRuleRep) in g.productions.Productions)
            {
                int [] symRefs = prodRuleRep.SymbolReferences(nonterm).ToArray();
                if (symRefs.Length > 0)
                {
                    // Each i is a bitmask for permutations of symbol occurrences in the rule.
                    for (int i = 0; i < (1 << symRefs.Length) - 1; ++i)
                    {
                        List<string> newRule = new List<string>();
                        int symRefCursor = 0;
                        foreach (var si in prodRuleRep.derivation.WithIdxs())
                        {
                            if (symRefCursor < symRefs.Length && si.idx == symRefs[symRefCursor])
                            {
                                if ((i & (1 << symRefCursor)) != 0)
                                    { newRule.Add(si.item); }
                                symRefCursor += 1;
                            }
                            else
                                { newRule.Add(si.item); }
                        }

                        // A unit nonterm which gets removed results in a null production. This will
                        // get pushed further out on each iteration of the outer while loop.
                        if (newRule.Count() == 0)
                            { newRule.Add(string.Empty); }

                        addedProds.Add((nontermRep, newRule.ToArray()));
                    }
                }
            }

            // add prods we created
            foreach (var ap in addedProds)
                { g.Prod(ap.nonterminal, ap.derivation); }

            // We'd found some nulls, but start the search over; we might be creating new ones.
            nullProds = g.productions.NullProductions.GetEnumerator();
        }

        return g;
    }

    public ContextFreeGrammar AbstractifyStartSymbol()
    {
        Report("Abstracting start symbol", 1);

        if (productions.ProductionsReferencing(startSymbol).Count() > 0)
        {
            var g = new ContextFreeGrammar(this);
            var newStartSymbol = g.GetNewSymbol(startSymbol);
            Report($"Abstracting start symbol {newStartSymbol} -> {startSymbol}", 2);
            g.Prod(newStartSymbol, new [] {startSymbol}, true);
            return g;
        }
        else
        {
            Report($"No need to abstract {startSymbol}", 2);
            return this;
        }
    }

    public ContextFreeGrammar IsolateTerminals()
    {
        Report("Isolating mixed term/nonterm productions", 1);

        var g = new ContextFreeGrammar(this);

        // For each rule A->a (a is terminal), record {a:A}. Maps a unitary terminal
        // production's terminal to the nonterminal that owns it.
        var terminalsToNonterminals = new Dictionary<string, string>();

        // gather up the unitary terminal productions we already have
        /*
        foreach (var (nonterm, idx, prodRule) in
            (from prod in g.produtions.Productions
             where prod.rules.Count() > 0
                && prod.rules[0].derivation.Count() == 1
                && terms.Contains(prod.rules[0].derivation[0])
                && terminalsToNonterminals.ContainsKey(prod.rules[0].derivation[0]) == false)
            { terminalsToNonterminals.Add(terminal, nonterm); }
        */
        // TODO: Rewrite this trash -- with the above?
        foreach (var (nonterm, idx, prodRule) in g.productions.Productions)
        {
            if (g.productions.ProductionOf(nonterm).rules.Count() > 1)
                { continue; }
            var terminal = prodRule.derivation[0];
            if (prodRule.derivation.Count() == 1
             && terminals.Contains(terminal)
             && terminalsToNonterminals.ContainsKey(terminal) == false)
                { terminalsToNonterminals.Add(terminal, nonterm); }
        }

        var removals = new List<(string, int)>();
        var adds = new List<(string, List<string>)>();

        foreach (var (nonterm, idx, prodRule) in g.productions.Productions)
        {
            var (numTerms, numNonterms) = g.CountDerivationSymbols(prodRule.derivation);
            if (numTerms > 0)
            {
                if (numTerms == 1 && numNonterms == 0)
                    { continue; }   // we don't have to isolate unitary terminals

                Report($"Isolating: {nonterm}:{idx} -> {string.Join(' ', prodRule.derivation)}", 2);

                // split up rules like aA and Aa and ab
                var newDerivation = new List<string>();
                foreach (var s in prodRule.derivation)
                {
                    if (terminals.Contains(s))
                    {
                        if (terminalsToNonterminals.ContainsKey(s))
                        {
                            newDerivation.Add(terminalsToNonterminals[s]);
                        }
                        else
                        {
                            var newNonterm = g.GetNewSymbol(s);
                            adds.Add((newNonterm, new List<string>(new [] { s } ) ));

                            terminalsToNonterminals[s] = newNonterm;
                            newDerivation.Add(newNonterm);
                        }
                    }
                    else
                    {
                        newDerivation.Add(s);
                    }
                }
                removals.Add((nonterm, idx));
                adds.Add((nonterm, newDerivation));
            }
        }

        // remove the bad'ns
        removals.Reverse();
        foreach (var (nonterm, idx) in removals)
        {
            g.productions.Remove(nonterm, idx);
        }
        // add the good'ns
        foreach (var (nonterm, deriv) in adds)
        {
            g.Prod(nonterm, deriv);
        }
        return g;
    }

    public ContextFreeGrammar ReduceRulesToPairs()
    {
        Report("Reducting productions with > 2 symbols", 1);

        var g = new ContextFreeGrammar(this);

        var removals = new List<(string, int)>();
        var adds = new List<(string, List<string>)>();

        foreach (var (nonterm, idx, prodRule) in g.productions.Productions)
        {
            var numSymbols = prodRule.derivation.Count();
            if (numSymbols > 2)
            {
                Report($"Splitting: {nonterm}:{idx} -> {string.Join(' ', prodRule.derivation)}", 2);

                var der = prodRule.derivation.ToList();
                removals.Add((nonterm, idx));
                while (der.Count() > 2)
                {
                    var s0 = der[der.Count() - 2];
                    var s1 = der[der.Count() - 1];
                    var newNonterm = g.GetNewSymbol(nonterm);
                    adds.Add((newNonterm, new List<string>(new [] {s0, s1})));
                    der = der.Take(der.Count() - 2).ToList();
                    der.Add(newNonterm);
                    /*
                    var s0 = der[0];
                    var s1 = der[1];
                    var newNonterm = g.GetNewSymbol(nonterm);
                    adds.Add((newNonterm, new List<string>(new [] {s0, s1})));
                    der = der.Skip(1).ToList();
                    der[0] = newNonterm;
                    */
                }

                adds.Add((nonterm, der));
            }
        }

        // remove the bad'ns
        removals.Reverse();
        foreach (var (nonterm, idx) in removals)
        {
            g.productions.Remove(nonterm, idx);
        }
        // add the good'ns
        foreach (var (nonterm, deriv) in adds)
        {
            g.Prod(nonterm, deriv);
        }

        return g;
    }

    public ContextFreeGrammar ToChomskyNormalForm()
    {
        Report("Transforming to CNF", 1);

        return AbstractifyStartSymbol()
              .EliminateNullProductions()
              .EliminateUnitProductions()
              .EliminateUselessProductions()
              .IsolateTerminals()
              .ReduceRulesToPairs();
    }

    public ContextFreeGrammar EliminateCycles()
    {
        // currently my needs are met by EliminateUnitProductions
        return this;
    }

    private void EliminateImmediateLeftRecursion(string nonterm)
    {
        Report($" - Eliminating immediate left-recursion for {nonterm}", 1);

        //  separate rules of nonterm into two groups:
        //  M = {immediately left-recursive prods A->Ax}, N = {all other prods A->y}
        var p = productions.ProductionOf(nonterm);
        var M = new List<int>();
        var N = new List<int>();

        foreach (var (rule, ruleIdx) in p.rules.WithIdxs())
        {
            if (rule.derivation[0] == nonterm)
                { M.Add(ruleIdx); }
            else
                { N.Add(ruleIdx); }
        }

        // No left-recursion found. Bail.
        if (M.Count() == 0)
            { return; }

        var adds = new List<(string, List<string>)>();
        var removals = new List<(string, int)>();

        //  Remove all nonterm's rules.
        foreach (var (rule, ruleIdx) in p.rules.WithIdxs())
            { removals.Add((nonterm, ruleIdx)); }

        // to be replaced by:

        //  A_0 = newNonterm()
        var newNonterm = GetNewSymbol(nonterm);

        //  for each A->y in N:
        //          prod( A, [*y A_0] )
        foreach (var n in N)
        {
            adds.Add((nonterm, productions.DerivationsOf(nonterm, n).ToList().Concat(new [] { newNonterm }).ToList()));
        }

        //  for each A->Ax in M:
        //          prod( A_0, [*x A_0] )
        foreach (var m in M)
        {
            adds.Add((newNonterm, productions.DerivationsOf(nonterm, m).Skip(1).ToList().Concat(new [] { newNonterm }).ToList()));
        }

        //  prod( A_0, [string.Empty] )
        adds.Add((newNonterm, new List<string>(new [] { string.Empty } )));

        // remove the bad'ns
        removals.Reverse();
        foreach (var (nonterm_r, idx_r) in removals)
        {
            productions.Remove(nonterm_r, idx_r);
        }
        // add the good'ns
        foreach (var (nonterm_a, deriv) in adds)
        {
            Prod(nonterm_a, deriv);
        }
    }

    public ContextFreeGrammar EliminateLeftRecursion()
    {
        Report($"Eliminating left-recursion", 1);

        var g = new ContextFreeGrammar(this);

        var adds = new List<(string, List<string>)>();
        var removals = new List<(string, int)>();

        var allNonterminals = g.Nonterminals.ToArray();
        for (int i = 0; i < allNonterminals.Length; ++i)
        {
            adds.Clear();
            removals.Clear();
            string ai = allNonterminals[i];
            var aip = g.productions.ProductionOf(ai);

            for (int j = 0; j < i; ++j)
            {
                string aj = allNonterminals[j];
                var ajp = g.productions.ProductionOf(aj);
                foreach (var (rule_i, ruleIdx_i) in aip.rules.WithIdxs())
                {
                    if (rule_i.derivation[0] == aj)
                    {
                        removals.Add((ai, ruleIdx_i));
                        var aid_tail = rule_i.derivation.Skip(1).ToList();
                        foreach (var (rule_j, ruleIdx_j) in ajp.rules.WithIdxs())
                        {
                            var ajd = rule_j.derivation.ToList().Concat(aid_tail).ToList();
                            adds.Add((ai, ajd));
                        }
                    }
                }
            }

            // remove the bad'ns
            removals.Reverse();
            foreach (var (nonterm, idx) in removals)
            {
                g.productions.Remove(nonterm, idx);
            }
            // add the good'ns
            foreach (var (nonterm, deriv) in adds)
            {
                g.Prod(nonterm, deriv);
            }

            // NOTE: If this can create new recursive prods, we're currently not
            // including them in allTerminals. NEED TO CHECK and make sure this
            // is correct behavior.
            g.EliminateImmediateLeftRecursion(ai);
        }

        return g;
    }

    public ContextFreeGrammar LeftFactor()
    {
        Report($"Left-factor", 1);

        var g = new ContextFreeGrammar(this);

        //  foreach production A -> x0 | x1 | x2 ... xn
        //      Find each longest sequence of symbols [s0 s1 s2 ... sm0]0, [sm1]1, [sm2]2, .. [smp]p that
        //      begin two or more RHSs of A, and mp is >= 1.
        //      Make each []p a new nonterminal's prod, and replace all the sequences in A with the new NT

        //  Question: Do we prefer to sub a sequence of two that matches three alts, or a sequence of
        //  three that matches two alts and leaves the third unmatched?
        //      A -> ABCDE | ABCFG | ABHIJ | KLM | NO
        //  Should we replace AB:       + 1
        //      A -> ABX | KLM | MO
        //      X -> CDE | CFG | HIJ
        //      =>
        //      A -> ABX | KLM | MO
        //      X -> CY | HIJ
        //      Y -> DE | FG
        //  or ABC:
        //      A -> ABCX | ABHIJ | KLM | MO
        //      X -> DE | FG
        //      =>
        //      A -> ABY | KLM | MO
        //      X -> DE | FG
        //      Y -> CX | HIJ
        //  I don't think it matters.

        var adds = new List<(string, List<string>)>();
        var removals = new List<(string, int)>();

        var tracking = new HashSet<string>(g.Nonterminals);

        while (tracking.Count() > 0)
        {
            var allNonterminals = tracking.ToArray();
            tracking.Clear();

            bool lastProdHadChanges = false;
            for (int i = 0; i < allNonterminals.Length; ++i)
            {
                // if the last thing we did made babies, we need to go over it again
                if (lastProdHadChanges)
                {
                    lastProdHadChanges = false;
                    i -= 1;
                }

                adds.Clear();
                removals.Clear();
                string ai = allNonterminals[i];
                var aip = g.productions.ProductionOf(ai);

                for (int r = 0; r < aip.rules.Count(); ++r)
                {
                    var ard = aip.rules[r].derivation;

                    Report($"Reference: {ai} -> {string.Join(' ', ard)}", 2);

                    int bestNumberOfMatchesSoFar = 0;
                    int lengthOfMatch = ard.Count();    // comparing against a maximum

                    // count the number of common beginning symbols
                    for (int s = r + 1; s < aip.rules.Count(); ++s)
                    {
                        var asd = aip.rules[s].derivation;

                        Report($"Checkinag against: {ai} -> {string.Join(' ', asd)}", 2);

                        int numMatches = 0;
                        for (int m = 0; m < Math.Min(ard.Count(), asd.Count()); ++m)
                        {
                            if (ard[m] == asd[m])
                                { numMatches += 1; }
                            else
                                { break; }
                        }

                        Report($" - {numMatches} matches", 2);

                        if (numMatches > 0)
                        {
                            bestNumberOfMatchesSoFar += 1;
                            // min - prefer shorter sequences that begin more productions
                            // doesn't really matter, but this is easy
                            lengthOfMatch = Math.Min(lengthOfMatch, numMatches);

                            Report($" - New best length: {lengthOfMatch} matches", 2);
                        }
                    }

                    if (bestNumberOfMatchesSoFar > 0)
                    {
                        lastProdHadChanges = true;

                        Report($"Abstracting sequence: {ai} -> {string.Join(' ', ard.Take(lengthOfMatch))}", 2);

                        var newNonterm = g.GetNewSymbol(ai);

                        // add new nonterm to tracking
                        tracking.Add(newNonterm);

                        removals.Add((ai, r));
                        var newDer = ard.Take(lengthOfMatch).ToList();
                        newDer.Add(newNonterm);
                        adds.Add((ai, newDer));

                        Report($" - Remove: {ai} -> {string.Join(' ', ard)}", 2);
                        Report($" - Add: {ai} -> {string.Join(' ', newDer)}", 2);

                        newDer = ard.Skip(lengthOfMatch).ToList();
                        if (newDer.Count() == 0)
                            { newDer.Add(string.Empty); }
                        adds.Add((newNonterm, newDer));

                        Report($" - Add: {newNonterm} -> {string.Join(' ', newDer)}", 2);

                        // replace the abstracted derivations
                        for (int s = r + 1; s < aip.rules.Count(); ++s)
                        {
                            var asd = aip.rules[s].derivation;
                            int numMatches = 0;
                            for (int m = 0; m < Math.Min(lengthOfMatch, asd.Count()); ++m)
                            {
                                if (asd[m] == ard[m])
                                    { numMatches += 1; }
                            }
                            if (numMatches == lengthOfMatch)
                            {
                                removals.Add((ai, s));
                                newDer = asd.Skip(numMatches).ToList();
                                if (newDer.Count() == 0)
                                    { newDer.Add(string.Empty); }

                                adds.Add((newNonterm, newDer));
                                Report($" - Remove: {ai} -> {string.Join(' ', asd)}", 2);
                                Report($" - Add: {newNonterm} -> {string.Join(' ', newDer)}", 2);
                            }
                        }

                        // remove the bad'ns
                        removals.Reverse();
                        foreach (var (nonterm, idx) in removals)
                        {
                            g.productions.Remove(nonterm, idx);
                        }
                        // add the good'ns
                        foreach (var (nonterm, deriv) in adds)
                        {
                            g.Prod(nonterm, deriv);
                        }
                    }
                }
            }
        }

        return g;
    }
}
