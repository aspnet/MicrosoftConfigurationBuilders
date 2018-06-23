﻿using System;
using Microsoft.Configuration.ConfigurationBuilders;
using Xunit;


namespace Test
{
    public class KeyPerFileTests
    {
        public KeyPerFileTests()
        {
            // Populate the filesystem with key/value pairs that are needed for common tests
        }

        // ======================================================================
        //   KeyPerFile parameters
        // ======================================================================
        //    - See Parameters section of BaseTests for examples
        // directoryPath
        // keyDelimiter
        // ignorePrefix
        // optional


        // ======================================================================
        //   CommonBuilderTests
        // ======================================================================
        [Fact]
        public void KeyPerFile_GetValue()
        {
        }

        [Fact]
        public void KeyPerFile_GetAllValues()
        {
        }

        // ======================================================================
        //   Errors
        // ======================================================================
        // Make sure various expected exceptions from KeyPerFile contain the name of the builder
        // No file AND no ID specified
        // File and/or ID specified, but can't be found and optional = false
    }
}
