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
g.Prod("trove",             new [] { "node" }, true );
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

Console.WriteLine($"\n - Bottom-up reduction");
g = g.ReduceBottomUp();
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");

Console.WriteLine($"\n - Top-down reduction");
g = g.ReduceTopDown();
Console.WriteLine($"\n  - BNF: \n{g.GenerateBnf()}");




/*
var parser = g.MakeLalrParser(typeof(HumonGrammar));

Console.WriteLine("  - Terminal defs:");
foreach (var c in parser.Terminals)
{
    Console.WriteLine(c.Name);
}

Console.WriteLine("\n  - Nonterminal defs:");
foreach (var c in parser.Nonterminals)
{
    Console.WriteLine(c.Name);
}

Console.WriteLine($"\n  - BNF: \n{parser.GenerateBnf()}");

Console.WriteLine($"\n  - Src:\n{src}");

Console.WriteLine($"\n  - Tokens:");
foreach (var token in parser.GenerateTokens(src))
{
    Console.WriteLine($"{token.GetType().Name}: {token.Value}");
}

public class HumonGrammar
{
    [Lex(@"\s|,")]                      public class Whitespace : Terminal { }
    [Lex(@"\/\*(.|\n)*?\*\/")]          public class CStyleComment : Terminal { }
    [Lex(@"\/\/.*?$")]                  public class CppStyleComment : Terminal { }
    [Lex(@":")]                         public class KeyValueSeparator : Terminal { }
    [Lex(@"\[")]                        public class ListBegin : Terminal { }
    [Lex(@"]")]                         public class ListEnd : Terminal { }
    [Lex(@"\{")]                        public class DictBegin : Terminal { }
    [Lex(@"}")]                         public class DictEnd : Terminal { }
    [Lex(@"@")]                         public class AnnotationMark : Terminal { }
    [Lex(@"'(.|\n)*?'")]                public class Word_squote : Terminal { }
    [Lex(@"""(.|\n)*?""")]              public class Word_dquote : Terminal { }
    [Lex(@"`(.|\n)*?`")]                public class Word_backquote : Terminal { }
    [Lex(@"(\^(.|\n)*?\^)(.|\n)*\1")]   public class Word_heredoc : Terminal { }
    [Lex(@"[^\s\{\}\[\]\:,@]+?(?=(\/\*)|(\/\/)|[\s\{\}\[\]\:,@]|$)")]
                                        public class Word : Terminal { }

    public class Trove : Nonterminal
    {
        public Trove(Node node) { }
    }

    public class Node : Nonterminal
    {
        public Node(List list) { }
        public Node(List list, Annotation annotation) { }
        public Node(Dict dict) { }
        public Node(Dict dict, Annotation annotation) { }
        public Node(Value value) { }
        public Node(Value value, Annotation annotation) { }
    }

    public class List : Nonterminal
    {
        public List(ListBegin listBegin, Sequence sequence, ListEnd listEnd) { }
        public List(ListBegin listBegin, Annotation annotation, Sequence sequence, ListEnd listEnd) { }
    }

    public class Dict : Nonterminal
    {
        public Dict(DictBegin dictBegin, KeyNodeSequence keyNodeSequence, DictEnd dictEnd) { }
        public Dict(DictBegin dictBegin, Annotation annotation, KeyNodeSequence keyNodeSequence, DictEnd dictEnd) { }
    }

    public class Value : Nonterminal
    {
        public Value(Word_squote word_sqote) { }
        public Value(Word_dquote word_dquote) { }
        public Value(Word_backquote word_backquote) { }
        public Value(Word_heredoc word_heredoc) { }
        public Value(Word word) { }
    }

    public class Sequence : Nonterminal
    {
        public Sequence(Sequence sequence, Node node) { }
        public Sequence() { }
    }

    public class KeyNodeSequence : Nonterminal
    {
        public KeyNodeSequence(KeyNodeSequence keyNodeSequence, Node node) { }
        public KeyNodeSequence() { }
    }

    public class KeyNode : Nonterminal
    {
        public KeyNode(Value value, KeyValueSeparator keyValueSeparator, Node node) { }
        public KeyNode(Value value, Annotation annotation, KeyValueSeparator keyValueSeparator, Node node) { }
        public KeyNode(Value value, KeyValueSeparator keyValueSeparator, Annotation annotation, Node node) { }
        public KeyNode(Value value, Annotation annotation, KeyValueSeparator keyValueSeparator, Annotation annotation, Node node) { }
    }

    public class Annotation : Nonterminal
    {
        public Annotation(AnnotationMark annotationMark, DictBegin dictBegin, KeyValueSequence keyValueSequence, DictEnd dictEnd) { }
        public Annotation(AnnotationMark annotationMark, KeyValue keyValue) { }
    }

    public class KeyValueSequence : Nonterminal
    {
        public KeyValueSequence(KeyValueSequence keyValueSequence, keyValue) { }
        public KeyValueSequence() { }
    }

    public class KeyValue : Nonterminal
    {
        public KeyValue(Value value0, KeyValueSeparator keyValueSeparator, Value value1) { }
    }
}
*/
