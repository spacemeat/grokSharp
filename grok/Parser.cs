using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Specialized;
using System.Diagnostics;

﻿namespace grok;

using ParseTableTermEntry =
    List<int>;

using ParseTableNontermEntry =
    Dictionary<
        string,
        List<int>
    >;

using ParseTable_pvt =
    Dictionary<
        string,
        Dictionary<
            string,
            List<int>
        >
    >;

public class Node
{
    public string Label { get; private set; }
    private List<Node> children = new List<Node>();

    public IEnumerable<Node> Children => from ch in children select ch;

    public Node(string label, bool terminal)
    {
        Label = label;
    }

    public void AddNode(Node newNode)
    {
        children.Add(newNode);
    }
}

public class ParseTable
{
    ParseTable_pvt pt = new ParseTable_pvt();

    public void Add(string nonterm, string inputTerm, int ruleIdx)
    {
        ParseTableNontermEntry? ptne;
        if (pt.TryGetValue(nonterm, out ptne) == false)
        {
            ptne = new ParseTableNontermEntry();
            pt.Add(nonterm, ptne);
        }

        ParseTableTermEntry? ptte;
        if (ptne.TryGetValue(inputTerm, out ptte) == false)
        {
            ptte = new ParseTableTermEntry();
            ptne.Add(inputTerm, ptte);
        }

        ptte.Add(ruleIdx);
    }

    public IEnumerable<int> Get(string nonterm, string inputTerm) =>
        from i in pt[nonterm][inputTerm] select i;

    public string ToString(ContextFreeGrammar g)
    {
        var sb = new StringBuilder();
        foreach (var ptkv in pt)
        {
            sb.Append($"{ptkv.Key}: ");
            foreach (var ptnekv in ptkv.Value)
            {
                sb.Append($"{ptnekv.Key}: ");
                foreach (var i in ptnekv.Value)
                {
                    sb.Append($" {ptkv.Key} -> {string.Join(' ', g.DerivationsOf(ptkv.Key, i))}");
                }
                sb.Append("; ");
            }
            sb.Append('\n');
        }

        return sb.ToString();
    }
}



public class Parser
{
    Lexer lexer;
    ContextFreeGrammar grammar;

    public Parser(Lexer lexer, ContextFreeGrammar grammar)
    {
        this.lexer = lexer;
        this.grammar = grammar;
    }

    public Node Parse(string text)
    {
        // tokenize
        var tokens = this.lexer.GenerateTokens(text);

        // build parse tree
        return Parse_impl(tokens);
    }

    protected virtual Node Parse_impl(IEnumerable<Token> tokens)
    {
        throw new Exception("Use a Parser-derived class.");
    }
}

public class TopDownParser : Parser
{
    public TopDownParser(Lexer lexer, ContextFreeGrammar grammar) : base(lexer, grammar)
    {

    }

    protected override Node Parse_impl(IEnumerable<Token> tokens)
    {
        throw new Exception("Not impl");
    }
}

public class RecursiveDescentParser : TopDownParser
{
    public RecursiveDescentParser(Lexer lexer, ContextFreeGrammar grammar) : base(lexer, grammar)
    {

    }

    protected override Node Parse_impl(IEnumerable<Token> tokens)
    {
        throw new Exception("Not impl");
    }
}


public class PredictiveParser : RecursiveDescentParser
{
    ParseTable parseTable = new ParseTable();
    readonly Lexer lexer;
    readonly ContextFreeGrammar grammar;

    public PredictiveParser(Lexer lexer, ContextFreeGrammar grammar) : base(lexer, grammar)
    {
        this.lexer = lexer;
        this.grammar = grammar;

        foreach (var (nonterm, ruleIdx, rule) in grammar.Productions)
        {
            var f = grammar.First(rule.derivation);

            foreach (var sym in f)
            {
                if (grammar.Terminals.Contains(sym))
                {
                    parseTable.Add(nonterm, sym, ruleIdx);
                }
            }

            if (f.Contains(string.Empty))
            {
                foreach (var b in grammar.Follow(nonterm))
                {
                    if (grammar.Terminals.Contains(b))
                    {
                        parseTable.Add(nonterm, b, ruleIdx);
                    }
                }

                if (grammar.Follow(nonterm).Contains(grammar.EofString))
                {
                    parseTable.Add(nonterm, grammar.EofString, ruleIdx);
                }
            }
        }
    }

    public string PrintParseTable() => parseTable.ToString(grammar);

    protected override Node Parse_impl(IEnumerable<Token> tokens)
    {
        throw new Exception("Not impl");
    }
}

public class BottomUpParser : Parser
{
    public BottomUpParser(Lexer lexer, ContextFreeGrammar grammar) : base(lexer, grammar)
    {

    }

    protected override Node Parse_impl(IEnumerable<Token> tokens)
    {
        throw new Exception("Not impl");
    }
}
