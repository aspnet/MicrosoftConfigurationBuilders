using System;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using Microsoft.Configuration.ConfigurationBuilders;
using Xunit;

namespace Test
{
    public class KeyPerFileFixture : IDisposable
    {
        public string FsRoot { get; private set; }
        public string FsRootCommon { get; private set; }
        public string FsRootKVP { get; private set; }

        public KeyPerFileFixture()
        {
            // Get a clean KeyPerFile directory
            FsRoot = Path.Combine(Environment.CurrentDirectory, "KeyPerFileTest_" + Path.GetRandomFileName());
            FsRootCommon = Path.Combine(FsRoot, "common");
            FsRootKVP = Path.Combine(FsRoot, "kvp");
            if (Directory.Exists(FsRoot))
                Directory.Delete(FsRoot, true);
            Directory.CreateDirectory(FsRoot);
            Directory.CreateDirectory(FsRootCommon);
            Directory.CreateDirectory(FsRootKVP);

            // Populate the filesystem with key/value pairs that are needed for common tests
            foreach (string key in CommonBuilderTests.CommonKeyValuePairs)
                File.WriteAllText(Path.Combine(FsRootCommon, key), CommonBuilderTests.CommonKeyValuePairs[key]);

            // Also add some more of our own stuff for KeyPerFile-specific tests
            File.WriteAllText(Path.Combine(FsRootKVP, "testkey"), "simple test value");
            File.WriteAllText(Path.Combine(FsRootKVP, "dash-testkey"), "test value with dash");
            File.WriteAllText(Path.Combine(FsRootKVP, "ignore.testkey"), "default hidden test value");
            Directory.CreateDirectory(Path.Combine(FsRootKVP, "subFeature"));
            File.WriteAllText(Path.Combine(FsRootKVP, "subFeature", "testkey"), "subFeature value");
            File.WriteAllText(Path.Combine(FsRootKVP, "subFeature", "dash-testkey"), "subFeature dash value");
            File.WriteAllText(Path.Combine(FsRootKVP, "subFeature", "ignore.testkey"), "default subFeature hidden value");
            Directory.CreateDirectory(Path.Combine(FsRootKVP, "ignore"));
            File.WriteAllText(Path.Combine(FsRootKVP, "ignore", "testkey"), "visible test value");
            File.WriteAllText(Path.Combine(FsRootKVP, "ignore", ".testkey"), "hopefully not hidden test value");
            File.WriteAllText(Path.Combine(FsRootKVP, "ignore", "-testkey"), "hopefully not hidden dash test value");
            Directory.CreateDirectory(Path.Combine(FsRootKVP, "dash"));
            File.WriteAllText(Path.Combine(FsRootKVP, "dash", "testkey"), "conflict key from dash folder");
            File.WriteAllText(Path.Combine(FsRootKVP, "dash", "dir--testkey"), "double conflict test value from plain dash folder");
            Directory.CreateDirectory(Path.Combine(FsRootKVP, "dash--dir"));
            File.WriteAllText(Path.Combine(FsRootKVP, "dash--dir", "testkey"), "double conflict test value from dash-testkey folder");
            Directory.CreateDirectory(Path.Combine(FsRootKVP, "dash-"));
            File.WriteAllText(Path.Combine(FsRootKVP, "dash-", "testkey"), "double delimiter key from dash- folder");
        }

        public void Dispose()
        {
            Directory.Delete(FsRoot, true);
        }
    }

    public class KeyPerFileTests : IClassFixture<KeyPerFileFixture>
    {
        private readonly KeyPerFileFixture _fixture;

        public KeyPerFileTests(KeyPerFileFixture fixture)
        {
            _fixture = fixture;
        }

        // ======================================================================
        //   CommonBuilderTests
        // ======================================================================
        [Fact]
        public void KeyPerFile_GetValue()
        {
            CommonBuilderTests.GetValue(() => new KeyPerFileConfigBuilder(), "KeyPerFileGetValue",
                new NameValueCollection() { { "directoryPath", _fixture.FsRootCommon } });
        }

        [Fact]
        public void KeyPerFile_GetAllValues()
        {
            CommonBuilderTests.GetAllValues(() => new KeyPerFileConfigBuilder(), "KeyPerFileGetAll",
                new NameValueCollection() { { "directoryPath", _fixture.FsRootCommon } });
        }

        [Fact]
        public void KeyPerFile_ProcessConfigurationSection()
        {
            CommonBuilderTests.ProcessConfigurationSection(() => new KeyPerFileConfigBuilder(), "KeyPerFileProcessConfig",
                new NameValueCollection() { { "directoryPath", _fixture.FsRootCommon } });
        }

        // ======================================================================
        //   KeyPerFile parameters
        // ======================================================================
        [Fact]
        public void KeyPerFile_DefaultSettings()
        {
            var builder = TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileDefault",
                new NameValueCollection() { { "directoryPath", _fixture.FsRootKVP } });

            // DirectoryPath
            var mappedRoot = Utils.MapPath(_fixture.FsRootKVP);
            Assert.Equal(mappedRoot, builder.DirectoryPath);
            Assert.Equal(_fixture.FsRootKVP, mappedRoot);  // Doesn't really matter. But this should be the case in this test.

            // KeyDelimiter
            Assert.Null(builder.KeyDelimiter);

            // IgnorePrefix
            Assert.Equal("ignore.", builder.IgnorePrefix);

            // Enabled
            Assert.Equal(KeyValueEnabled.Enabled, builder.Enabled);

            // CharacterMap
            Assert.Empty(builder.CharacterMap);
        }

        [Fact]
        public void KeyPerFile_IgnorePrefix()
        {
            // DirectoryPath, KeyDelimiter, IgnorePrefix attributes are case insensitive
            var builder = TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileIgPrefix1",
                new NameValueCollection() { { "diRECTOrYpath", _fixture.FsRootKVP }, { "keYdeLIMITer", "--" }, { "igNOreprefiX", "you-cant-see-me+" } });
            var mappedPath = Utils.MapPath(_fixture.FsRootKVP);
            Assert.Equal(_fixture.FsRootKVP, mappedPath);    // Does not have to be true functionally speaking, but it should be true here.
            Assert.Equal(mappedPath, builder.DirectoryPath);
            Assert.Equal("--", builder.KeyDelimiter);
            Assert.Equal("you-cant-see-me+", builder.IgnorePrefix);

            // IgnorePrefix works single-level - GetAllValues()
            builder = TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileIgPrefix2",
                new NameValueCollection() { { "directoryPath", _fixture.FsRootKVP }, { "ignorePrefix", "ignore." } });
            var allValues = builder.GetAllValues("");
            Assert.Null(builder.KeyDelimiter);
            Assert.Equal("ignore.", builder.IgnorePrefix);
            Assert.Equal(2, allValues.Count);
            Assert.Equal("simple test value", TestHelper.GetValueFromCollection(allValues, "testkey"));
            Assert.Equal("test value with dash", TestHelper.GetValueFromCollection(allValues, "dash-testkey"));
            Assert.Null(TestHelper.GetValueFromCollection(allValues, "ignore.testkey"));

            // IgnorePrefix works single-level - GetValue()
            builder = TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileIgPrefix3",
                new NameValueCollection() { { "directoryPath", _fixture.FsRootKVP }, { "ignorePrefix", "ignore." } });
            Assert.Null(builder.KeyDelimiter);
            Assert.Equal("ignore.", builder.IgnorePrefix);
            Assert.Equal("simple test value", builder.GetValue("testkey"));
            Assert.Equal("test value with dash", builder.GetValue("dash-testkey"));
            Assert.Null(builder.GetValue("ignore.testkey"));

            // IgnorePrefix works multi-level - GetAllValues()
            builder = TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileIgPrefix4",
                new NameValueCollection() { { "directoryPath", _fixture.FsRootKVP }, { "keyDelimiter", ":" }, { "ignorePrefix", "ignore." } });
            allValues = builder.GetAllValues("");
            Assert.Equal(":", builder.KeyDelimiter);
            Assert.Equal("ignore.", builder.IgnorePrefix);
            Assert.Equal(11, allValues.Count);
            Assert.Equal("simple test value", TestHelper.GetValueFromCollection(allValues, "testkey"));
            Assert.Equal("test value with dash", TestHelper.GetValueFromCollection(allValues, "dash-testkey"));
            Assert.Null(TestHelper.GetValueFromCollection(allValues, "ignore.testkey"));
            Assert.Equal("subFeature value", TestHelper.GetValueFromCollection(allValues, "subFeature:testkey"));
            Assert.Equal("subFeature dash value", TestHelper.GetValueFromCollection(allValues, "subFeature:dash-testkey"));
            Assert.Null(TestHelper.GetValueFromCollection(allValues, "subFeature:ignore.testkey"));
            Assert.Equal("visible test value", TestHelper.GetValueFromCollection(allValues, "ignore:testkey"));
            Assert.Equal("hopefully not hidden test value", TestHelper.GetValueFromCollection(allValues, "ignore:.testkey"));
            Assert.Equal("hopefully not hidden dash test value", TestHelper.GetValueFromCollection(allValues, "ignore:-testkey"));
            Assert.Equal("conflict key from dash folder", TestHelper.GetValueFromCollection(allValues, "dash:testkey"));
            Assert.Equal("double conflict test value from plain dash folder", TestHelper.GetValueFromCollection(allValues, "dash:dir--testkey"));
            Assert.Equal("double conflict test value from dash-testkey folder", TestHelper.GetValueFromCollection(allValues, "dash--dir:testkey"));

            // IgnorePrefix works multi-level - GetValue()
            builder = TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileIgPrefix5",
                new NameValueCollection() { { "directoryPath", _fixture.FsRootKVP }, { "keyDelimiter", ":" }, { "ignorePrefix", "ignore." } });
            Assert.Equal(":", builder.KeyDelimiter);
            Assert.Equal("ignore.", builder.IgnorePrefix);
            Assert.Equal("simple test value", builder.GetValue("testkey"));
            Assert.Equal("test value with dash", builder.GetValue("dash-testkey"));
            Assert.Null(builder.GetValue("ignore.testkey"));
            Assert.Equal("subFeature value", builder.GetValue("subFeature:testkey"));
            Assert.Equal("subFeature dash value", builder.GetValue("subFeature:dash-testkey"));
            Assert.Null(builder.GetValue("subFeature:ignore.testkey"));

            // IgnorePrefix is "" - Single level - GetAllValues()
            builder = TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileIgPrefix6",
                new NameValueCollection() { { "directoryPath", _fixture.FsRootKVP }, { "ignorePrefix", "" } });
            allValues = builder.GetAllValues("");
            Assert.Null(builder.KeyDelimiter);
            Assert.Equal("", builder.IgnorePrefix);
            Assert.Equal(3, allValues.Count);
            Assert.Equal("simple test value", TestHelper.GetValueFromCollection(allValues, "testkey"));
            Assert.Equal("test value with dash", TestHelper.GetValueFromCollection(allValues, "dash-testkey"));
            Assert.Equal("default hidden test value", TestHelper.GetValueFromCollection(allValues, "ignore.testkey"));

            // IgnorePrefix is "" - Single level - GetValue()
            builder = TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileIgPrefix7",
                new NameValueCollection() { { "directoryPath", _fixture.FsRootKVP }, { "ignorePrefix", "" } });
            Assert.Null(builder.KeyDelimiter);
            Assert.Equal("", builder.IgnorePrefix);
            Assert.Equal("simple test value", builder.GetValue("testkey"));
            Assert.Equal("test value with dash", builder.GetValue("dash-testkey"));
            Assert.Equal("default hidden test value", builder.GetValue("ignore.testkey"));

            // IgnorePrefix is "" - Multi-level - GetValue()
            builder = TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileIgPrefix8",
                new NameValueCollection() { { "directoryPath", _fixture.FsRootKVP }, { "keyDelimiter", ":" }, { "ignorePrefix", "" } });
            allValues = builder.GetAllValues("");
            Assert.Equal(":", builder.KeyDelimiter);
            Assert.Equal("", builder.IgnorePrefix);
            Assert.Equal(13, allValues.Count);
            Assert.Equal("simple test value", TestHelper.GetValueFromCollection(allValues, "testkey"));
            Assert.Equal("test value with dash", TestHelper.GetValueFromCollection(allValues, "dash-testkey"));
            Assert.Equal("default hidden test value", TestHelper.GetValueFromCollection(allValues, "ignore.testkey"));
            Assert.Equal("subFeature value", TestHelper.GetValueFromCollection(allValues, "subFeature:testkey"));
            Assert.Equal("subFeature dash value", TestHelper.GetValueFromCollection(allValues, "subFeature:dash-testkey"));
            Assert.Equal("default subFeature hidden value", TestHelper.GetValueFromCollection(allValues, "subFeature:ignore.testkey"));
            Assert.Equal("visible test value", TestHelper.GetValueFromCollection(allValues, "ignore:testkey"));
            Assert.Equal("hopefully not hidden test value", TestHelper.GetValueFromCollection(allValues, "ignore:.testkey"));
            Assert.Equal("hopefully not hidden dash test value", TestHelper.GetValueFromCollection(allValues, "ignore:-testkey"));
            Assert.Equal("conflict key from dash folder", TestHelper.GetValueFromCollection(allValues, "dash:testkey"));
            Assert.Equal("double conflict test value from plain dash folder", TestHelper.GetValueFromCollection(allValues, "dash:dir--testkey"));
            Assert.Equal("double conflict test value from dash-testkey folder", TestHelper.GetValueFromCollection(allValues, "dash--dir:testkey"));

            // IgnorePrefix is "" - Multi-level - GetValue()
            builder = TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileIgPrefix9",
                new NameValueCollection() { { "directoryPath", _fixture.FsRootKVP }, { "keyDelimiter", ":" }, { "ignorePrefix", "" } });
            Assert.Equal(":", builder.KeyDelimiter);
            Assert.Equal("", builder.IgnorePrefix);
            Assert.Equal("simple test value", builder.GetValue("testkey"));
            Assert.Equal("test value with dash", builder.GetValue("dash-testkey"));
            Assert.Equal("default hidden test value", builder.GetValue("ignore.testkey"));
            Assert.Equal("subFeature value", builder.GetValue("subFeature:testkey"));
            Assert.Equal("subFeature dash value", builder.GetValue("subFeature:dash-testkey"));
            Assert.Equal("default subFeature hidden value", builder.GetValue("subFeature:ignore.testkey"));
        }

        [Fact]
        public void KeyPerFile_KeyDelimiter()
        {
            // DirectoryPath, KeyDelimiter, IgnorePrefix attributes are case insensitive
            var builder = TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileDelimiter1",
                new NameValueCollection() { { "diRECTOrYpath", _fixture.FsRootKVP }, { "keYdeLIMITer", "--" }, { "igNOreprefiX", "you-cant-see-me+" } });
            var mappedPath = Utils.MapPath(_fixture.FsRootKVP);
            Assert.Equal(_fixture.FsRootKVP, mappedPath);    // Does not have to be true functionally speaking, but it should be true here.
            Assert.Equal(mappedPath, builder.DirectoryPath);
            Assert.Equal("--", builder.KeyDelimiter);
            Assert.Equal("you-cant-see-me+", builder.IgnorePrefix);

            // keyDelimiter is null => Only top-level settings available
            builder = TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileDelimiter2",
                new NameValueCollection() { { "directoryPath", _fixture.FsRootKVP } });
            var allValues = builder.GetAllValues("");
            Assert.Null(builder.KeyDelimiter);
            Assert.Equal("ignore.", builder.IgnorePrefix);
            Assert.Equal(2, allValues.Count);
            Assert.Equal("simple test value", TestHelper.GetValueFromCollection(allValues, "testkey"));
            Assert.Equal("test value with dash", TestHelper.GetValueFromCollection(allValues, "dash-testkey"));
            Assert.Null(TestHelper.GetValueFromCollection(allValues, "ignore.testkey"));

            // keyDelimiter not null => multi-level settings available (no delimiter conflict)
            builder = TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileDelimiter3",
                new NameValueCollection() { { "directoryPath", _fixture.FsRootKVP }, { "keyDelimiter", ":" } });
            allValues = builder.GetAllValues("");
            Assert.Equal(":", builder.KeyDelimiter);
            Assert.Equal("ignore.", builder.IgnorePrefix);
            Assert.Equal(11, allValues.Count);
            Assert.Equal("simple test value", TestHelper.GetValueFromCollection(allValues, "testkey"));
            Assert.Equal("test value with dash", TestHelper.GetValueFromCollection(allValues, "dash-testkey"));
            Assert.Null(TestHelper.GetValueFromCollection(allValues, "ignore.testkey"));
            Assert.Equal("subFeature value", TestHelper.GetValueFromCollection(allValues, "subFeature:testkey"));
            Assert.Equal("subFeature dash value", TestHelper.GetValueFromCollection(allValues, "subFeature:dash-testkey"));
            Assert.Null(TestHelper.GetValueFromCollection(allValues, "subFeature:ignore.testkey"));
            Assert.Equal("visible test value", TestHelper.GetValueFromCollection(allValues, "ignore:testkey"));
            Assert.Equal("hopefully not hidden test value", TestHelper.GetValueFromCollection(allValues, "ignore:.testkey"));
            Assert.Equal("hopefully not hidden dash test value", TestHelper.GetValueFromCollection(allValues, "ignore:-testkey"));
            Assert.Equal("conflict key from dash folder", TestHelper.GetValueFromCollection(allValues, "dash:testkey"));
            Assert.Equal("double conflict test value from plain dash folder", TestHelper.GetValueFromCollection(allValues, "dash:dir--testkey"));
            Assert.Equal("double conflict test value from dash-testkey folder", TestHelper.GetValueFromCollection(allValues, "dash--dir:testkey"));

            // ignorePrefix filters ignored files in GetValue() as well
            builder = TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileDelimiter4",
                new NameValueCollection() { { "directoryPath", _fixture.FsRootKVP }, { "keyDelimiter", ":" } });
            Assert.Equal(":", builder.KeyDelimiter);
            Assert.Equal("ignore.", builder.IgnorePrefix);
            Assert.Equal("simple test value", builder.GetValue("testkey"));
            Assert.Equal("test value with dash", builder.GetValue("dash-testkey"));
            Assert.Null(builder.GetValue("ignore.testkey"));
            Assert.Equal("subFeature value", builder.GetValue("subFeature:testkey"));
            Assert.Equal("subFeature dash value", builder.GetValue("subFeature:dash-testkey"));
            Assert.Null(builder.GetValue("subFeature:ignore.testkey"));

            // keyDelimiter in file names
            builder = TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileDelimiter5",
                new NameValueCollection() { { "directoryPath", _fixture.FsRootKVP }, { "keyDelimiter", "-" } });
            Assert.Equal("-", builder.KeyDelimiter);
            Assert.Equal("ignore.", builder.IgnorePrefix);
            Assert.Equal("simple test value", builder.GetValue("testkey"));
            //Assert.Equal("test value with dash", builder.GetValue("dash-testkey"));   // Has conflict. Covered in Errors test.
            Assert.Null(builder.GetValue("ignore.testkey"));
            Assert.Equal("subFeature value", builder.GetValue("subFeature-testkey"));
            Assert.Equal("subFeature dash value", builder.GetValue("subFeature-dash-testkey"));
            Assert.Null(builder.GetValue("subFeature-ignore.testkey"));
            Assert.Equal("visible test value", builder.GetValue("ignore-testkey"));
            Assert.Equal("hopefully not hidden dash test value", builder.GetValue("ignore--testkey"));  // This is also a double delimiter
            Assert.Equal("double delimiter key from dash- folder", builder.GetValue("dash--testkey"));
            
            // keyDelimiter matches end of ignorePrefix in GetValue() (ie, do you ignore 'hide-testkey' or is that a multi-level [hide, testkey] lookup?)
            builder = TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileDelimiter6",
                new NameValueCollection() { { "directoryPath", _fixture.FsRootKVP }, { "keyDelimiter", "." } });
            Assert.Equal(".", builder.KeyDelimiter);
            Assert.Equal("ignore.", builder.IgnorePrefix);
            Assert.Equal("simple test value", builder.GetValue("testkey"));
            Assert.Equal("test value with dash", builder.GetValue("dash-testkey"));
            //Assert.Null(builder.GetValue("ignore.testkey"));  // Ignored value also matches valid value from subdir
            Assert.Equal("subFeature value", builder.GetValue("subFeature.testkey"));
            Assert.Equal("subFeature dash value", builder.GetValue("subFeature.dash-testkey"));
            Assert.Null(builder.GetValue("subFeature.ignore.testkey"));
            Assert.Equal("visible test value", builder.GetValue("ignore.testkey"));
            Assert.Equal("hopefully not hidden dash test value", builder.GetValue("ignore.-testkey"));
            // A Windows peculiarity: '.' at the end of a directory name is ignored. So here we match 'ignore/.testkey'
            // as well as 'ignore./testkey' because windows treats the latter like 'ignore/testkey'.
            //Assert.Equal("hopefully not hidden test value", builder.GetValue("ignore..testkey"));


            // keyDelimiter matches end of ignorePrefix in GetAllValues() (ie, do you ignore 'hide-testkey' or is that a multi-level [hide, testkey] lookup?)
            builder = TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileDelimiter7",
                new NameValueCollection() { { "directoryPath", _fixture.FsRootKVP }, { "keyDelimiter", "." } });
            allValues = builder.GetAllValues("");
            Assert.Equal(".", builder.KeyDelimiter);
            Assert.Equal("ignore.", builder.IgnorePrefix);
            Assert.Equal(11, allValues.Count);
            Assert.Equal("simple test value", TestHelper.GetValueFromCollection(allValues, "testkey"));
            Assert.Equal("test value with dash", TestHelper.GetValueFromCollection(allValues, "dash-testkey"));
            //Assert.Null(TestHelper.GetValueFromCollection(allValues, "ignore.testkey"));  // Ignored value also matches valid value from subdir
            Assert.Equal("subFeature value", TestHelper.GetValueFromCollection(allValues, "subFeature.testkey"));
            Assert.Equal("subFeature dash value", TestHelper.GetValueFromCollection(allValues, "subFeature.dash-testkey"));
            Assert.Null(TestHelper.GetValueFromCollection(allValues, "subFeature.ignore.testkey"));
            Assert.Equal("visible test value", TestHelper.GetValueFromCollection(allValues, "ignore.testkey"));
            Assert.Equal("hopefully not hidden test value", TestHelper.GetValueFromCollection(allValues, "ignore..testkey"));
            Assert.Equal("hopefully not hidden dash test value", TestHelper.GetValueFromCollection(allValues, "ignore.-testkey"));
            Assert.Equal("conflict key from dash folder", TestHelper.GetValueFromCollection(allValues, "dash.testkey"));
            Assert.Equal("double conflict test value from plain dash folder", TestHelper.GetValueFromCollection(allValues, "dash.dir--testkey"));
            Assert.Equal("double conflict test value from dash-testkey folder", TestHelper.GetValueFromCollection(allValues, "dash--dir.testkey"));
            Assert.Equal("double delimiter key from dash- folder", TestHelper.GetValueFromCollection(allValues, "dash-.testkey"));
        }

        // TODO
        // Prefix conflict with delimiter and/or ignore prefix
        // How KPF interacts with charMap would be interesting to explore

        // ======================================================================
        //   Errors
        // ======================================================================
        [Theory]
        [InlineData(KeyValueEnabled.Optional)]
        [InlineData(KeyValueEnabled.Enabled)]
        [InlineData(KeyValueEnabled.Disabled)]
        public void KeyPerFile_ErrorsOptional(KeyValueEnabled enabled)
        {
            // No directoryPath
            var exception = Record.Exception(() =>
            {
                TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileErrors1",
                    new NameValueCollection() { { "enabled", enabled.ToString() } });
            });
            if (enabled == KeyValueEnabled.Enabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "KeyPerFileErrors1");
            else
                Assert.Null(exception);

            // directoryPath not found
            exception = Record.Exception(() =>
            {
                TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileErrors2",
                    new NameValueCollection() { { "directoryPath", "invalid-Dir-Path" }, { "enabled", enabled.ToString() } });
            });
            if (enabled == KeyValueEnabled.Enabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "KeyPerFileErrors2");
            else
                Assert.Null(exception);

            // ignore prefix has invalid characters?
            exception = Record.Exception(() =>
            {
                TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileErrors3",
                    new NameValueCollection() { { "directoryPath", _fixture.FsRootKVP }, { "ignorePrefix", @"ig\n*re/." }, { "enabled", enabled.ToString() } });
            });
            Assert.Null(exception);
        }

        [Fact]
        public void KeyPerFile_ErrorsConflict()
        {
            // keyDelimiter conflicts with filename characters in GetValue() (parent/child)
            var builder = TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileConflict1",
                new NameValueCollection() { { "directoryPath", _fixture.FsRootKVP }, { "keyDelimiter", "-" }, { "enabled", "Enabled" } });
            Assert.Equal("-", builder.KeyDelimiter);
            Assert.Equal("simple test value", builder.GetValue("testkey"));
            Assert.Equal("subFeature value", builder.GetValue("subFeature-testkey"));
            var exception = Record.Exception(() =>
            {
                builder.GetValue("dash-testkey");
            });
            TestHelper.ValidateBasicException<ArgumentException>(exception);

            // keyDelimiter conflicts with filename characters in GetAllValues() (parent/child)
            builder = TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileConflict2",
                new NameValueCollection() { { "directoryPath", _fixture.FsRootKVP }, { "keyDelimiter", "-" }, { "enabled", "Enabled" } });
            exception = Record.Exception(() =>
            {
                builder.GetAllValues("");
            });
            TestHelper.ValidateBasicException<ArgumentException>(exception);

            // keyDelimiter conflicts with filename characters in GetValue() (siblings)
            builder = TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileConflict3",
                new NameValueCollection() { { "directoryPath", _fixture.FsRootKVP }, { "keyDelimiter", "--" }, { "enabled", "Enabled" } });
            Assert.Equal("--", builder.KeyDelimiter);
            Assert.Equal("simple test value", builder.GetValue("testkey"));
            Assert.Equal("subFeature value", builder.GetValue("subFeature--testkey"));
            Assert.Equal("test value with dash", builder.GetValue("dash-testkey"));
            Assert.Equal("conflict key from dash folder", builder.GetValue("dash--testkey"));   // Not a conflict in this '--' case
            exception = Record.Exception(() =>
            {
                builder.GetValue("dash--dir--testkey");
            });
            TestHelper.ValidateBasicException<ArgumentException>(exception);

            // keyDelimiter conflicts with filename characters in GetAllValues() (siblings)
            builder = TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileConflict4",
                new NameValueCollection() { { "directoryPath", _fixture.FsRootKVP }, { "keyDelimiter", "--" }, { "enabled", "Enabled" } });
            exception = Record.Exception(() =>
            {
                builder.GetAllValues("");
            });
            TestHelper.ValidateBasicException<ArgumentException>(exception);

            // Conflicts are not an optional failure
            builder = TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileConflict5",
                new NameValueCollection() { { "directoryPath", _fixture.FsRootKVP }, { "keyDelimiter", "-" }, { "enabled", "Optional" } });
            exception = Record.Exception(() =>
            {
                builder.GetValue("dash-testkey");
            });
            TestHelper.ValidateBasicException<ArgumentException>(exception);
            builder = TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileConflict6",
                new NameValueCollection() { { "directoryPath", _fixture.FsRootKVP }, { "keyDelimiter", "-" }, { "enabled", "Optional" } });
            exception = Record.Exception(() =>
            {
                builder.GetAllValues("");
            });
            TestHelper.ValidateBasicException<ArgumentException>(exception);

            // No conflicts when disabled - Strict
            var appSettings = new AppSettingsSection();
            appSettings.Settings.Add("testkey", "should not change");
            builder = TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileConflict7",
                new NameValueCollection() { { "directoryPath", _fixture.FsRootKVP }, { "keyDelimiter", "-" }, { "enabled", "Disabled" } });
            appSettings = (AppSettingsSection)builder.ProcessConfigurationSection(appSettings); // No exception
            Assert.Single(appSettings.Settings);
            Assert.Equal("should not change", appSettings.Settings["testkey"]?.Value);

            // No conflicts when disabled - Greedy
            builder = TestHelper.CreateBuilder<KeyPerFileConfigBuilder>(() => new KeyPerFileConfigBuilder(), "KeyPerFileConflict8",
                new NameValueCollection() { { "mode", "Greedy" }, { "directoryPath", _fixture.FsRootKVP }, { "keyDelimiter", "-" }, { "enabled", "Disabled" } });
            appSettings = (AppSettingsSection)builder.ProcessConfigurationSection(new AppSettingsSection()); // No exception
            Assert.Empty(appSettings.Settings);
        }
    }
}
