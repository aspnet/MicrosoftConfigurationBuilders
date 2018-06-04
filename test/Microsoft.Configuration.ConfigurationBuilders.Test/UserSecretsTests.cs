using System;
using Microsoft.Configuration.ConfigurationBuilders;
using Xunit;


namespace Test
{
    public class UserSecretsTests
    {
        public UserSecretsTests()
        {
            // Populate the environment with key/value pairs that are needed for common tests
        }

        // ======================================================================
        //   SimpleJson parameters
        // ======================================================================
        //    - See Parameters section of BaseTests for examples
        // userSecretsFile
        // userSecretsId
        // optional
        // Verify that userSecretsFile takes priority over userSecretsId when both are given

 
        // ======================================================================
        //   CommonBuilderTests
        // ======================================================================
        [Fact]
        public void UserSecrets_GetValue()
        {
        }

        [Fact]
        public void UserSecrets_GetAllValues()
        {
        }

        // ======================================================================
        //   Errors
        // ======================================================================
        // Make sure various expected exceptions from UserSecrets contain the name of the builder
        // No file AND no ID specified
        // File and/or ID specified, but can't be found and optional = false
    }
}
