using System.Threading.Channels;
using Fireflies.GraphQL.Core.Json;

namespace Fireflies.GraphQL.Core;

public class ResultBuilder : IResultBuilder {
    private readonly bool _isWebsocket;
    private readonly CancellationTokenSource _cancellationToken;
    private readonly Channel<(string? Id, JsonWriter Writer)> _channel;
    private int _outstandingOperations;

    public ResultBuilder(bool isWebsocket, CancellationTokenSource cancellationToken) {
        _isWebsocket = isWebsocket;
        _cancellationToken = cancellationToken;
        _channel = Channel.CreateUnbounded<(string? Id, JsonWriter Writer)>(new UnboundedChannelOptions { SingleReader = true });
    }

    public async IAsyncEnumerator<(string? Id, byte[] Result)> GetAsyncEnumerator(CancellationToken cancellationToken = new()) {
        while(!_cancellationToken.IsCancellationRequested) {
            (string? Id, JsonWriter Writer) entry;
            try {
                await _channel.Reader.WaitToReadAsync(_cancellationToken.Token).ConfigureAwait(false);
                entry = await _channel.Reader.ReadAsync(_cancellationToken.Token).ConfigureAwait(false);
            } catch(OperationCanceledException) {
                break;
            }

            if(_isWebsocket) {
                yield return (entry.Id, await entry.Writer.GetBuffer().ConfigureAwait(false));
            } else {
                var newOutstandingOperations = Interlocked.Decrement(ref _outstandingOperations);
                if(newOutstandingOperations != 0)
                    continue;

                yield return (entry.Id, await entry.Writer.GetBuffer().ConfigureAwait(false));

                yield break;
            }
        }
    }

    public async Task PublishResult(string? id, JsonWriter writer) {
        await _channel.Writer.WriteAsync((id, writer), _cancellationToken.Token).ConfigureAwait(false);
    }

    public void Done() {
        _cancellationToken.Cancel();
    }

    public void IncreaseExpectedOperations(int i = 1) {
        if(!_isWebsocket)
            Interlocked.Add(ref _outstandingOperations, i);
    }
}