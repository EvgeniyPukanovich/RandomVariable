using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RandomVariable
{
    public class Parser
    {
        Tokenizer _tokenizer;
        //can be change to easily distinguish unary minus from minus between terms
        public static string unaryMinus = "-";

        public Parser(Tokenizer tokenizer)
        {
            _tokenizer = tokenizer;
        }

        // Parse an entire expression and check EOF was reached
        public Node ParseExpression()
        {
            // For the moment, all we understand is add and subtract
            var expr = ParseAddSubtract();

            // Check everything was consumed
            if (_tokenizer.Token != Token.EOF)
                throw new SyntaxException("Unexpected characters at end of expression");

            return expr;
        }

        // Parse an sequence of add/subtract operators
        Node ParseAddSubtract()
        {
            // Parse the left hand side
            var lhs = ParseMultiplyDivide();

            while (true)
            {
                // Work out the operator
                Func<string, string, string> op = null;
                if (_tokenizer.Token == Token.Add)
                {
                    op = (a, b) => a + "+" + b;
                }
                else if (_tokenizer.Token == Token.Subtract)
                {
                    op = (a, b) => a + "-" + b;
                }

                // Binary operator found?
                if (op == null)
                    return lhs;             // no

                // Skip the operator
                _tokenizer.NextToken();

                // Parse the right hand side of the expression
                var rhs = ParseMultiplyDivide();

                // Create a binary node and use it as the left-hand side from now on
                lhs = new NodeBinary(lhs, rhs, op);
            }
        }

        // Parse an sequence of add/subtract operators
        Node ParseMultiplyDivide()
        {
            // Parse the left hand side
            var lhs = ParseUnary();

            while (true)
            {
                // Work out the operator
                Func<string, string, string> op = null;
                if (_tokenizer.Token == Token.Multiply)
                {
                    op = (a, b) =>
                    {
                        return MultiplyDivide("*", a, b);
                    };
                }
                else if (_tokenizer.Token == Token.Divide)
                {
                    op = (a, b) =>
                    {
                        return MultiplyDivide("/", a, b);
                    };
                }

                // Binary operator found?
                if (op == null)
                    return lhs;             // no

                // Skip the operator
                _tokenizer.NextToken();

                // Parse the right hand side of the expression
                var rhs = ParseUnary();

                // Create a binary node and use it as the left-hand side from now on
                lhs = new NodeBinary(lhs, rhs, op);
            }
        }

        private string MultiplyDivide(string sign, string a, string b)
        {
            string[] membersLeft = a.Split(new char[] { '+', '-' });
            char[] signsLeft = a.ToCharArray().Where(ch => ch == '+' || ch == '-').ToArray();
            string[] membersRight = b.Split(new char[] { '+', '-' });
            char[] signsRight = b.ToCharArray().Where(ch => ch == '+' || ch == '-').ToArray();

            string res = "";
            for (int i = 0; i < membersLeft.Length; i++)
            {
                string leftItem = membersLeft[i];
                for (int j = 0; j < membersRight.Length; j++)
                {
                    string rightItem = membersRight[j];

                    string termSign = "";
                    if (i != 0)
                    {
                        if (j != 0)
                        {
                            if ((signsLeft[i - 1] == '-' && signsRight[j - 1] == '-') ||
                            ((signsLeft[i - 1] == '+' && signsRight[j - 1] == '+')))
                                termSign = "+";
                            else
                                termSign = "-";
                        }
                        termSign = signsLeft[i - 1].ToString();
                    }
                    else
                    {
                        if (j != 0)
                            termSign = signsRight[j - 1].ToString();
                    }

                    res += termSign + leftItem + sign + rightItem;
                }
            }
            return res;
        }

        // Parse a unary operator (eg: negative/positive)
        Node ParseUnary()
        {
            // Positive operator is a no-op so just skip it
            if (_tokenizer.Token == Token.Add)
            {
                // Skip
                _tokenizer.NextToken();
                return ParseUnary();
            }

            // Negative operator
            if (_tokenizer.Token == Token.Subtract)
            {
                // Skip
                _tokenizer.NextToken();

                // Parse RHS 
                // Note this recurses to self to support negative of a negative
                var rhs = ParseUnary();

                // Create unary node
                return new NodeUnary(rhs, a =>
                {
                    string[] members = a.Split(new char[] { '+', '-' });
                    char[] signs = a.ToCharArray().Where(ch => ch == '+' || ch == '-').ToArray();
                    string res = "";

                    for (int i = 0; i < members.Length; i++)
                    {
                        if (i == 0)
                            res += "$" + members[i];
                        else
                            res += signs[i - 1] + unaryMinus + members[i];
                    }
                    return res;
                });
            }

            // No positive/negative operator so parse a leaf node
            return ParseLeaf();
        }

        // Parse a leaf node
        // (For the moment this is just a number)
        Node ParseLeaf()
        {
            // Is it a number?
            if (_tokenizer.Token == Token.Number)
            {
                var node = new NodeNumber(_tokenizer.Number.ToString());
                _tokenizer.NextToken();
                return node;
            }

            // Parenthesis?
            if (_tokenizer.Token == Token.OpenParens)
            {
                // Skip '('
                _tokenizer.NextToken();

                // Parse a top-level expression
                var node = ParseAddSubtract();

                // Check and skip ')'
                if (_tokenizer.Token != Token.CloseParens)
                    throw new SyntaxException("Missing close parenthesis");
                _tokenizer.NextToken();

                // Return
                return node;
            }

            // Don't Understand
            throw new SyntaxException($"Unexpect token: {_tokenizer.Token}");
        }


        #region Convenience Helpers

        // Static helper to parse a string
        public static Node Parse(string str)
        {
            return Parse(new Tokenizer(new StringReader(str)));
        }

        // Static helper to parse from a tokenizer
        public static Node Parse(Tokenizer tokenizer)
        {
            var parser = new Parser(tokenizer);
            return parser.ParseExpression();
        }

        #endregion
    }
}
