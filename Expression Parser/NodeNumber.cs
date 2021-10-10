namespace RandomVariable
{
    // NodeNumber represents a literal number in the expression
    class NodeNumber : Node
    {
        public NodeNumber(string number)
        {
            _number = number;
        }

        string _number;             // The number

        public override string Eval()
        {
            // Just return it.  Too easy.
            return _number;
        }
    }
}
