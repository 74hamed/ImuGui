using FluentAssertions;
using ImuGui.Core.Models;
using ImuGui.Core.Sources;
using ImuGui.Core.Tests.TestUtilities;
using Xunit;

namespace ImuGui.Core.Tests.Sources;

public class SerialSensorSourceTests
{
    private const string GoodLine1 = "0.1,0.2,0.3,0.0,0.0,-1.0,30,0,40,25.0";
    private const string GoodLine2 = "1.1,1.2,1.3,0.0,0.0,-1.0,30,0,40,25.1";

    private static SerialSensorOptions Options(bool autoReconnect = true) => new()
    {
        PortName = "COM3",
        BaudRate = 115200,
        AutoReconnect = autoReconnect,
        ReconnectDelay = TimeSpan.FromMilliseconds(20),
    };

    [Fact]
    public async Task Parses_lines_into_samples_and_discards_the_first_partial_line()
    {
        var connection = new ScriptedSerialConnection()
            .EnqueueLine("-1.0,25.0")   // partial frame typical right after opening
            .EnqueueLine(GoodLine1)
            .EnqueueLine(GoodLine2);
        var factory = new ScriptedSerialPortFactory(connection);
        var clock = new FakeClock();
        clock.Advance(TimeSpan.FromSeconds(1));
        using var source = new SerialSensorSource(Options(), factory, clock);
        using var probe = new SourceProbe(source);

        await source.StartAsync();
        source.ConnectionState.Should().Be(SensorConnectionState.Connected);
        await probe.WaitForSamplesAsync(2);
        await source.StopAsync();

        SensorSample[] samples = probe.Samples.ToArray();
        samples.Should().HaveCount(2);
        samples[0].Gyroscope.Should().Be(new Vector3(0.1, 0.2, 0.3));
        samples[1].Gyroscope.Should().Be(new Vector3(1.1, 1.2, 1.3));
        samples[0].Timestamp.Should().Be(TimeSpan.FromSeconds(1), "arrival is stamped from the clock");
        source.MalformedLineCount.Should().Be(0, "the discarded first line is not an error");
    }

    [Fact]
    public async Task Malformed_lines_are_counted_and_skipped_while_headers_are_ignored()
    {
        var connection = new ScriptedSerialConnection()
            .EnqueueLine("partial")
            .EnqueueLine(GoodLine1)
            .EnqueueLine("1,2,3")                                           // wrong field count
            .EnqueueLine("0.1,oops,0.3,0.0,0.0,-1.0,30,0,40,25.0")           // non-numeric
            .EnqueueLine(SensorLineParser.ExpectedHeader)                    // device boot header echo
            .EnqueueLine(GoodLine2);
        using var source = new SerialSensorSource(
            Options(), new ScriptedSerialPortFactory(connection), new FakeClock());
        using var probe = new SourceProbe(source);

        await source.StartAsync();
        await probe.WaitForSamplesAsync(2);
        await source.StopAsync();

        probe.Samples.Should().HaveCount(2);
        source.MalformedLineCount.Should().Be(2, "header echoes are skipped without counting as errors");
    }

    [Fact]
    public async Task Device_loss_triggers_reconnect_and_sample_flow_resumes()
    {
        var firstConnection = new ScriptedSerialConnection()
            .EnqueueLine("partial")
            .EnqueueLine(GoodLine1)
            .EnqueueThrow(new IOException("The device does not recognize the command."));
        var secondConnection = new ScriptedSerialConnection()
            .EnqueueLine("partial-after-reconnect")
            .EnqueueLine(GoodLine2);
        var factory = new ScriptedSerialPortFactory(firstConnection, secondConnection);
        using var source = new SerialSensorSource(Options(), factory, new FakeClock());
        using var probe = new SourceProbe(source);

        await source.StartAsync();
        await probe.WaitForSamplesAsync(2);

        probe.StateTransitions.Should().Contain(SensorConnectionState.Reconnecting);
        probe.Errors.Should().Contain(message => message.Contains("reconnecting"));
        source.ConnectionState.Should().Be(SensorConnectionState.Connected);
        factory.CreateCallCount.Should().Be(2);
        firstConnection.WasDisposed.Should().BeTrue("the dead connection is torn down");

        await source.StopAsync();
        secondConnection.WasClosed.Should().BeTrue();
    }

    [Fact]
    public async Task Device_loss_without_auto_reconnect_faults_the_source()
    {
        var connection = new ScriptedSerialConnection()
            .EnqueueLine("partial")
            .EnqueueLine(GoodLine1)
            .EnqueueThrow(new IOException("Unplugged."));
        using var source = new SerialSensorSource(
            Options(autoReconnect: false), new ScriptedSerialPortFactory(connection), new FakeClock());
        using var probe = new SourceProbe(source);

        await source.StartAsync();
        await probe.WaitForStateAsync(SensorConnectionState.Faulted);

        probe.Errors.Should().NotBeEmpty();
        probe.Errors.Should().Contain(message => message.Contains("COM3"));
    }

    [Fact]
    public async Task Open_failure_produces_an_actionable_error_listing_available_ports()
    {
        var connection = new ScriptedSerialConnection
        {
            OpenException = new UnauthorizedAccessException("Access to the port 'COM3' is denied."),
        };
        var factory = new ScriptedSerialPortFactory(connection) { AvailablePorts = ["COM4", "COM7"] };
        using var source = new SerialSensorSource(Options(), factory, new FakeClock());

        Func<Task> act = () => source.StartAsync();

        (await act.Should().ThrowAsync<SensorSourceException>())
            .WithMessage("*COM3*")
            .WithMessage("*COM4, COM7*");
        source.ConnectionState.Should().Be(SensorConnectionState.Disconnected);
    }

    [Fact]
    public async Task Stopping_closes_the_port_and_reports_disconnected()
    {
        var connection = new ScriptedSerialConnection()
            .EnqueueLine("partial")
            .EnqueueLine(GoodLine1);
        using var source = new SerialSensorSource(
            Options(), new ScriptedSerialPortFactory(connection), new FakeClock());
        using var probe = new SourceProbe(source);

        await source.StartAsync();
        await probe.WaitForSamplesAsync(1);
        await source.StopAsync();

        source.ConnectionState.Should().Be(SensorConnectionState.Disconnected);
        connection.WasClosed.Should().BeTrue();
        connection.WasDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task Starting_twice_without_stopping_is_rejected()
    {
        var connection = new ScriptedSerialConnection().EnqueueLine("partial").EnqueueLine(GoodLine1);
        using var source = new SerialSensorSource(
            Options(), new ScriptedSerialPortFactory(connection), new FakeClock());

        await source.StartAsync();
        Func<Task> act = () => source.StartAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
        await source.StopAsync();
    }
}
