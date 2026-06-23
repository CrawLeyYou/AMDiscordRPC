using Microsoft.VisualStudio.TestTools.UnitTesting;
using static AMDiscordRPC.Globals;
using static AMDiscordRPC.Database;

namespace AMDiscordRPC.Tests;

[TestClass]
public class GlobalsTests
{
    [TestMethod]
    public void CheckDatabaseSemantics()
    {
        string expectedMinimumVersionString = "1.0.0";
        long expectedMinimumVersion = 4294967296;
        
        Assert.IsGreaterThanOrEqualTo(expectedMinimumVersion, schemeVersion.SemanticPack().Value);
        Assert.AreEqual(expectedMinimumVersionString.SemanticPack(), expectedMinimumVersion);
        Assert.AreEqual(expectedMinimumVersion.SemanticUnpack(), expectedMinimumVersionString);
    }
}