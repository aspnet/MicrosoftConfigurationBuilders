using System;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.Configuration.ConfigurationBuilders;
using Xunit;


namespace Test
{
    public class UserSecretsTests : IDisposable
    {
        private readonly string secretsFileName;
        private readonly string secretsId;
        private readonly string secretsIdFileName;
        private bool disposedValue;

        public UserSecretsTests()
        {
            // Get clean secrets file locations
            secretsId = Guid.NewGuid().ToString();
            secretsIdFileName = GetSecretsFileFromID(secretsId);
            if (File.Exists(secretsIdFileName))
                File.Delete(secretsIdFileName);
            string idDirectory = Path.GetDirectoryName(secretsIdFileName);
            if (!Directory.Exists(idDirectory))
                Directory.CreateDirectory(idDirectory);
            secretsFileName = Path.Combine(Environment.CurrentDirectory, "UserSecretsTest_" + Path.GetRandomFileName() + ".xml");
            if (File.Exists(secretsFileName))
                File.Delete(secretsFileName);


            // Populate the secrets file with key/value pairs that are needed for common tests
            XDocument xdocFile = XDocument.Parse("<root><secrets ver=\"1.0\"><secret name=\"secretSource\" value=\"file\" /></secrets></root>");
            XDocument xdocId = XDocument.Parse("<root><secrets ver=\"1.0\"><secret name=\"secretSource\" value=\"id\" /></secrets></root>");
            foreach (string key in CommonBuilderTests.CommonKeyValuePairs)
            {
                XElement e = new XElement("secret");
                e.SetAttributeValue("name", key);
                e.SetAttributeValue("value", CommonBuilderTests.CommonKeyValuePairs[key]);
                xdocId.Root.Element("secrets").Add(new XElement(e));
                xdocFile.Root.Element("secrets").Add(e);
            }
            xdocId.Save(secretsIdFileName);
            xdocFile.Save(secretsFileName);
        }


        // ======================================================================
        //   CommonBuilderTests
        // ======================================================================
        [Fact]
        public void UserSecrets_GetValue()
        {
            CommonBuilderTests.GetValue(() => new UserSecretsConfigBuilder(), "SecretsGetValue",
                new NameValueCollection() { { "userSecretsFile", secretsFileName } });
        }

        [Fact]
        public void UserSecrets_GetAllValues()
        {
            CommonBuilderTests.GetAllValues(() => new UserSecretsConfigBuilder(), "SecretsGetAll",
                new NameValueCollection() { { "userSecretsFile", secretsFileName } });
        }

        // ======================================================================
        //   UserSecrets Parameters
        // ======================================================================
        [Fact]
        public void UserSecrets_DefaultSettings()
        {
            var secrets = TestHelper.CreateBuilder<UserSecretsConfigBuilder>(() => new UserSecretsConfigBuilder(), "SecretsDefault",
                new NameValueCollection() { { "userSecretsFile", secretsFileName } });
            var mappedFile = Utils.MapPath(secretsFileName);

            // UserSecretsFile
            Assert.Equal(mappedFile, secrets.UserSecretsFile);
            Assert.Equal(secretsFileName, mappedFile);  // Doesn't really matter. But this should be the case in this test.

            // UserSecretsId
            Assert.Null(secrets.UserSecretsId);

            // Enabled
            Assert.Equal(KeyValueEnabled.Optional, secrets.Enabled);

            // CharacterMap
            Assert.Empty(secrets.CharacterMap);
        }

        [Fact]
        public void UserSecrets_Settings()
        {
            // SecretsFile
            var secrets = TestHelper.CreateBuilder<UserSecretsConfigBuilder>(() => new UserSecretsConfigBuilder(), "SecretsSettings1",
                new NameValueCollection() { { "userSecretsFile", secretsFileName } });
            var mappedFile = Utils.MapPath(secretsFileName);
            Assert.Equal(mappedFile, secrets.UserSecretsFile);
            Assert.Equal(secretsFileName, mappedFile);  // Doesn't really matter. But this should be the case in this test.
            Assert.Null(secrets.UserSecretsId);
            Assert.Equal("file", secrets.GetValue("secretSource"));

            // UserSecretsID
            secrets = TestHelper.CreateBuilder<UserSecretsConfigBuilder>(() => new UserSecretsConfigBuilder(), "SecretsSettings2",
                new NameValueCollection() { { "userSECRETSid", secretsId } });
            mappedFile = Utils.MapPath(secretsIdFileName);
            Assert.Equal(mappedFile, secrets.UserSecretsFile);
            Assert.Equal(secretsIdFileName, mappedFile);  // Doesn't really matter. But this should be the case in this test.
            Assert.Equal(secretsId, secrets.UserSecretsId);
            Assert.Equal("id", secrets.GetValue("secretSource"));


            // Both UserSecretsID and UserSecretsFile - Builder only looks at secretsFile
            secrets = TestHelper.CreateBuilder<UserSecretsConfigBuilder>(() => new UserSecretsConfigBuilder(), "SecretsSettings3",
                new NameValueCollection() { { "userSecretsId", secretsId }, { "usERSecRETsFile", secretsFileName } });
            mappedFile = Utils.MapPath(secretsFileName);
            Assert.Equal(mappedFile, secrets.UserSecretsFile);
            Assert.Equal(secretsFileName, mappedFile);  // Doesn't really matter. But this should be the case in this test.
            Assert.Null(secrets.UserSecretsId);
            Assert.Equal("file", secrets.GetValue("secretSource"));
        }

        // ======================================================================
        //   Errors
        // ======================================================================
        [Theory]
        [InlineData(KeyValueEnabled.Optional)]
        [InlineData(KeyValueEnabled.Enabled)]
        [InlineData(KeyValueEnabled.Disabled)]
        public void UserSecrets_Errors(KeyValueEnabled enabled)
        {
            // NoSecretsFile or ID
            var exception = Record.Exception(() => {
                TestHelper.CreateBuilder<UserSecretsConfigBuilder>(() => new UserSecretsConfigBuilder(), "SecretsErrors1",
                    new NameValueCollection() { { "enabled", enabled.ToString() } });
            });
            if (enabled == KeyValueEnabled.Enabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "SecretsErrors1");
            else
                Assert.Null(exception);

            // File does not exist
            exception = Record.Exception(() => {
                TestHelper.CreateBuilder<UserSecretsConfigBuilder>(() => new UserSecretsConfigBuilder(), "SecretsErrors2",
                    new NameValueCollection() { { "enabled", enabled.ToString() }, { "userSecretsFile", "invalidFileName" } });
            });
            if (enabled == KeyValueEnabled.Enabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "SecretsErrors2");
            else
                Assert.Null(exception);


            // ID does not exist
            //  ... when Optional (default)
            exception = Record.Exception(() => {
                TestHelper.CreateBuilder<UserSecretsConfigBuilder>(() => new UserSecretsConfigBuilder(), "SecretsErrors3",
                    new NameValueCollection() { { "enabled", enabled.ToString() }, { "userSecretsId", "invalidId" } });
            });
            if (enabled == KeyValueEnabled.Enabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "SecretsErrors3");
            else
                Assert.Null(exception);
        }


        // ======================================================================
        //   Helpers
        // ======================================================================
        private string GetSecretsFileFromID(string id)
        {
            MethodInfo getFileName = typeof(UserSecretsConfigBuilder).GetMethod("GetSecretsFileFromId", BindingFlags.NonPublic | BindingFlags.Instance);
            var tempBuilder = new UserSecretsConfigBuilder();
            string filename = getFileName.Invoke(tempBuilder, new object[] { id }) as string;
            return filename;
        }

        // ======================================================================
        //   IDisposable Pattern
        // ======================================================================
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                File.Delete(secretsFileName);
                File.Delete(secretsIdFileName);
                disposedValue = true;
            }
        }

        // override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        ~UserSecretsTests()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
