using System;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.Configuration.ConfigurationBuilders;
using Xunit;


namespace Test
{
    public class UserSecretsFixture : IDisposable
    {
        public string SecretsFileName { get; private set; }
        public string SecretsId { get; private set; }
        public string SecretsIdFileName { get; private set; }
        public string CommonSecretsFileName { get; private set; }

        public UserSecretsFixture()
        {
            // Get clean secrets file locations
            SecretsId = Guid.NewGuid().ToString();
            SecretsIdFileName = GetSecretsFileFromID(SecretsId);
            if (File.Exists(SecretsIdFileName))
                File.Delete(SecretsIdFileName);
            string idDirectory = Path.GetDirectoryName(SecretsIdFileName);
            if (!Directory.Exists(idDirectory))
                Directory.CreateDirectory(idDirectory);
            SecretsFileName = Path.Combine(Environment.CurrentDirectory, "UserSecretsTest_" + Path.GetRandomFileName() + ".xml");
            if (File.Exists(SecretsFileName))
                File.Delete(SecretsFileName);
            CommonSecretsFileName = Path.Combine(Environment.CurrentDirectory, "UserSecretsTest_" + Path.GetRandomFileName() + ".xml");
            if (File.Exists(CommonSecretsFileName))
                File.Delete(CommonSecretsFileName);

            // Populate the secrets file with key/value pairs that are needed for common tests
            XDocument xdocFile = XDocument.Parse("<root><secrets ver=\"1.0\"><secret name=\"secretSource\" value=\"file\" /></secrets></root>");
            XDocument xdocCommonFile = XDocument.Parse("<root><secrets ver=\"1.0\"></secrets></root>");
            XDocument xdocId = XDocument.Parse("<root><secrets ver=\"1.0\"><secret name=\"secretSource\" value=\"id\" /></secrets></root>");
            foreach (string key in CommonBuilderTests.CommonKeyValuePairs)
            {
                XElement e = new XElement("secret");
                e.SetAttributeValue("name", key);
                e.SetAttributeValue("value", CommonBuilderTests.CommonKeyValuePairs[key]);
                xdocId.Root.Element("secrets").Add(new XElement(e));
                xdocFile.Root.Element("secrets").Add(e);
                xdocCommonFile.Root.Element("secrets").Add(e);
            }
            xdocId.Save(SecretsIdFileName);
            xdocFile.Save(SecretsFileName);
            xdocCommonFile.Save(CommonSecretsFileName);
        }

        private string GetSecretsFileFromID(string id)
        {
            MethodInfo getFileName = typeof(UserSecretsConfigBuilder).GetMethod("GetSecretsFileFromId", BindingFlags.NonPublic | BindingFlags.Instance);
            var tempBuilder = new UserSecretsConfigBuilder();
            string filename = getFileName.Invoke(tempBuilder, new object[] { id }) as string;
            return filename;
        }

        public void Dispose()
        {
            File.Delete(SecretsFileName);
            File.Delete(SecretsIdFileName);
            File.Delete(CommonSecretsFileName);
        }
    }

    public class UserSecretsTests : IClassFixture<UserSecretsFixture>
    {
        private readonly UserSecretsFixture _fixture;

        public UserSecretsTests(UserSecretsFixture fixture)
        {
            _fixture = fixture;
        }

        // ======================================================================
        //   CommonBuilderTests
        // ======================================================================
        [Fact]
        public void UserSecrets_GetValue()
        {
            CommonBuilderTests.GetValue(() => new UserSecretsConfigBuilder(), "SecretsGetValue",
                new NameValueCollection() { { "userSecretsFile", _fixture.CommonSecretsFileName } });
        }

        [Fact]
        public void UserSecrets_GetAllValues()
        {
            CommonBuilderTests.GetAllValues(() => new UserSecretsConfigBuilder(), "SecretsGetAll",
                new NameValueCollection() { { "userSecretsFile", _fixture.CommonSecretsFileName } });
        }

        [Fact]
        public void UserSecrets_ProcessConfigurationSection()
        {
            CommonBuilderTests.ProcessConfigurationSection(() => new UserSecretsConfigBuilder(), "SecretsProcessConfig",
                new NameValueCollection() { { "userSecretsFile", _fixture.CommonSecretsFileName } });
        }

        // ======================================================================
        //   UserSecrets Parameters
        // ======================================================================
        [Fact]
        public void UserSecrets_DefaultSettings()
        {
            var secrets = TestHelper.CreateBuilder<UserSecretsConfigBuilder>(() => new UserSecretsConfigBuilder(), "SecretsDefault",
                new NameValueCollection() { { "userSecretsFile", _fixture.SecretsFileName } });
            var mappedFile = Utils.MapPath(_fixture.SecretsFileName);

            // UserSecretsFile
            Assert.Equal(mappedFile, secrets.UserSecretsFile);
            Assert.Equal(_fixture.SecretsFileName, mappedFile);  // Doesn't really matter. But this should be the case in this test.

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
                new NameValueCollection() { { "userSecretsFile", _fixture.SecretsFileName } });
            var mappedFile = Utils.MapPath(_fixture.SecretsFileName);
            Assert.Equal(mappedFile, secrets.UserSecretsFile);
            Assert.Equal(_fixture.SecretsFileName, mappedFile);  // Doesn't really matter. But this should be the case in this test.
            Assert.Null(secrets.UserSecretsId);
            Assert.Equal("file", secrets.GetValue("secretSource"));

            // UserSecretsID
            secrets = TestHelper.CreateBuilder<UserSecretsConfigBuilder>(() => new UserSecretsConfigBuilder(), "SecretsSettings2",
                new NameValueCollection() { { "userSECRETSid", _fixture.SecretsId } });
            mappedFile = Utils.MapPath(_fixture.SecretsIdFileName);
            Assert.Equal(mappedFile, secrets.UserSecretsFile);
            Assert.Equal(_fixture.SecretsIdFileName, mappedFile);  // Doesn't really matter. But this should be the case in this test.
            Assert.Equal(_fixture.SecretsId, secrets.UserSecretsId);
            Assert.Equal("id", secrets.GetValue("secretSource"));

            // Both UserSecretsID and UserSecretsFile - Builder only looks at secretsFile
            secrets = TestHelper.CreateBuilder<UserSecretsConfigBuilder>(() => new UserSecretsConfigBuilder(), "SecretsSettings3",
                new NameValueCollection() { { "userSecretsId", _fixture.SecretsId }, { "usERSecRETsFile", _fixture.SecretsFileName } });
            mappedFile = Utils.MapPath(_fixture.SecretsFileName);
            Assert.Equal(mappedFile, secrets.UserSecretsFile);
            Assert.Equal(_fixture.SecretsFileName, mappedFile);  // Doesn't really matter. But this should be the case in this test.
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
            var exception = Record.Exception(() =>
            {
                TestHelper.CreateBuilder<UserSecretsConfigBuilder>(() => new UserSecretsConfigBuilder(), "SecretsErrors1",
                    new NameValueCollection() { { "enabled", enabled.ToString() } });
            });
            if (enabled == KeyValueEnabled.Enabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "SecretsErrors1");
            else
                Assert.Null(exception);

            // File does not exist
            exception = Record.Exception(() =>
            {
                TestHelper.CreateBuilder<UserSecretsConfigBuilder>(() => new UserSecretsConfigBuilder(), "SecretsErrors2",
                    new NameValueCollection() { { "enabled", enabled.ToString() }, { "userSecretsFile", "invalidFileName" } });
            });
            if (enabled == KeyValueEnabled.Enabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "SecretsErrors2");
            else
                Assert.Null(exception);

            // ID does not exist
            //  ... when Optional (default)
            exception = Record.Exception(() =>
            {
                TestHelper.CreateBuilder<UserSecretsConfigBuilder>(() => new UserSecretsConfigBuilder(), "SecretsErrors3",
                    new NameValueCollection() { { "enabled", enabled.ToString() }, { "userSecretsId", "invalidId" } });
            });
            if (enabled == KeyValueEnabled.Enabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "SecretsErrors3");
            else
                Assert.Null(exception);
        }
    }
}
