using System.Threading.Channels;
using HyperMark.Models;
using HyperMark.Parsers;
using HyperMark.Storage;
using Microsoft.Extensions.Hosting;

namespace HyperMark.Web;

/// <summary>
/// 后台链接解析处理器
/// 使用 Channel 作为任务队列，IHostedService 管理生命周期
/// Link 是核心实体，Page 是 Link 的可选属性
/// </summary>
public class BackgroundLinkProcessor : IHostedService
{
    private readonly Channel<Link> _channel;
    private readonly UrlParser _parser;
    private readonly Repository _repository;
    private readonly FileLogger _logger;
    private Task? _consumerTask;

    public BackgroundLinkProcessor(UrlParser parser, Repository repository, FileLogger logger)
    {
        _parser = parser;
        _repository = repository;
        _logger = logger;
        _channel = Channel.CreateBounded<Link>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _consumerTask = Task.Run(async () =>
        {
            await foreach (var link in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    var page = _parser.Parse(link.Url);
                    if (!string.IsNullOrEmpty(page.HyperId))
                    {
                        // 更新已有 Link 的 Page 信息
                        _repository.UpdateLinkPage(link.Url, page);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"解析链接失败: {link.Url}");
                }
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.Writer.Complete();
        if (_consumerTask != null)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            try
            {
                await _consumerTask.WaitAsync(linked.Token);
            }
            catch (OperationCanceledException)
            {
                // 超时或被取消，忽略
            }
        }
    }

    public async Task Enqueue(Link link)
    {
        await _channel.Writer.WriteAsync(link);
    }
}
