using AwesomeAssertions;
using CSnip.Abstractions;
using CSnip.Pipeline;
using Moq;
using Moq.AutoMock;

namespace CSnip.Tests.Pipeline;

[TestFixture]
public class FormatResolverTests
{
    private AutoMocker _mocker = null!;
    private FormatResolver _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _mocker = new AutoMocker();
        _sut = _mocker.CreateInstance<FormatResolver>();
    }

    private Mock<IConsoleEnvironment> Env(bool outputRedirected)
    {
        var env = _mocker.GetMock<IConsoleEnvironment>();
        env.Setup(e => e.IsOutputRedirected).Returns(outputRedirected);
        return env;
    }

    [Test]
    public void Resolve_AutoAndTty_ReturnsText()
    {
        Env(outputRedirected: false);
        _sut.Resolve(OutputFormat.Auto, _mocker.Get<IConsoleEnvironment>()).Should().Be(OutputFormat.Text);
    }

    [Test]
    public void Resolve_AutoAndRedirected_ReturnsJson()
    {
        Env(outputRedirected: true);
        _sut.Resolve(OutputFormat.Auto, _mocker.Get<IConsoleEnvironment>()).Should().Be(OutputFormat.Json);
    }

    [Test]
    public void Resolve_ExplicitText_ReturnsText()
    {
        Env(outputRedirected: true);
        _sut.Resolve(OutputFormat.Text, _mocker.Get<IConsoleEnvironment>()).Should().Be(OutputFormat.Text);
    }

    [Test]
    public void Resolve_ExplicitJson_ReturnsJson()
    {
        Env(outputRedirected: false);
        _sut.Resolve(OutputFormat.Json, _mocker.Get<IConsoleEnvironment>()).Should().Be(OutputFormat.Json);
    }
}
