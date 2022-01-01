using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Specialized;
using System.Diagnostics;

namespace grok;

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
    public int Line;
    public int Column;
    public string Value;
}

public abstract class Lexer
{
    public virtual string [] Terminals { get; } = new string [] {};

    public IEnumerable<Token> GenerateTokens(string src)
    {
        return GenerateTokens_imp(src);
    }

    protected abstract IEnumerable<Token> GenerateTokens_imp(string src);
}

public class RegexLexer : Lexer
{
    OrderedStringDictionary<(string pattern, Regex regex, bool producesTokens)> lexRules
        = new OrderedStringDictionary<(string pattern, Regex regex, bool producesTokens)>();

    public void Lex(string name, string pattern, bool producesTokens = true)
    {
        lexRules.Add(name, (pattern, new Regex(pattern, RegexOptions.Compiled), producesTokens));
    }

    // rules should have order preserved
    public override string [] Terminals => lexRules.Keys.ToArray();

    protected override IEnumerable<Token> GenerateTokens_imp(string src)
    {
        // Here we tokenize src into lexemes.
        // I just feel cool using the word 'lexeme.'
        int backCur = 0;
        int line = 1;
        int column = 1;

        while(backCur < src.Length)
        {
            string bestMatchTerminal = string.Empty;
            int bestMatchTerminalRuleIdx = -1;
            int bestMatchLen = 0;
            string bestMatchStr = string.Empty;
            bool bestMatchProducesToken = true;

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
                        bestMatchProducesToken = rule.value.producesTokens;
                    }
                }
            }

            if (bestMatchTerminal == string.Empty)
                { throw new Exception($"({line}:{column}): syntax error: No matching lexemes"); }

            backCur += bestMatchLen;

            if (bestMatchProducesToken)
            {
                yield return new Token {
                    Terminal = bestMatchTerminal,
                    TerminalRuleIdx = bestMatchTerminalRuleIdx,
                    Line = line,
                    Column = column,
                    Value = bestMatchStr };
            }
            for (int i = 0; i < bestMatchStr.Length; ++i)
            {
                // TODO: \r, CRLF, al that thankless bs
                if (bestMatchStr[i] == '\n')
                {
                    line += 1;
                    column = 0;
                }
                column += 1;
            }

            if (backCur > src.Length)
                { throw new Exception("Fatality!"); }
        }
    }
}
