using System.Diagnostics;
using Analyzer;
using Api.Models;
using Api.Options;
using Api.Repositories;
using Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace VulnScanner.Tests;

public sealed class ScanBackgroundServiceTests
{
    [Theory]
    [InlineData(false, ScanStatus.Completed)]
    [InlineData(true, ScanStatus.Failed)]
    public async Task Execution_SetsExpectedStatus(
        bool shouldFail,
        ScanStatus expectedStatus)
    {
        var orchestrator = new FakeOrchestrator((_, _) =>
            shouldFail
                ? Task.FromException<ScanResult>(
                    new InvalidOperationException("Test error"))
                : Task.FromResult(new ScanResult()));

        await using var context = new TestContext(orchestrator);

        var job = context.Enqueue();

        await context.StartAsync();
        await WaitUntilAsync(() => ReadStatus(job) == expectedStatus);

        lock (job.SyncRoot)
        {
            if (shouldFail)
            {
                Assert.Null(job.Result);
                Assert.NotNull(job.Error);
            }
            else
            {
                Assert.NotNull(job.Result);
                Assert.Null(job.Error);
            }
        }

        await WaitUntilAsync(
            () => context.Registry.GetToken(job.Id) is null);
    }

    [Fact]
    public async Task Cancellation_StopsRunningJob()
    {
        var started = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var orchestrator = new FakeOrchestrator(async (_, token) =>
        {
            started.TrySetResult(true);

            await Task.Delay(Timeout.InfiniteTimeSpan, token);

            return new ScanResult();
        });

        await using var context = new TestContext(orchestrator);

        var job = context.Enqueue();

        await context.StartAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        lock (job.SyncRoot)
        {
            job.Status = ScanStatus.CancellationRequested;
            job.LastStatusChangedAt = DateTimeOffset.UtcNow;

            context.Registry.Cancel(job.Id);
        }

        await WaitUntilAsync(
            () => ReadStatus(job) == ScanStatus.Cancelled);

        await WaitUntilAsync(
            () => context.Registry.GetToken(job.Id) is null);

        Assert.Null(job.Result);
    }

    [Fact]
    public async Task Parallelism_DoesNotExceedConfiguredLimit()
    {
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var active = 0;
        var maximumActive = 0;
        var startedCount = 0;

        var counterLock = new object();

        var orchestrator = new FakeOrchestrator(async (_, token) =>
        {
            lock (counterLock)
            {
                active++;
                startedCount++;
                maximumActive = Math.Max(maximumActive, active);
            }

            try
            {
                await release.Task.WaitAsync(token);

                return new ScanResult();
            }
            finally
            {
                lock (counterLock)
                {
                    active--;
                }
            }
        });

        await using var context = new TestContext(
            orchestrator,
            maxParallelScans: 2);

        var jobs = Enumerable.Range(0, 3)
            .Select(_ => context.Enqueue())
            .ToArray();

        await context.StartAsync();

        await WaitUntilAsync(() =>
        {
            lock (counterLock)
            {
                return startedCount >= 2;
            }
        });

        Assert.Equal(ScanStatus.Queued, ReadStatus(jobs[2]));

        lock (counterLock)
        {
            Assert.Equal(2, active);
            Assert.Equal(2, maximumActive);
        }

        release.TrySetResult(true);

        await WaitUntilAsync(() =>
            jobs.All(job =>
                ReadStatus(job) == ScanStatus.Completed));

        lock (counterLock)
        {
            Assert.Equal(2, maximumActive);
            Assert.Equal(3, startedCount);
        }
    }

    [Fact]
    public void FullQueue_RejectsNextJob()
    {
        var options = Options.Create(new ScanningOptions
        {
            MaxParallelScans = 1,
            QueueCapacity = 2
        });

        var queue = new ScanQueue(options);

        Assert.True(queue.Enqueue(Guid.NewGuid()));
        Assert.True(queue.Enqueue(Guid.NewGuid()));
        Assert.False(queue.Enqueue(Guid.NewGuid()));
    }

    private static ScanStatus ReadStatus(ScanJob job)
    {
        lock (job.SyncRoot)
        {
            return job.Status;
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var stopwatch = Stopwatch.StartNew();

        while (!condition())
        {
            if (stopwatch.Elapsed > TimeSpan.FromSeconds(5))
            {
                throw new TimeoutException(
                    "Не дождались ожидаемого состояния.");
            }

            await Task.Delay(10);
        }
    }

    private sealed class FakeOrchestrator(
        Func<Guid, CancellationToken, Task<ScanResult>> execute)
        : IScanOrchestrator
    {
        public Task<ScanResult> ScanAsync(
            Guid scanId,
            string repositoryUrl,
            CancellationToken cancellationToken)
        {
            return execute(scanId, cancellationToken);
        }
    }

    private sealed class TestContext : IAsyncDisposable
    {
        public ScanRepository Repository { get; } = new();

        public ScanCancelRegistry Registry { get; } = new();

        private readonly ScanQueue _queue;
        private readonly ScanBackgroundService _service;

        public TestContext(
            IScanOrchestrator orchestrator,
            int maxParallelScans = 1)
        {
            var options = Options.Create(new ScanningOptions
            {
                MaxParallelScans = maxParallelScans,
                QueueCapacity = 10
            });

            _queue = new ScanQueue(options);

            _service = new ScanBackgroundService(
                _queue,
                Repository,
                Registry,
                orchestrator,
                NullLogger<ScanBackgroundService>.Instance,
                options);
        }

        public ScanJob Enqueue()
        {
            var now = DateTimeOffset.UtcNow;

            var job = new ScanJob
            {
                Id = Guid.NewGuid(),
                RepositoryUrl = "https://github.com/example/test.git",
                Status = ScanStatus.Queued,
                CreatedAt = now,
                LastStatusChangedAt = now
            };

            Repository.Add(job);
            Registry.Create(job.Id);

            Assert.True(_queue.Enqueue(job.Id));

            return job;
        }

        public Task StartAsync()
        {
            return _service.StartAsync(CancellationToken.None);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                using var timeout =
                    new CancellationTokenSource(TimeSpan.FromSeconds(5));

                await _service.StopAsync(timeout.Token);
            }
            finally
            {
                _service.Dispose();
            }
        }
    }
}