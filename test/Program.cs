using grok_lib;

var src = @"{foo:bar}";

var l = new RegexLexer();

l.Lex("whitespace",         @"\s|,"                     );
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

var g = new Grammar(l);
/**/

g.Prod("trove",             new [] { "node" }, true );
g.Prod("trove",             new [] { "annotation", "node" }, true );
g.Prod("trove",             new [] { string.Empty }, true );
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

Console.WriteLine($"\n  ---------------- HUMON: start = {g.StartSymbol}\n");
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");

Console.WriteLine($"\n  - Src:\n{src}");

Console.WriteLine($"\n  - Tokens:");
foreach (var token in g.GenerateTokens(src))
{
    Console.WriteLine($"{token.GetType().Name} ({token.Address}): {token.Value}");
}

Console.WriteLine($"\n - Eliminating null productions");
g = g.EliminateNullProductions();
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");

Console.WriteLine($"\n - Eliminating unit productions");
g = g.EliminateUnitProductions();
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");

Console.WriteLine($"\n - Isolating mixed terminal productions");
g = g.IsolateTerminals();
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");

Console.WriteLine($"\n - Reducing rules to pairs");
g = g.IsolateTerminals();
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");

l = new RegexLexer();
l.Lex("a", "a");
l.Lex("c", "c");
l.Lex("e", "e");

g = new Grammar(l);
g.Prod("S", new [] { "A", "C" }, true );
g.Prod("S", new [] { "B" }, true );
g.Prod("A", new [] { "a" } );
g.Prod("C", new [] { "c" } );
g.Prod("C", new [] { "B", "C" } );
g.Prod("E", new [] { "a", "A" } );
g.Prod("E", new [] { "e" } );

Console.WriteLine($"\n  ---------------- ACES: start = {g.StartSymbol}\n");
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");

Console.WriteLine($"\n - Bottom-up reduction");
g = g.ReduceBottomUp();
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");
// should be: S -> AC; A -> a; C -> c; E -> aA | E;

Console.WriteLine($"\n - Top-down reduction");
g = g.ReduceTopDown();
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");
// should be: S -> AC; A -> a; C -> c;

l = new RegexLexer();
l.Lex("a", "a");
l.Lex("b", "b");

g = new Grammar(l);
g.Prod("S", new [] { "A", "S", "B" }, true );
g.Prod("A", new [] { "a", "A", "S" } );
g.Prod("A", new [] { "a" } );
g.Prod("A", new [] { string.Empty } );
g.Prod("B", new [] { "S", "b", "S" } );
g.Prod("B", new [] { "A" } );
g.Prod("B", new [] { "b", "b" } );

Console.WriteLine($"\n  ---------------- SAB (https://www.geeksforgeeks.org/converting-context-free-grammar-chomsky-normal-form/): start = {g.StartSymbol}\n");
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");

Console.WriteLine($"\n - AbstractifyStartSymbol");
g = g.AbstractifyStartSymbol();
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");

Console.WriteLine($"\n - Eliminating null productions");
g = g.EliminateNullProductions();
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");

Console.WriteLine($"\n - Eliminating unit productions");
g = g.EliminateUnitProductions();
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");

Console.WriteLine($"\n - Isolating mixed terminal productions");
g = g.IsolateTerminals();
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");

Console.WriteLine($"\n - Reducing rules to pairs (broken)");
g = g.ReduceRulesToPairs();
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");


l = new RegexLexer();
l.Lex("a", "a");
l.Lex("b", "b");

g = new Grammar(l);
g.Prod("S", new [] { "a" }, true );
g.Prod("S", new [] { "a", "A" }, true );
g.Prod("S", new [] { "B" }, true );
g.Prod("A", new [] { "a", "B", "B" } );
g.Prod("A", new [] { string.Empty } );
g.Prod("B", new [] { "A", "a" } );
g.Prod("B", new [] { "b" } );

Console.WriteLine($"\n  ---------------- SAB2: start = {g.StartSymbol}\n");
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");

Console.WriteLine($"\n - AbstractifyStartSymbol");
g = g.AbstractifyStartSymbol();
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");

Console.WriteLine($"\n - Eliminating null productions");
g = g.EliminateNullProductions();
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");

Console.WriteLine($"\n - Eliminating unit productions");
g = g.EliminateUnitProductions();
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");

Console.WriteLine($"\n - Reducing");
g = g.EliminateUselessProductions();
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");

Console.WriteLine($"\n - Isolating mixed terminal productions");
g = g.IsolateTerminals();
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");

Console.WriteLine($"\n - Reducing rules to pairs");
g = g.ReduceRulesToPairs();
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");


l = new RegexLexer();
l.Lex("a", "a");
l.Lex("b", "b");

g = new Grammar(l);
g.Prod("S", new [] { "A", "S", "A" }, true );
g.Prod("S", new [] { "a", "B" }, true );
g.Prod("A", new [] { "B" } );
g.Prod("A", new [] { "S" } );
g.Prod("B", new [] { "b" } );
g.Prod("B", new [] { string.Empty } );

Console.WriteLine($"\n  ---------------- SABBA SASS (https://courses.engr.illinois.edu/cs373/sp2009/lectures/lect_12.pdf): start = {g.StartSymbol}\n");
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");

Console.WriteLine($"\n - AbstractifyStartSymbol");
g = g.AbstractifyStartSymbol();
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");

Console.WriteLine($"\n - Eliminating null productions");
g = g.EliminateNullProductions();
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");

Console.WriteLine($"\n - Eliminating unit productions");
g = g.EliminateUnitProductions();
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");

Console.WriteLine($"\n - Reducing");
g = g.EliminateUselessProductions();
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");

Console.WriteLine($"\n - Isolating mixed terminal productions");
g = g.IsolateTerminals();
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");

Console.WriteLine($"\n - Reducing rules to pairs");
g = g.ReduceRulesToPairs();
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");


l = new RegexLexer();
l.Lex("a", "a");
l.Lex("b", "b");

g = new Grammar(l);
g.Prod("S", new [] { "S", "X" }, true );
g.Prod("S", new [] { "S", "S", "b" }, true );
g.Prod("S", new [] { "X", "S" }, true );
g.Prod("S", new [] { "a" }, true );
g.Prod("X", new [] { "X", "b" } );
g.Prod("X", new [] { "S", "a" } );
g.Prod("X", new [] { "b" } );

Console.WriteLine($"\n  ---------------- SSX tricky (https://studylib.net/doc/18262360/eliminating-left-recursion--three-steps-recall--a-cfg-is-...): start = {g.StartSymbol}\n");
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");

Console.WriteLine($"\n - Eliminating left recursion");
g = g.EliminateLeftRecursion();
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");
