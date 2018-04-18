using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace OctoPlusCore.Logging
{
    public static class LoggingProvider 
    {
        internal static ILoggerFactory loggerFactory;

        static LoggingProvider() 
        {
            loggerFactory = new LoggerFactory();
        }

        public static Interfaces.ILogger<T> GetLogger<T>() where T : class 
        {
            return new OctoLogger<T>();
        }
    }
}
