using System.Net;
using PhotoBooth.Server.Utilities;

namespace PhotoBooth.Server.Tests;

[TestClass]
public sealed class NetworkUtilitiesTests
{
    [TestMethod]
    public void IsLocalhost_WhenNull_ReturnsFalse()
    {
        Assert.IsFalse(NetworkUtilities.IsLocalhost(null));
    }

    [TestMethod]
    public void IsLocalhost_WhenIPv4Loopback_ReturnsTrue()
    {
        Assert.IsTrue(NetworkUtilities.IsLocalhost(IPAddress.Parse("127.0.0.1")));
    }

    [TestMethod]
    public void IsLocalhost_WhenIPv6Loopback_ReturnsTrue()
    {
        Assert.IsTrue(NetworkUtilities.IsLocalhost(IPAddress.Parse("::1")));
    }

    [TestMethod]
    public void IsLocalhost_WhenIPv4MappedIPv6Loopback_ReturnsTrue()
    {
        Assert.IsTrue(NetworkUtilities.IsLocalhost(IPAddress.Parse("::ffff:127.0.0.1")));
    }

    [TestMethod]
    public void IsLocalhost_WhenExternalIPv4_ReturnsFalse()
    {
        Assert.IsFalse(NetworkUtilities.IsLocalhost(IPAddress.Parse("192.168.1.100")));
    }

    [TestMethod]
    public void IsLocalhost_WhenExternalIPv6_ReturnsFalse()
    {
        Assert.IsFalse(NetworkUtilities.IsLocalhost(IPAddress.Parse("2001:db8::1")));
    }
}
