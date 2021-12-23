using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;

﻿namespace grok_lib_typeexp;

public static class EnumHelpers
{
    public static IEnumerable < (T item, int idx) > WithIdxs<T>(this IEnumerable<T> obj)
    {
        return obj.Select((obj, idx) => (obj, idx));
    }
}

public class Symbol
{
}

public class Terminal : Symbol
{
    public string Value { get; set; } = string.Empty;
}

public class Nonterminal : Symbol
{
    public Symbol [] Children { get; set; } = new Symbol[0];
}

public enum GrammarKinds
{
    LL,
    SLR,
    LR,
    LALR
}

public class LexAttribute : System.Attribute
{
    public string Pattern { get; set; } = string.Empty;
    public Regex Regex { get; private set; }

    public LexAttribute(string pattern)
    {
        Pattern = pattern;
        Regex = new Regex(pattern, RegexOptions.Compiled);
    }
}


public class Parser
{
    protected Parser(Type grammar)
    {
        var subTypes = grammar.GetNestedTypes();
        if (subTypes != null)
        {
            Terminals = (from t in subTypes
                         where t.BaseType != null && t.BaseType == typeof(Terminal)
                         select t)
                         .ToArray();
            Nonterminals = (from t in subTypes
                      where t.BaseType != null && t.BaseType == typeof(Nonterminal)
                      select t)
                      .ToArray();
        }
    }

    public Type [] Terminals { get; private set; } = new Type[0];
    public Type [] Nonterminals { get; private set; } = new Type[0];

    public string GenerateBnf()
    {
        var sb = new StringBuilder();

        int maxNonterminalNameLen = (from t in Nonterminals select t.Name.Length).Max();

        foreach (var t in Nonterminals)
        {
            sb.Append(t.Name).Append(new string(' ', maxNonterminalNameLen - t.Name.Length));
            foreach (var (ctr, idx) in t.GetConstructors().WithIdxs())
            {
                if (idx == 0)
                    { sb.Append(": "); }
                else
                    { sb.Append("| "); }
                var ps  = ctr.GetParameters();
                if (ps == null || ps.Count() == 0)
                    { sb.Append("{ e }\n"); }
                else
                    { sb.Append(string.Join(' ', from p in ctr.GetParameters() select p.ParameterType.Name)).Append('\n'); }
                sb.Append(new string(' ', maxNonterminalNameLen));
            }

            sb.Append(";\n\n");
        }

        sb.Append('\n');

        return sb.ToString();
    }

    public IEnumerable<Terminal> GenerateTokens(string src)
    {
        // Here we tokenize src into lexemes.
        // I just feel cool using the word 'lexeme.'
        int backCur = 0;
        while(backCur < src.Length)
        {
            int bestMatch = -1;
            int bestMatchLen = 0;
            string bestMatchStr = string.Empty;
            foreach (var (nont, idx) in Terminals.WithIdxs())
            {
                var atts = from att in System.Attribute.GetCustomAttributes(nont)
                           where att.GetType() == typeof(LexAttribute)
                           select att;
                if (atts == null)
                    { continue; }
                foreach (var att in atts)
                {
                    var re = (att as LexAttribute)?.Regex;
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
                            bestMatch = idx;
                            bestMatchLen = len;
                            bestMatchStr = match.Groups[0].Value;
                        }
                    }
                }
            }

            if (bestMatch == -1)
                { throw new Exception($"({backCur}): syntax error: No matching lexemes"); }

            backCur += bestMatchLen;

            //Console.WriteLine($"{Terminals[bestMatch].Name} - value = \"{bestMatchStr}\" - backCur = {backCur}");
            var t = Activator.CreateInstance(Terminals[bestMatch]) as Terminal;
            if (t != null)
            {
                t.Value = bestMatchStr;
                yield return t;
            }
            else
                { throw new Exception($"({backCur}): NO LOVE"); }

            if (backCur > src.Length)
                { throw new Exception("Fatality!"); }
        }
    }
}

public class LalrParser : Parser
{
    public LalrParser(Type grammar) : base(grammar)
    {
    }
}

public class Grokker
{
    public Grokker()
    {
    }

    public Parser MakeLalrParser(Type grammar)
    {
        return new LalrParser(grammar);
    }

    void Test(HumonGrammar g, string src)
    {
        var parser = MakeLalrParser(typeof(HumonGrammar));

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
    }
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
        public KeyNode(Value value, Annotation annotation0, KeyValueSeparator keyValueSeparator, Annotation annotation1, Node node) { }
    }

    public class Annotation : Nonterminal
    {
        public Annotation(AnnotationMark annotationMark, DictBegin dictBegin, KeyValueSequence keyValueSequence, DictEnd dictEnd) { }
        public Annotation(AnnotationMark annotationMark, KeyValue keyValue) { }
    }

    public class KeyValueSequence : Nonterminal
    {
        public KeyValueSequence(KeyValueSequence keyValueSequence, KeyValue keyValue) { }
        public KeyValueSequence() { }
    }

    public class KeyValue : Nonterminal
    {
        public KeyValue(Value value0, KeyValueSeparator keyValueSeparator, Value value1) { }
    }
}
