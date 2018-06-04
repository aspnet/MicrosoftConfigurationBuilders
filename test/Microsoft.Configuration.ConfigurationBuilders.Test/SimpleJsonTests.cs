using System;
using Microsoft.Configuration.ConfigurationBuilders;
using Xunit;


namespace Test
{
    public class SimpleJsonTests
    {
        public SimpleJsonTests()
        {
            // Populate the environment with key/value pairs that are needed for common tests
        }

        // ======================================================================
        //   SimpleJson parameters
        // ======================================================================
        //    - See Parameters section of BaseTests for examples
        // jsonFile
        // optional
        // jsonMode

        // ======================================================================
        //   CommonBuilderTests
        // ======================================================================
        [Fact]
        public void SimpleJson_GetValue()
        {
        }

        [Fact]
        public void SimpleJson_GetAllValues()
        {
        }


        // ======================================================================
        //   Errors
        // ======================================================================
        // Make sure various expected exceptions from SimpleJson contain the name of the builder
        // no file specified
        // can't find file and optional is false
    }
}
