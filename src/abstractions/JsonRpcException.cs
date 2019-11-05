using System;

namespace NeoExpress.Abstractions
{
    public class JsonRpcException : Exception
    {
        public int Code => HResult;

        public JsonRpcException(string message, int code) : base(message)
        {
            HResult = code;
        }

        public JsonRpcException()
        {
        }

        public JsonRpcException(string message) : base(message)
        {
        }

        public JsonRpcException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
