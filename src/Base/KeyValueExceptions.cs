// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

using System;
using System.Configuration;

namespace Microsoft.Configuration.ConfigurationBuilders
{
    internal class KeyValueExceptionHelper
    {
        public static Exception CreateKVCException(string msg, Exception ex, ConfigurationBuilder cb)
        {

            // If it's a ConfigurationErrorsException though, that means its coming from a re-entry to the
            // config system. That's where the root issue is, and that's the "Error Message" we want on
            // the top of the exception chain. So wrap it in another ConfigurationErrorsException of
            // sorts so the config system will use it instead of rolling it's own at this secondary
            // level.
            if (ex is ConfigurationErrorsException ceex)
            {
                var inner = new KeyValueConfigException($"'{cb.Name}' {msg} ==> {ceex.InnerException?.Message ?? ceex.Message}", ex.InnerException);
                return new KeyValueConfigWrappedException(ceex.Message, inner);
            }

            return new KeyValueConfigException($"'{cb.Name}' {msg}: {ex.Message}", ex);
        }

        public static bool IsKeyValueConfigException(Exception ex) => (ex is KeyValueConfigException) || (ex is KeyValueConfigWrappedException);
    }

    // There are two different exception types here because the .Net config system treats
    // ConfigurationErrorsExceptions differently. It considers it to be a pre-wrapped and ready for
    // presentation exception. Other exceptions get wrapped by the config system.

    internal class KeyValueConfigException : Exception
    {
        public KeyValueConfigException(string msg, Exception inner) : base(msg, inner) { }
    }

    internal class KeyValueConfigWrappedException : ConfigurationErrorsException
    {
        public KeyValueConfigWrappedException(string msg, Exception inner) : base(msg, inner) { }
    }
}
