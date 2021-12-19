using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Specialized;
using System.Diagnostics;

﻿namespace grok_lib;

public static class EnumHelpers
{
    public static IEnumerable < (T item, int idx) > WithIdxs<T>(this IEnumerable<T> obj)
    {
        return obj.Select((obj, idx) => (obj, idx));
    }
}

public class OrderedStringDictionary<V> //where K: notnull
{
    List<(string key, V value)> entries = new List<(string key, V value)>();
    Dictionary<string, List<int>> keys = new Dictionary<string, List<int>>();

    public void Add(string key, V value)
    {
        int c = entries.Count;
        entries.Add((key, value));

        List<int>? vl;
        if (keys.TryGetValue(key, out vl) == false)
        {
            vl = new List<int>();
            keys.Add(key, vl);
        }
        vl.Add(c);
    }

    public IEnumerable<V> this[string key] =>
        from idx in keys[key]
        select entries[idx].value;

    public IEnumerable<string> Keys => from kvp in keys select kvp.Key;

    public bool TryGetValue(string key, out V[] values)
    {
        List<int>? idxs;
        if (keys.TryGetValue(key, out idxs))
            { values = (from idx in idxs select entries[idx].value).ToArray(); return true; }
        values = new V[] {};
        return false;
    }

    public IEnumerable<(string key, V value)> Entries => from e in entries select e;

    /*
    public IEnumerable<(string key, V[] value)> CollatedEntries =>
        from (key, idxs) in keys
        select (key, (from idx in idxs select entries[idx].value).ToArray());
        */
}

public struct Token
{
    public string Terminal;
    public int TerminalRuleIdx; // usually 0
    public int Address;
    public string Value;
}

public abstract class Lexer
{
    public virtual string [] Terminals => new string []{};

    public IEnumerable<Token> GenerateTokens(string src)
    {
        return GenerateTokens_imp(src);
    }

    protected abstract IEnumerable<Token> GenerateTokens_imp(string src);
}

public class RegexLexer : Lexer
{
    OrderedStringDictionary<(string pattern, Regex regex)> lexRules
        = new OrderedStringDictionary<(string pattern, Regex regex)>();

    public void Lex(string name, string pattern)
    {
        lexRules.Add(name, (pattern, new Regex(pattern, RegexOptions.Compiled)));
    }

    public override string [] Terminals => lexRules.Keys.ToArray();

    protected override IEnumerable<Token> GenerateTokens_imp(string src)
    {
        // Here we tokenize src into lexemes.
        // I just feel cool using the word 'lexeme.'
        int backCur = 0;
        while(backCur < src.Length)
        {
            string bestMatchTerminal = string.Empty;
            int bestMatchTerminalRuleIdx = -1;
            int bestMatchLen = 0;
            string bestMatchStr = string.Empty;

            foreach (var (rule, idx) in lexRules.Entries.WithIdxs())
            {
                // rules is (string, (string pattern, Regex regex))
                var terminal = rule.key;
                bestMatchTerminalRuleIdx = -1;
                var re = rule.value.regex;
                if (re == null)
                    { continue; }
                MatchCollection matches = re.Matches(src, backCur);
                if (matches == null)
                    { continue; }
                foreach (Match match in matches)
                {
                    var len = match.Groups[0].Length;
                    if (len > bestMatchLen && match.Groups[0].Index == backCur)
                    {
                        bestMatchTerminal = terminal;
                        bestMatchTerminalRuleIdx = idx;
                        bestMatchLen = len;
                        bestMatchStr = match.Groups[0].Value;
                    }
                }
            }

            if (bestMatchTerminal == string.Empty)
                { throw new Exception($"({backCur}): syntax error: No matching lexemes"); }

            backCur += bestMatchLen;

            //Console.WriteLine($"{Terminals[bestMatch].Name} - value = \"{bestMatchStr}\" - backCur = {backCur}");

            yield return new Token {
                Terminal = bestMatchTerminal,
                TerminalRuleIdx = bestMatchTerminalRuleIdx,
                Address = backCur,
                Value = bestMatchStr };

            if (backCur > src.Length)
                { throw new Exception("Fatality!"); }
        }
    }
}

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
}

public class Production
{
    public string nonterminal = string.Empty;
    public List<ProdRule> rules = new List<ProdRule>();

    public void AddProduction(IEnumerable<string> derivation)
    {
        if (nonterminal == "B")
        {
            Console.WriteLine($"Adding B -> {string.Join(' ', derivation)} ({derivation.Count()} members)");
        }
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
}

public class ProductionSet
{
    List<Production> productions = new List<Production>();
    Dictionary<string, int> productionIndices = new Dictionary<string, int>();

    public IEnumerable<string> Nonterminals => from p in productions select p.nonterminal;

    public IEnumerable<(string nonterminal, int idx, ProdRule prodRule)> Productions =>
        from p in productions
        from di in p.rules.WithIdxs()
        select (p.nonterminal, di.idx, di.item);

    /* - Haven't tested, may not need it, it's lousy perf
    public IEnumerable<(string nonterminal, int idx, ProdRule prodRule)> ProductionsTopDown(string startSymbol, IEnumerable<string> terminals)
    {
        var nontermsYetToBeYielded = new Stack<string>();
        nontermsYetToBeYielded.Push(startSymbol);
        var yielded = new HashSet<string>(terminals);
        yielded.Add(startSymbol);

        while (nontermsYetToBeYielded.Count() > 0)
        {
            string nonterm = nontermsYetToBeYielded.Pop();

            foreach (var (nonterminal, idx, prodRule) in
                (from di in productions[productionIndices[nonterm].rules.WithIdxs()
                 select (p.nonterminal, di.idx, di.item)))
            {
                yield (nonterminal, idx, prodRule);
                yielded.Add(nonterminal);
                foreach (var s in di.item.derivative)
                {
                    if (yielded.Contains(s) == false)
                        { nontermsYetToBeYielded.Push(s); }
                }
            }
        }
    }
    */

    public IEnumerable<(string nonterminal, int idx, ProdRule prodRule)> ProductionsOf(string nonterminal) =>
        from di in productions[productionIndices[nonterminal]].rules.WithIdxs()
        select (nonterminal, di.idx, di.item);

    public Production ProductionOf(string nonterminal) => productions[productionIndices[nonterminal]];

    public IEnumerable<string> DerivationsOf(string nonterminal, int ruleIdx) => ProductionOf(nonterminal).rules[ruleIdx].derivation;

    public (string nonterminal, int idx) FirstNonterminalWithEmptyRule =>
        (from p in productions
         from di in p.rules.WithIdxs() where (di.item.derivation.Count() == 1
                                           && di.item.derivation[0] == string.Empty)
         select (p.nonterminal, di.idx)).FirstOrDefault((string.Empty, -1));

    public (string nonterminal, int idx) FirstNonterminalWithUnitRule =>
        (from p in productions
         from di in p.rules.WithIdxs() where (di.item.derivation.Count() == 1
                                           && Nonterminals.Contains(di.item.derivation[0]))
         select (p.nonterminal, di.idx)).FirstOrDefault((string.Empty, -1));

    public IEnumerable<(string nonterminal, int idx, ProdRule prodRule)> UnitRules =>
        from p in productions                                                               // A -> {x1|x2|x3}
        from di in p.rules.WithIdxs() where (di.item.derivation.Count() == 1                // unitary prod xn == B
                                          && Nonterminals.Contains(di.item.derivation[0]))  // that is a nonterminal
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

    public ProductionSet Clone()
    {
        var ps = new ProductionSet();
        ps.productions = (from p in productions select p.Clone()).ToList();
        ps.productionIndices = new Dictionary<string, int>(productionIndices);
        return ps;
    }

    public void Add(string nonterminal, IEnumerable<string> derivation)
    {
        Console.WriteLine($"ADD RULE: {nonterminal} -> {string.Join(' ', derivation)}");

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

            Console.WriteLine($"REMOVE RULE {nonterminal} -> {string.Join(' ', prod.rules[idx].derivation)}");

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

    public string GenerateBnf(string startSymbol = "")
    {
        var sb = new StringBuilder();

        int maxNonterminalNameLen = (from t in Nonterminals select t.Length).Max();

        if (startSymbol != string.Empty)
            { sb.Append($"Start symbol: {startSymbol}\n\n"); }

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

public class Grammar
{
    Lexer lexer;
    ProductionSet productions = new ProductionSet();
    string startSymbol = string.Empty;

    public Grammar(Lexer lexer)
    {
        this.lexer = lexer;
    }

    public Grammar(Lexer lexer, Grammar template)
    {
        this.lexer = lexer;
        this.productions = template.productions.Clone();
        this.startSymbol = template.startSymbol;
    }

    public Grammar(Grammar template)
    {
        this.lexer = template.lexer; // TODO: Clone lexer in ctrs
        this.productions = template.productions.Clone();
        this.startSymbol = template.startSymbol;
    }

    public void Prod(string nonterminal, IEnumerable<string> derivation, bool startSymbol = false)
    {
        productions.Add(nonterminal, derivation);
        if (startSymbol)
        {
            this.startSymbol = nonterminal;
            productions.MoveToFront(this.startSymbol);
        }
    }

    public string StartSymbol => startSymbol;

    public IEnumerable<string> Terminals => lexer.Terminals;

    public IEnumerable<string> Nonterminals => productions.Nonterminals;

    public string GenerateBnf()
    {
        return productions.GenerateBnf(startSymbol);
    }

    public IEnumerable<Token> GenerateTokens(string src)
    {
        return lexer.GenerateTokens(src);
    }

    public IEnumerable<string> CheckIntegrity()
    {
        // empty means no errors
        // TODO: Do some errors
        return new string[] {};
    }

    public Grammar ReduceBottomUp()
    {
        Grammar g = new Grammar(this);
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

    public Grammar ReduceTopDown()
    {
        Grammar g = new Grammar(lexer);

        HashSet<string> W = new HashSet<string>();
        HashSet<string> wToAdd = new HashSet<string>();
        HashSet<string> nontermsToFind = new HashSet<string>();

        Console.WriteLine($"start symbol: {startSymbol}");
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
                    Console.WriteLine($"Adding rule: {nonterminal} -> {string.Join(' ', rule.derivation)}");
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

    public Grammar Reduce()
    {
        Grammar g = ReduceBottomUp();
        return g.ReduceTopDown();
    }

    public Grammar EliminateUnitProductions()
    {
        Grammar g = new Grammar(this);

        // find all unit pairs
        var unitRules = g.productions.UnitRules;
        var addedProds = new List<(string nonterminal, string [] derivation)>();

        // add all nonunit productions as appropriate
        foreach (var (nonterm, ruleIdx, prodRule) in unitRules)
        {
            string unitProd = prodRule.derivation[0];
            if (unitProd == nonterm)
                { continue; }       // Do not copy my own rules to myself. I have them already.

            Console.WriteLine($"Unit production: {nonterm}:{ruleIdx} -> {unitProd}");

            foreach (var (nontermRep, ruleIdxRep, prodRuleRep) in g.productions.ProductionsOf(unitProd))
            {
                Console.WriteLine($"Writing rule {nonterm} -> {string.Join(' ', prodRuleRep.derivation)}");
                addedProds.Add((nonterm, prodRuleRep.derivation.ToArray()));
            }
        }

        foreach (var (nonterm, deriv) in addedProds)
            { g.Prod(nonterm, deriv); }

        // remove all unit productions of any kind
        // Run this op again because the above may have added new unit rules. (They're still removable though.)
        var unitRulesArr = g.productions.UnitRules.ToList();
        unitRulesArr.Reverse();    // removing several rules from a prod, we do so in reverse order so the indices
                                   // of future removals don't get out of match
        var s = string.Join('\n',
                            from prod in unitRulesArr
                            select $"{prod.nonterminal}:{prod.idx} -> " + string.Join(' ', prod.prodRule.derivation));
        Console.WriteLine($"REMOVING: {s}");
        foreach (var (nonterm, ruleIdx, prodRule) in unitRulesArr)
        {
            g.productions.Remove(nonterm, ruleIdx);
        }

        /*
        var (nonterm, ruleIdx) = g.productions.FirstNonterminalWithUnitRule;
        while (nonterm != string.Empty)
        {
            Console.WriteLine($"Unit production: {nonterm}:{ruleIdx}");

            var addedProds = new List<(string nonterminal, string [] derivation)>();

            string [] deriv = g.productions.DerivationsOf(nonterm, ruleIdx).ToArray();
            string? unitProd = null;
            if (deriv.Length == 1)
                { unitProd = deriv[0]; }

            if (unitProd == null)
                { throw new Exception($"what fuckery is this?"); }

            // if S -> S, just kill it dead; otherwise, S -> A, so add S -> {all of A's productions}
            if (unitProd != nonterm)
            {
                foreach (var (nontermRep, ruleIdxRep, prodRuleRep) in g.productions.ProductionsOf(unitProd))
                {
                    Console.WriteLine($"Writing rule {nonterm} -> {string.Join(' ', prodRuleRep.derivation)}");
                    addedProds.Add((nonterm, prodRuleRep.derivation.ToArray()));
                }
            }

            g.productions.Remove(nonterm, ruleIdx);

            // add prods we created
            foreach (var ap in addedProds)
                { g.Prod(ap.nonterminal, ap.derivation, ap.nonterminal == startSymbol); }

            Console.WriteLine($"QUICK BNF: \n{g.GenerateBnf()}");
            //Console.ReadLine();

            (nonterm, ruleIdx) = g.productions.FirstNonterminalWithUnitRule;
        }
        */

        return g;
    }

    public Grammar EliminateNullProductions()
    {
        Grammar g = new Grammar(this);

        var (nonterm, ruleIdx) = g.productions.FirstNonterminalWithEmptyRule;
        while (nonterm != string.Empty)
        {
            Console.WriteLine($"Null production: {nonterm}:{ruleIdx}");
            if (nonterm == g.productions.Nonterminals.First())  // do not eliminate start->e
                { continue; }

            // eliminate the empty prod
            g.productions.Remove(nonterm, ruleIdx);

            var addedProds = new List<(string nonterminal, string [] derivation)>();

            // now visit every prod that references this nonterminal, and replace
            // them with versions with and without this nonterminal in the production
            foreach (var (nontermRep, ruleIdxRep, prodRuleRep) in g.productions.Productions)
            {
                int [] symRefs = prodRuleRep.SymbolReferences(nonterm).ToArray();
                if (symRefs.Length > 0)
                {
                    Console.WriteLine($"Found {nonterm} in production rule for {nontermRep}:{ruleIdxRep}: {string.Join(' ', symRefs)}");
                    //if (prodRuleRep.derivation.Count() == 1)
                    //    { continue; }   // do not add empty productions

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
                                {
                                    newRule.Add(si.item);
                                }
                                symRefCursor += 1;
                            }
                            else
                            {
                                newRule.Add(si.item);
                            }
                        }

                        // A unit nonterm which gets removed results in a null production. This will
                        // get pushed further out on each iteration of the outer while loop.
                        if (newRule.Count() == 0)
                            { newRule.Add(string.Empty); }

                        Console.WriteLine($"Writing rule {nontermRep} -> {string.Join(' ', newRule)} ({newRule.Count()} members)");
                        addedProds.Add((nontermRep, newRule.ToArray()));
                    }
                }
            }

            // add prods we created
            foreach (var ap in addedProds)
                { g.Prod(ap.nonterminal, ap.derivation); }

            (nonterm, ruleIdx) = g.productions.FirstNonterminalWithEmptyRule;
        }

        return g;
    }

    public string GetNewNonterminal(string baseName = "")
    {
        if (baseName == string.Empty)
            { baseName = "NewNonterminal"; }

        for (int i = 0; i < int.MaxValue; ++i)
        {
            var name = $"{baseName}_{i}";
            if (productions.ProductionsReferencing(name).Count() == 0)
            {
                return name;
            }
        }

        throw new Exception("Could not find a nonconflicting nonterminal name. Maybe I should have tried harder. But I didn't. womp womp");
    }

    public Grammar AbstractifyStartSymbol()
    {
        if (productions.ProductionsReferencing(startSymbol).Count() > 0)
        {
            var g = new Grammar(this);
            g.Prod(g.GetNewNonterminal(startSymbol), new [] {startSymbol}, true);
            return g;
        }
        else
        {
            return this;
        }
    }

    public Grammar EliminateCycles()
    {
        return this;
    }

    public Grammar EliminateImmediateLeftRecursion()
    {
        return this;
    }
}
