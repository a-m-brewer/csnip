using AwesomeAssertions;
using CSnip.Services;

namespace CSnip.Tests.Services;

[TestFixture]
public class TemplateServiceTests
{
    private TemplateService _sut = null!;

    [SetUp]
    public void SetUp() => _sut = new TemplateService();

    // --- ParseTemplates ---

    [Test]
    public void ParseTemplates_NoTemplates_ReturnsEmpty()
    {
        _sut.ParseTemplates("git status").Should().BeEmpty();
    }

    [Test]
    public void ParseTemplates_SingleTemplateWithoutDefault_ReturnsVariableWithNullDefault()
    {
        var result = _sut.ParseTemplates("ssh <hostname>");

        result.Should().HaveCount(1);
        result[0].Key.Should().Be("hostname");
        result[0].Default.Should().BeNull();
    }

    [Test]
    public void ParseTemplates_SingleTemplateWithDefault_ReturnsVariableWithDefault()
    {
        var result = _sut.ParseTemplates("ip a s <if-name=eth0>");

        result.Should().HaveCount(1);
        result[0].Key.Should().Be("if-name");
        result[0].Default.Should().Be("eth0");
    }

    [Test]
    public void ParseTemplates_MultipleTemplates_ReturnsAllInOrder()
    {
        var result = _sut.ParseTemplates("ssh <user=admin>@<hostname> -p <port=22>");

        result.Should().HaveCount(3);
        result[0].Key.Should().Be("user");
        result[0].Default.Should().Be("admin");
        result[1].Key.Should().Be("hostname");
        result[1].Default.Should().BeNull();
        result[2].Key.Should().Be("port");
        result[2].Default.Should().Be("22");
    }

    [Test]
    public void ParseTemplates_DuplicateKey_ReturnsFirstOccurrenceOnly()
    {
        var result = _sut.ParseTemplates("echo <value> && echo <value>");

        result.Should().HaveCount(1);
        result[0].Key.Should().Be("value");
    }

    [Test]
    public void ParseTemplates_EmptyDefault_ReturnsVariableWithEmptyStringDefault()
    {
        var result = _sut.ParseTemplates("cmd <flag=>");

        result.Should().HaveCount(1);
        result[0].Key.Should().Be("flag");
        result[0].Default.Should().Be("");
    }

    // --- ApplyTemplates ---

    [Test]
    public void ApplyTemplates_NoTemplates_ReturnsOriginalCommand()
    {
        var result = _sut.ApplyTemplates("git status", new Dictionary<string, string>());

        result.Should().Be("git status");
    }

    [Test]
    public void ApplyTemplates_SingleTemplateNoDefault_ReplacesWithValue()
    {
        var result = _sut.ApplyTemplates("ssh <hostname>", new Dictionary<string, string> { ["hostname"] = "server1" });

        result.Should().Be("ssh server1");
    }

    [Test]
    public void ApplyTemplates_TemplateWithDefault_ReplacesWithProvidedValue()
    {
        var result = _sut.ApplyTemplates("ip a s <if-name=eth0>", new Dictionary<string, string> { ["if-name"] = "wlan0" });

        result.Should().Be("ip a s wlan0");
    }

    [Test]
    public void ApplyTemplates_MultipleTemplates_ReplacesAll()
    {
        var values = new Dictionary<string, string> { ["user"] = "root", ["hostname"] = "server1", ["port"] = "2222" };

        var result = _sut.ApplyTemplates("ssh <user=admin>@<hostname> -p <port=22>", values);

        result.Should().Be("ssh root@server1 -p 2222");
    }

    [Test]
    public void ApplyTemplates_DuplicateKeyOccurrences_ReplacesAllOccurrences()
    {
        var result = _sut.ApplyTemplates("echo <value> && echo <value>", new Dictionary<string, string> { ["value"] = "hello" });

        result.Should().Be("echo hello && echo hello");
    }

    [Test]
    public void ApplyTemplates_MissingKeyInValues_LeavesTemplateUnchanged()
    {
        var result = _sut.ApplyTemplates("ssh <hostname>", new Dictionary<string, string>());

        result.Should().Be("ssh <hostname>");
    }
}
