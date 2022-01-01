using grok;
using System.Text;


bool Test_humon()
{
    Console.WriteLine($"\n  ---------------- HUMON:\n");

    var l = new RegexLexer();

    l.Lex("whitespace",         @"(\s|,)+"                  , false);
    l.Lex("cStyleComment",      @"\/\*(.|\n)*?\*\/"         );
    l.Lex("cppStyleComment",    @"\/\/.*?$"                 );
    l.Lex("keyValueSeparator",  @":"                        );
    l.Lex("listBegin",          @"\["                       );
    l.Lex("listEnd",            @"]"                        );
    l.Lex("dictBegin",          @"\{"                       );
    l.Lex("dictEnd",            @"}"                        );
    l.Lex("annotationMark",     @"@"                        );
    l.Lex("word_squote",        @"'(.|\n)*?'"               );
    l.Lex("word_dquote",        @"""(.|\n)*?"""             );
    l.Lex("word_backquote",     @"`(.|\n)*?`"               );
    l.Lex("word_heredoc",       @"(\^(.|\n)*?\^)(.|\n)*\1"  );
    l.Lex("word",               @"[^\s\{\}\[\]\:,@]+?(?=(\/\*)|(\/\/)|[\s\{\}\[\]\:,@]|$)");

    var g = new ContextFreeGrammar(l.Terminals, 3);
    /**/

    g.Prod("trove",             new [] { "node" }, true );
    g.Prod("trove",             new [] { "annotation", "node" }, true );
    g.Prod("trove",             new [] { "annotation" }, true );
    //g.Prod("trove",             new [] { string.Empty }, true );
    g.Prod("node",              new [] { "list" } );
    g.Prod("node",              new [] { "list", "annotation" } );
    g.Prod("node",              new [] { "dict" } );
    g.Prod("node",              new [] { "dict", "annotation" } );
    g.Prod("node",              new [] { "value" } );
    g.Prod("node",              new [] { "value", "annotation" } );
    g.Prod("list",              new [] { "listBegin", "sequence", "listEnd" } );
    g.Prod("list",              new [] { "listBegin", "annotation", "sequence", "listEnd" } );
    g.Prod("dict",              new [] { "dictBegin", "keyNodeSequence", "dictEnd" } );
    g.Prod("dict",              new [] { "dictBegin", "annotation", "keyNodeSequence", "dictEnd" } );
    g.Prod("value",             new [] { "word_squote" } );
    g.Prod("value",             new [] { "word_dquote" } );
    g.Prod("value",             new [] { "word_backquote" } );
    g.Prod("value",             new [] { "word_heredoc" } );
    g.Prod("value",             new [] { "word" } );
    g.Prod("sequence",          new [] { "sequence", "node" } );
    g.Prod("sequence",          new [] { string.Empty } );
    g.Prod("keyNodeSequence",   new [] { "keyNodeSequence", "node" } );
    g.Prod("keyNodeSequence",   new [] { string.Empty } );
    g.Prod("keyNode",           new [] { "value", "keyValueSeparator", "node" } );
    g.Prod("keyNode",           new [] { "value", "annotation", "keyValueSeparator", "node" } );
    g.Prod("keyNode",           new [] { "value", "keyValueSeparator", "annotation", "node" } );
    g.Prod("keyNode",           new [] { "value", "annotation", "keyValueSeparator", "annotation", "node" } );
    g.Prod("annotation",        new [] { "annotationMark", "dictBegin", "keyValueSequence", "dictEnd" } );
    g.Prod("annotation",        new [] { "annotationMark", "keyValue" } );
    g.Prod("keyValueSequence",  new [] { "keyValueSequence", "keyValue" } );
    g.Prod("keyValueSequence",  new [] { string.Empty } );
    g.Prod("keyValue",          new [] { "value", "keyValueSeparator", "value" } );

    var src = @"{
        'foo': `bar`
        ""baz"": ^buz^biz^buz^
    }";

    Console.WriteLine($"\n  - BNF: \n{g}");

    Console.WriteLine($"\n  - Src:\n{src}");

    Console.WriteLine($"\n  - Tokens:");
    foreach (var token in l.GenerateTokens(src))
    {
        var sb = new StringBuilder();

        sb.Append($"{token.GetType().Name} ({token.Line}, {token.Column}): {token.Terminal}")
          .Append(new string(' ', 52 - Math.Min(sb.Length, 50)));
        if (token.Terminal != "whitespace")
            { sb.Append(token.Value); }
        Console.WriteLine(sb.ToString());
    }

    g = g.EliminateNullProductions();
    Console.WriteLine($"\n  - BNF: \n{g}");

    g = g.EliminateUnitProductions();
    Console.WriteLine($"\n  - BNF: \n{g}");

    g = g.IsolateTerminals();
    Console.WriteLine($"\n  - BNF: \n{g}");

    g = g.ReduceRulesToPairs();
    Console.WriteLine($"\n  - BNF: \n{g}");

    return true;
}


bool Test_aces()
{
    Console.WriteLine($"\n  ---------------- ACES:\n");

    var l = new RegexLexer();
    l.Lex("a", "a");
    l.Lex("c", "c");
    l.Lex("e", "e");

    var g = new ContextFreeGrammar(l.Terminals, 1);
    g.Prod("S", new [] { "A", "C" }, true );
    g.Prod("S", new [] { "B" }, true );
    g.Prod("A", new [] { "a" } );
    g.Prod("C", new [] { "c" } );
    g.Prod("C", new [] { "B", "C" } );
    g.Prod("E", new [] { "a", "A" } );
    g.Prod("E", new [] { "e" } );

    Console.WriteLine($"\n  - BNF: \n{g}");

    g = g.ReduceBottomUp();

    var gt = new ContextFreeGrammar(l.Terminals, 0);
    gt.Prod("S", new [] { "A", "C" }, true );
    gt.Prod("A", new [] { "a" } );
    gt.Prod("C", new [] { "c" } );
    gt.Prod("E", new [] { "a", "A" } );
    gt.Prod("E", new [] { "e" } );
    Console.WriteLine($"\n  - BNF: \n{g}");
    Console.WriteLine($"Test passed: {g.ToString() == gt.ToString()}");
    // should be: S -> AC; A -> a; C -> c; E -> aA | E;

    g = g.ReduceTopDown();
    gt = new ContextFreeGrammar(l.Terminals, 0);
    gt.Prod("S", new [] { "A", "C"}, true );
    gt.Prod("A", new [] { "a" } );
    gt.Prod("C", new [] { "c" } );
    Console.WriteLine($"\n  - BNF: \n{g}");
    Console.WriteLine($"Test passed: {g.ToString() == gt.ToString()}");
    // should be: S -> AC; A -> a; C -> c;

    return true;
}


bool Test_sab()
{
    Console.WriteLine($"\n  ---------------- SAB (https://www.geeksforgeeks.org/converting-context-free-grammar-chomsky-normal-form/):\n");

    var l = new RegexLexer();
    l.Lex("a", "a");
    l.Lex("b", "b");

    var g = new ContextFreeGrammar(l.Terminals, 1);
    g.Prod("S", new [] { "A", "S", "B" }, true );
    g.Prod("A", new [] { "a", "A", "S" } );
    g.Prod("A", new [] { "a" } );
    g.Prod("A", new [] { string.Empty } );
    g.Prod("B", new [] { "S", "b", "S" } );
    g.Prod("B", new [] { "A" } );
    g.Prod("B", new [] { "b", "b" } );

    Console.WriteLine($"\n  - BNF: \n{g}");

    g = g.AbstractifyStartSymbol();
    var gt = new ContextFreeGrammar(l.Terminals, 0);
    gt.Prod("S0", new [] { "S" }, true );
    gt.Prod("S", new [] { "A", "S", "B" } );
    gt.Prod("A", new [] { "a", "A", "S" } );
    gt.Prod("A", new [] { "a" } );
    gt.Prod("A", new [] { string.Empty } );
    gt.Prod("B", new [] { "S", "b", "S" } );
    gt.Prod("B", new [] { "A" } );
    gt.Prod("B", new [] { "b", "b" } );
    Console.WriteLine($"\n  - BNF: \n{g}");
    Console.WriteLine($"Test passed: {g.ToString() == gt.ToString()}\n");

    g = g.EliminateNullProductions();
    gt = new ContextFreeGrammar(l.Terminals, 0);
    gt.Prod("S0", new [] { "S" }, true );
    gt.Prod("S", new [] { "A", "S", "B" } );
    gt.Prod("S", new [] { "S", "B" } );
    gt.Prod("S", new [] { "A", "S" } );
    gt.Prod("S", new [] { "S" } );
    gt.Prod("A", new [] { "a", "A", "S" } );
    gt.Prod("A", new [] { "a" } );
    gt.Prod("A", new [] { "a", "S" } );
    gt.Prod("B", new [] { "S", "b", "S" } );
    gt.Prod("B", new [] { "A" } );
    gt.Prod("B", new [] { "b", "b" } );
    Console.WriteLine($"\n  - BNF: \n{g}");
    Console.WriteLine($"Test passed: {g.ToString() == gt.ToString()}\n");

    g = g.EliminateUnitProductions();
    gt = new ContextFreeGrammar(l.Terminals, 0);
    gt.Prod("S0", new [] { "A", "S", "B" }, true );
    gt.Prod("S0", new [] { "S", "B" } );
    gt.Prod("S0", new [] { "A", "S" } );
    gt.Prod("S", new [] { "A", "S", "B" } );
    gt.Prod("S", new [] { "S", "B" } );
    gt.Prod("S", new [] { "A", "S" } );
    gt.Prod("A", new [] { "a", "A", "S" } );
    gt.Prod("A", new [] { "a" } );
    gt.Prod("A", new [] { "a", "S" } );
    gt.Prod("B", new [] { "S", "b", "S" } );
    gt.Prod("B", new [] { "b", "b" } );
    gt.Prod("B", new [] { "a", "A", "S" } );
    gt.Prod("B", new [] { "a" } );
    gt.Prod("B", new [] { "a", "S" } );
    Console.WriteLine($"\n  - BNF: \n{g}");
    Console.WriteLine($"Test passed: {g.ToString() == gt.ToString()}\n");

    g = g.IsolateTerminals();
    gt = new ContextFreeGrammar(l.Terminals, 0);
    gt.Prod("S0", new [] { "A", "S", "B" }, true );
    gt.Prod("S0", new [] { "S", "B" } );
    gt.Prod("S0", new [] { "A", "S" } );
    gt.Prod("S", new [] { "A", "S", "B" } );
    gt.Prod("S", new [] { "S", "B" } );
    gt.Prod("S", new [] { "A", "S" } );
    gt.Prod("A", new [] { "a" } );
    gt.Prod("A", new [] { "a0", "A", "S" } );
    gt.Prod("A", new [] { "a0", "S" } );
    gt.Prod("B", new [] { "a" } );
    gt.Prod("B", new [] { "S", "b0", "S" } );
    gt.Prod("B", new [] { "b0", "b0" } );
    gt.Prod("B", new [] { "a0", "A", "S" } );
    gt.Prod("B", new [] { "a0", "S" } );
    gt.Prod("a0", new [] { "a" } );
    gt.Prod("b0", new [] { "b" } );
    Console.WriteLine($"\n  - BNF: \n{g}");
    Console.WriteLine($"Test passed: {g.ToString() == gt.ToString()}\n");

    g = g.ReduceRulesToPairs();
    gt = new ContextFreeGrammar(l.Terminals, 0);
    gt.Prod("S0", new [] { "S", "B" }, true );
    gt.Prod("S0", new [] { "A", "S" } );
    gt.Prod("S0", new [] { "A", "S00" } );
    gt.Prod("S", new [] { "S", "B" } );
    gt.Prod("S", new [] { "A", "S" } );
    gt.Prod("S", new [] { "A", "S1" } );
    gt.Prod("A", new [] { "a" } );
    gt.Prod("A", new [] { "a0", "S" } );
    gt.Prod("A", new [] { "a0", "A0" } );
    gt.Prod("B", new [] { "a" } );
    gt.Prod("B", new [] { "b0", "b0" } );
    gt.Prod("B", new [] { "a0", "S" } );
    gt.Prod("B", new [] { "S", "B0" } );
    gt.Prod("B", new [] { "a0", "B1" } );
    gt.Prod("a0", new [] { "a" } );
    gt.Prod("b0", new [] { "b" } );
    gt.Prod("S00", new [] { "S", "B" } );
    gt.Prod("S1", new [] { "S", "B" } );
    gt.Prod("A0", new [] { "A", "S" } );
    gt.Prod("B0", new [] { "b0", "S" } );
    gt.Prod("B1", new [] { "A", "S" } );
    Console.WriteLine($"\n  - BNF: \n{g}");
    Console.WriteLine($"\n  - vs:{gt}");
    Console.WriteLine($"Test passed: {g.ToString() == gt.ToString()}\n");

    return true;
}


bool Test_sab2()
{
    Console.WriteLine($"\n  ---------------- SAB2:");

    var l = new RegexLexer();
    l.Lex("a", "a");
    l.Lex("b", "b");

    var g = new ContextFreeGrammar(l.Terminals, 1);
    g.Prod("S", new [] { "a" }, true );
    g.Prod("S", new [] { "a", "A" }, true );
    g.Prod("S", new [] { "B" }, true );
    g.Prod("A", new [] { "a", "B", "B" } );
    g.Prod("A", new [] { string.Empty } );
    g.Prod("B", new [] { "A", "a" } );
    g.Prod("B", new [] { "b" } );

    Console.WriteLine($"\n  - BNF: \n{g}");

    g = g.AbstractifyStartSymbol();
    Console.WriteLine($"\n  - BNF: \n{g}");

    g = g.EliminateNullProductions();
    Console.WriteLine($"\n  - BNF: \n{g}");

    g = g.EliminateUnitProductions();
    Console.WriteLine($"\n  - BNF: \n{g}");

    g = g.EliminateUselessProductions();
    Console.WriteLine($"\n  - BNF: \n{g}");

    g = g.IsolateTerminals();
    Console.WriteLine($"\n  - BNF: \n{g}");

    g = g.ReduceRulesToPairs();
    Console.WriteLine($"\n  - BNF: \n{g}");

    return true;
}


bool Test_sabbasass()
{
    Console.WriteLine($"\n  ---------------- SABBA SASS (https://courses.engr.illinois.edu/cs373/sp2009/lectures/lect_12.pdf):");

    var l = new RegexLexer();
    l.Lex("a", "a");
    l.Lex("b", "b");

    var g = new ContextFreeGrammar(l.Terminals, 1);
    g.Prod("S", new [] { "A", "S", "A" }, true );
    g.Prod("S", new [] { "a", "B" }, true );
    g.Prod("A", new [] { "B" } );
    g.Prod("A", new [] { "S" } );
    g.Prod("B", new [] { "b" } );
    g.Prod("B", new [] { string.Empty } );

    Console.WriteLine($"\n  - BNF: \n{g}");

    g = g.AbstractifyStartSymbol();
    Console.WriteLine($"\n  - BNF: \n{g}");

    g = g.EliminateNullProductions();
    Console.WriteLine($"\n  - BNF: \n{g}");

    g = g.EliminateUnitProductions();
    Console.WriteLine($"\n  - BNF: \n{g}");

    g = g.EliminateUselessProductions();
    Console.WriteLine($"\n  - BNF: \n{g}");

    g = g.IsolateTerminals();
    Console.WriteLine($"\n  - BNF: \n{g}");

    g = g.ReduceRulesToPairs();
    Console.WriteLine($"\n  - BNF: \n{g}");

    return true;
}


bool Test_ssxtricky()
{
    Console.WriteLine($"\n  ---------------- SSX tricky (https://studylib.net/doc/18262360/eliminating-left-recursion--three-steps-recall--a-cfg-is-...):");

    var l = new RegexLexer();
    l.Lex("a", "a");
    l.Lex("b", "b");

    var g = new ContextFreeGrammar(l.Terminals, 1);
    g.Prod("S", new [] { "S", "X" }, true );
    g.Prod("S", new [] { "S", "S", "b" }, true );
    g.Prod("S", new [] { "X", "S" }, true );
    g.Prod("S", new [] { "a" }, true );
    g.Prod("X", new [] { "X", "b" } );
    g.Prod("X", new [] { "S", "a" } );
    g.Prod("X", new [] { "b" } );

    Console.WriteLine($"\n  - BNF: \n{g}");

    g = g.EliminateLeftRecursion();
    Console.WriteLine($"\n  - BNF: \n{g}");

    return true;
}


bool Test_ite()
{
    Console.WriteLine($"\n  ---------------- ifthenelse (dragon book p 178):");

    var l = new RegexLexer();
    l.Lex("i", "i");
    l.Lex("t", "t");
    l.Lex("e", "e");
    l.Lex("a", "a");
    l.Lex("b", "b");

    var g = new ContextFreeGrammar(l.Terminals, 1);
    g.Prod("S", new [] { "i", "E", "t", "S" }, true );
    g.Prod("S", new [] { "i", "E", "t", "S", "e", "S" }, true );
    g.Prod("S", new [] { "a" }, true );
    g.Prod("E", new [] { "b" } );

    Console.WriteLine($"\n  - BNF: \n{g}");

    g = g.LeftFactor();
    Console.WriteLine($"\n  - BNF: \n{g}");

    return true;
}


bool Test_lf2()
{
    Console.WriteLine($"\n  ---------------- LeftFactor 02 (https://www.gatevidyalay.com/left-factoring-examples-compiler-design/):");

    var l = new RegexLexer();
    l.Lex("a", "a");
    l.Lex("b", "b");

    var g = new ContextFreeGrammar(l.Terminals, 1);
    g.Prod("A", new [] { "a", "A", "B" }, true );
    g.Prod("A", new [] { "a", "B", "c" }, true );
    g.Prod("A", new [] { "a", "A", "c" }, true );

    Console.WriteLine($"\n  - BNF: \n{g}");

    g = g.LeftFactor();
    Console.WriteLine($"\n  - BNF: \n{g}");

    return true;
}


bool Test_lf6()
{
    Console.WriteLine($"\n  ---------------- LeftFactor 06 (https://www.gatevidyalay.com/left-factoring-examples-compiler-design/):");

    var l = new RegexLexer();
    l.Lex("a", "a");
    l.Lex("b", "b");

    var g = new ContextFreeGrammar(l.Terminals, 1);
    g.Prod("S", new [] { "a", "A", "d" }, true );
    g.Prod("S", new [] { "a", "B" }, true );
    g.Prod("A", new [] { "a" } );
    g.Prod("A", new [] { "a", "b" } );
    g.Prod("B", new [] { "c", "c", "d" } );
    g.Prod("B", new [] { "d", "d", "c" } );

    Console.WriteLine($"\n  - BNF: \n{g}");

    g = g.LeftFactor();
    Console.WriteLine($"\n  - BNF: \n{g}");

    return true;
}


//Test_humon();
//Test_aces();
Test_sab();
