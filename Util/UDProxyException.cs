using System;
using System.Diagnostics;

namespace SimPlaza.UDProxy
{
    class UDProxyException : Exception
    {
        /// <summary>
        /// Better exception that actually gets the source of the error
        /// </summary>
        public UDProxyException(string message) : this(message, new Exception()) { }
        public UDProxyException(string message, Exception cause) :base(message, cause)
        {
            var stackframe = new StackFrame(1);
            var method = stackframe.GetMethod();
            Source = method.DeclaringType.Name;
        }
    }
}
