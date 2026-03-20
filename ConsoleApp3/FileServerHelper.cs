
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ConsoleApp3;

public sealed class FileServer : IAsyncDisposable
{
    private const string FaviconFileName = "favicon.ico";
    private const string ServerUrlPrefix = "http://127.0.0.1:";
    private const string ServerUrlSuffix = "/";
    private const string HtmlStart = "<html><body>";
    private const string HtmlEnd = "</body></html>";
    private const string LinkStart = "<a href=\"";
    private const string LinkMiddle = "\">";
    private const string LinkEnd = "</a><br>";
    private const string RootPath = "/";
    private const string Slash = "/";
    private const string HeaderOk = "HTTP/1.1 200 OK\r\n";
    private const string HeaderLength = "Content-Length: ";
    private const string HeaderType = "Content-Type: ";
    private const string HtmlContentType = "text/html; charset=utf-8\r\n";
    private const string BinaryContentType = "application/octet-stream\r\n";
    private const string HeaderEnd = "\r\n";
    private const char Space = ' ';
    private const int RequestBufferSize = 4096;
    private const int PathStartIndex = 4;

    private readonly string _folder;
    private readonly CancellationTokenSource _cts = new();

    private TcpListener _listener = null!;
    private string _faviconPath = null!;
    private Task _runTask = Task.CompletedTask;
    private int _port;

    public string Url => ServerUrlPrefix + _port + ServerUrlSuffix;

    private FileServer(string folder)
    {
        _folder = folder;
        _faviconPath = Path.Combine(_folder, FaviconFileName);
        File.WriteAllBytesAsync(_faviconPath, []).Wait();
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
    }

    public static FileServer Start(string folder)
    {
        var fileServer = new FileServer(folder);
        fileServer.Run();

        return fileServer;
    }

    private void Run()
    {
        _runTask = RunInternalAsync();
    }

    public async Task StopAsync()
    {
        _cts.Cancel();
        _listener.Stop();
        await _runTask;
        File.Delete(_faviconPath);
        _cts.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private async Task RunInternalAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                using var stream = client.GetStream();

                string path = await ReadPathAsync(stream);
                byte[] response = BuildResponse(path);

                await stream.WriteAsync(response, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<string> ReadPathAsync(NetworkStream stream)
    {
        byte[] buffer = new byte[RequestBufferSize];
        await stream.ReadAsync(buffer, _cts.Token);

        int end = PathStartIndex;
        for (; buffer[end] != Space; end++) { }

        string path = Encoding.ASCII.GetString(buffer, PathStartIndex, end - PathStartIndex);
        return Uri.UnescapeDataString(path);
    }

    private byte[] BuildResponse(string path)
    {
        string fullPath = _folder + path;

        return Directory.Exists(fullPath)
            ? BuildDirectoryResponse(fullPath, path)
            : BuildFileResponse(fullPath);
    }

    private byte[] BuildDirectoryResponse(string fullPath, string path)
    {
        var entries = Directory.GetFileSystemEntries(fullPath);
        var html = new StringBuilder(HtmlStart);

        foreach (var entry in entries)
        {
            html.Append(BuildDirectoryEntry(entry, path));
        }

        html.Append(HtmlEnd);

        byte[] body = Encoding.UTF8.GetBytes(html.ToString());
        byte[] header = BuildHeader(body.Length, HtmlContentType);

        return [.. header, .. body];
    }

    private string BuildDirectoryEntry(string entry, string path)
    {
        string name = Path.GetFileName(entry);
        string url = BuildEntryUrl(path, name);

        return LinkStart + url + LinkMiddle + name + LinkEnd;
    }

    private string BuildEntryUrl(string path, string name)
    {
        return path == RootPath ? RootPath + name : path + Slash + name;
    }

    private byte[] BuildFileResponse(string fullPath)
    {
        byte[] body = File.ReadAllBytes(fullPath);
        byte[] header = BuildHeader(body.Length, BinaryContentType);

        return [.. header, .. body];
    }

    private byte[] BuildHeader(int contentLength, string contentType)
    {
        string header =
            HeaderOk +
            HeaderLength + contentLength + HeaderEnd +
            HeaderType + contentType +
            HeaderEnd;

        return Encoding.ASCII.GetBytes(header);
    }
}