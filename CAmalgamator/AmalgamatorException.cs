namespace CAmalgamator
{
    using System;

    public sealed class AmalgamatorException : Exception
    {
        public AmalgamatorException(string message) : base(message) { }
    }
}