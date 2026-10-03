using System.Threading.Channels;
using Backend_API.Models;

namespace Backend_API.Services;

public class TelemetryChannel
{
    private readonly Channel<SensorTelemetry> _channel;

    public TelemetryChannel()
    {
        var options = new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        };
        _channel = Channel.CreateBounded<SensorTelemetry>(options);
    }

    public ChannelWriter<SensorTelemetry> Writer => _channel.Writer;
    public ChannelReader<SensorTelemetry> Reader => _channel.Reader;
}
