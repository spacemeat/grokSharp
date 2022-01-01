using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Specialized;
using System.Diagnostics;

﻿namespace grok;

public class Parser
{
    Lexer lexer;
    ContextFreeGrammar grammar;

    public Parser(Lexer lexer, ContextFreeGrammar grammar)
    {
        this.lexer = lexer;
        this.grammar = grammar;
    }
}

public class TopDownParser : Parser
{
    public TopDownParser(Lexer lexer, ContextFreeGrammar grammar) : base(lexer, grammar)
    {

    }
}

public class RecursiveDescentParser : TopDownParser
{
    public RecursiveDescentParser(Lexer lexer, ContextFreeGrammar grammar) : base(lexer, grammar)
    {

    }

}

public class BottomUpParser : Parser
{
    public BottomUpParser(Lexer lexer, ContextFreeGrammar grammar) : base(lexer, grammar)
    {

    }

}
