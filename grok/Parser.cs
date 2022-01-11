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
    public IEnumerable<Node> Children => from ch in children select ch;

    List<Node> children = new List<Node>();

    public Node(string label)
    {
        Label = label;
    }

    public void AddNode(Node newNode)
    {
        children.Add(newNode);
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        ToString_rec(sb, 0);
        return sb.ToString();
    }

    void ToString_rec(StringBuilder sb, int depth)
    {
        sb.Append($"{new string(' ', depth)}{Label}\n");

        foreach (var ch in Children)
        {
            ch.ToString_rec(sb, depth + 1);
        }
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

    /*
    public IEnumerable<int> Get(string nonterm, string inputTerm) =>
        from i in pt[nonterm][inputTerm] select i;
    */
    public IEnumerable<int> Get(string nonterm, string inputTerm)
    {
        Console.WriteLine($"Trying GET({nonterm}, {inputTerm})");
        ParseTableNontermEntry? ptne;
        if (pt.TryGetValue(nonterm, out ptne))
        {
            ParseTableTermEntry? ptte;
            if (ptne.TryGetValue(inputTerm, out ptte))
            {
                return from i in ptte select i;
            }
            else
            {
                throw new Exception($"No entry for input term {inputTerm}");
            }
        }
        else
        {
            throw new Exception($"No entry for nonterm {nonterm}");
        }
    }


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

    protected override Node Parse_impl(IEnumerable<Token> tokenStream)
    {
        IEnumerator<Token> tokens = tokenStream.GetEnumerator();

        var stack = new Stack<string>();
        stack.Push(grammar.StartSymbol);

        var nodeStack = new Stack<Node>();
        nodeStack.Push(new Node(grammar.StartSymbol));
        var topNode = nodeStack.Peek();

        tokens.MoveNext();
        bool eofReached = false;

        while (stack.Count() > 0)
        {
            var X = stack.Peek();
            var n = nodeStack.Peek();

            string a = grammar.EofString;
            if (eofReached == false)
                { a = tokens.Current.Terminal; }

            if (grammar.Terminals.Contains(X))
            {
                if (X == a)
                {
                    stack.Pop();
                    nodeStack.Pop();
                    eofReached = ! tokens.MoveNext();
                }
                else
                {
                    throw new Exception($"Error: Expected terminal {X}, found {a}");
                }
            }

            else
            {
                int [] rules = parseTable.Get(X, a).ToArray();
                if (rules.Length > 0)
                {
                    stack.Pop();
                    nodeStack.Pop();
                    // TODO: Choose an ambiguous rule
                    int rule = rules[0];
                    var derivation = grammar.DerivationsOf(X, rule);
                    if (derivation.Count() != 1 || derivation.First() != string.Empty)
                    {
                        var newNodes = (from s in derivation select new Node(s)).ToArray();
                        foreach (var nn in newNodes)
                            { n.AddNode(nn); }
                        foreach (var nn in newNodes.Reverse())
                        {
                            stack.Push(nn.Label);
                            nodeStack.Push(nn);
                        }
                    }
                }
                else
                    { throw new Exception($"Error: Bad syntax: {a}"); }
            }
        }

        return topNode;
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
