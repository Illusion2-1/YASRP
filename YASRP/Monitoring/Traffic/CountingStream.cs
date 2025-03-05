namespace YASRP.Monitoring.Traffic;

public class CountingStream : Stream {
    private readonly Stream _innerStream;
    private long _bytesRead;
    private long _bytesWritten;

    public CountingStream(Stream innerStream) {
        _innerStream = innerStream;
        _bytesRead = 0;
        _bytesWritten = 0;
    }

    public long BytesRead => _bytesRead;
    public long BytesWritten => _bytesWritten;

    public override bool CanRead => _innerStream.CanRead;
    public override bool CanSeek => _innerStream.CanSeek;
    public override bool CanWrite => _innerStream.CanWrite;
    public override long Length => _innerStream.Length;

    public override long Position {
        get => _innerStream.Position;
        set => _innerStream.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count) {
        var bytesRead = _innerStream.Read(buffer, offset, count);
        _bytesRead += bytesRead;
        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
        var bytesRead = await _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
        _bytesRead += bytesRead;
        return bytesRead;
    }

    public override void Write(byte[] buffer, int offset, int count) {
        _innerStream.Write(buffer, offset, count);
        _bytesWritten += count;
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
        await _innerStream.WriteAsync(buffer, offset, count, cancellationToken);
        _bytesWritten += count;
    }
    
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
        int bytesRead = await _innerStream.ReadAsync(buffer, cancellationToken);
        Interlocked.Add(ref _bytesRead, bytesRead);
        return bytesRead;
    }
    
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) {
        await _innerStream.WriteAsync(buffer, cancellationToken);
        Interlocked.Add(ref _bytesWritten, buffer.Length);
    }

    public override void Flush() {
        _innerStream.Flush();
    }

    public override long Seek(long offset, SeekOrigin origin) {
        return _innerStream.Seek(offset, origin);
    }

    public override void SetLength(long value) {
        _innerStream.SetLength(value);
    }

    protected override void Dispose(bool disposing) {
        if (disposing) _innerStream.Dispose();
        base.Dispose(disposing);
    }
}