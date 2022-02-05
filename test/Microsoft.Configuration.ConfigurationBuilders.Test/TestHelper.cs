using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.Configuration.ConfigurationBuilders;
using Xunit;

namespace Test
{
    public class TestHelper
    {
        static Type kvWrapperType;
        static Type kvExceptionType;

        static TestHelper()
        {
            kvWrapperType = typeof(KeyValueConfigBuilder).Assembly.GetType("Microsoft.Configuration.ConfigurationBuilders.KeyValueConfigWrappedException");
            kvExceptionType = typeof(KeyValueConfigBuilder).Assembly.GetType("Microsoft.Configuration.ConfigurationBuilders.KeyValueConfigException");
        }

        public static Exception AssertExceptionIsWrapped(Exception e, string msg = null)
        {
            Assert.NotNull(e);
            if (msg != null)
                Assert.Contains(msg, e.Message);
            Assert.IsType(kvWrapperType, e);
            Assert.NotNull(e.InnerException);
            Assert.IsType(kvExceptionType, e.InnerException);
            Assert.NotNull(e.InnerException.InnerException);

            return e.InnerException.InnerException;
        }
    }
}
