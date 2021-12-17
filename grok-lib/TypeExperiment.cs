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
}
