using System.Globalization;

namespace Mercantec.Tech.Contracts;

/// <summary>Evaluates Taljagt's tile tokens. A number token refers to a tile by its index.</summary>
public static class DailyNumberExpression
{
    public const int MaxTokens = 31;
    private const int MaxValue = 1_000_000;

    public static bool TryEvaluate(IReadOnlyList<int> tiles, IReadOnlyList<string>? tokens, out int value, out string expression)
    {
        value = 0;
        expression = "";
        if (tiles.Count != 6 || tokens is null || tokens.Count is < 3 or > MaxTokens)
            return false;

        var parser = new Parser(tiles, tokens);
        if (!parser.TryExpression(out var result) || !parser.AtEnd || parser.UsedCount < 2)
            return false;

        value = result;
        expression = string.Join(" ", tokens.Select(token => IsTileToken(token)
            ? tiles[token[0] - '0'].ToString(CultureInfo.InvariantCulture)
            : token));
        return true;
    }

    public static bool IsTileToken(string? token) => token is [>= '0' and <= '5'];

    private sealed class Parser(IReadOnlyList<int> tiles, IReadOnlyList<string> tokens)
    {
        private int _position;
        private int _used;

        public int UsedCount { get; private set; }
        public bool AtEnd => _position == tokens.Count;
        private string? Current => AtEnd ? null : tokens[_position];

        public bool TryExpression(out int value)
        {
            if (!TryTerm(out value)) return false;
            while (Current is "+" or "-")
            {
                var operation = tokens[_position++];
                if (!TryTerm(out var right)) return false;
                value = operation == "+" ? value + right : value - right;
                if (value is < 0 or > MaxValue) return false;
            }
            return true;
        }

        private bool TryTerm(out int value)
        {
            if (!TryFactor(out value)) return false;
            while (Current is "*" or "/")
            {
                var operation = tokens[_position++];
                if (!TryFactor(out var right)) return false;
                if (operation == "*")
                {
                    var product = (long)value * right;
                    if (product > MaxValue) return false;
                    value = (int)product;
                }
                else
                {
                    if (right == 0 || value % right != 0) return false;
                    value /= right;
                }
            }
            return true;
        }

        private bool TryFactor(out int value)
        {
            value = 0;
            var token = Current;
            if (token == "(")
            {
                _position++;
                if (!TryExpression(out value) || Current != ")") return false;
                _position++;
                return true;
            }

            if (!IsTileToken(token)) return false;
            var index = token![0] - '0';
            var bit = 1 << index;
            if ((_used & bit) != 0 || tiles[index] is < 0 or > MaxValue)
                return false;

            _position++;
            _used |= bit;
            UsedCount++;
            value = tiles[index];
            return true;
        }
    }
}
