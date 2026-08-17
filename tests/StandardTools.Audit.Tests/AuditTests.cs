using System.Text.Json;
using StandardTools.Audit;
using StandardTools.Core;
using Xunit;

namespace StandardTools.Audit.Tests;

public class AuditTests
{
    private static DecisionRecord SampleRecord(string id) => new()
    {
        RequestID = id,
        RecordedAt = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc),
        ToolName = "list_tools",
        Input = new Dictionary<string, object> { ["ticker"] = "AAPL" },
        InputHash = string.Empty,
        Output = new Dictionary<string, object> { ["tools"] = new[] { "list_tools" } },
        OutputHash = string.Empty,
        Status = "ok",
        GitCommitSHA = "abc123",
        PackageVersion = "0.1.0",
        RandomSeed = 42,
        RecordHash = string.Empty
    };

    [Fact]
    public void HashRecord_IsStableAndIgnoresExistingRecordHash()
    {
        var r = SampleRecord("r1");
        var h1 = AuditWriter.HashRecord(r);
        var h2 = AuditWriter.HashRecord(r);
        Assert.Equal(h1, h2);

        var tampered = r with { RecordHash = "tampered" };
        var h3 = AuditWriter.HashRecord(tampered);
        Assert.Equal(h1, h3);
    }

    [Fact]
    public async Task Writer_ChainsRecords()
    {
        var storage = new InMemoryAuditStorage();
        var writer = new AuditWriter(storage);

        await writer.WriteAsync(SampleRecord("r1"));
        var stored1 = await storage.LatestAsync();
        Assert.NotEmpty(stored1.RecordHash);
        Assert.Empty(stored1.PrevRecordHash);

        await writer.WriteAsync(SampleRecord("r2"));
        var stored2 = await storage.LatestAsync();
        Assert.Equal(stored1.RecordHash, stored2.PrevRecordHash);
        Assert.NotEqual(stored1.RecordHash, stored2.RecordHash);
    }

    [Fact]
    public async Task Writer_NormalizesRecordedAtToUtc()
    {
        var storage = new InMemoryAuditStorage();
        var writer = new AuditWriter(storage);

        var r = SampleRecord("r1") with { RecordedAt = new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Local) };
        await writer.WriteAsync(r);

        var stored = await storage.LatestAsync();
        Assert.Equal(DateTimeKind.Utc, stored.RecordedAt.Kind);
    }

    [Fact]
    public async Task Writer_ComputesInputOutputHashes()
    {
        var storage = new InMemoryAuditStorage();
        var writer = new AuditWriter(storage);

        await writer.WriteAsync(SampleRecord("r1"));
        var stored = await storage.LatestAsync();

        Assert.NotEmpty(stored.InputHash);
        Assert.NotEmpty(stored.OutputHash);
    }

    [Fact]
    public async Task Verifier_ValidChain_Passes()
    {
        var storage = new InMemoryAuditStorage();
        var writer = new AuditWriter(storage);
        var verifier = new AuditVerifier(storage);

        await writer.WriteAsync(SampleRecord("r1"));
        await writer.WriteAsync(SampleRecord("r2"));

        await verifier.VerifyChainAsync();
    }

    [Fact]
    public async Task Verifier_EmptyChain_Passes()
    {
        var storage = new InMemoryAuditStorage();
        var verifier = new AuditVerifier(storage);

        await verifier.VerifyChainAsync();
    }

    [Fact]
    public async Task Verifier_TamperedRecord_Throws()
    {
        var storage = new InMemoryAuditStorage();
        var writer = new AuditWriter(storage);
        var verifier = new AuditVerifier(storage);

        await writer.WriteAsync(SampleRecord("r1"));
        var latest = await storage.LatestAsync();
        var tampered = latest with { Status = "malicious" };
        await storage.AppendAsync(tampered);

        await Assert.ThrowsAsync<AuditIntegrityException>(() => verifier.VerifyChainAsync());
    }

    [Fact]
    public async Task Verifier_TamperedInputHash_Throws()
    {
        var storage = new InMemoryAuditStorage();
        var writer = new AuditWriter(storage);
        var verifier = new AuditVerifier(storage);

        await writer.WriteAsync(SampleRecord("r1"));
        var latest = await storage.LatestAsync();
        var tampered = latest with { InputHash = "tampered" };
        await storage.AppendAsync(tampered);

        await Assert.ThrowsAsync<AuditIntegrityException>(() => verifier.VerifyChainAsync());
    }

    [Fact]
    public async Task Verifier_BrokenChainLink_Throws()
    {
        var storage = new InMemoryAuditStorage();
        var writer = new AuditWriter(storage);
        var verifier = new AuditVerifier(storage);

        await writer.WriteAsync(SampleRecord("r1"));
        await writer.WriteAsync(SampleRecord("r2"));

        var all = await storage.AllAsync();
        var broken = all[1] with { PrevRecordHash = "tampered" };
        await storage.AppendAsync(broken);

        await Assert.ThrowsAsync<AuditIntegrityException>(() => verifier.VerifyChainAsync());
    }

    [Fact]
    public async Task Storage_GetByRequestID_ReturnsRecord()
    {
        var storage = new InMemoryAuditStorage();
        await storage.AppendAsync(SampleRecord("r1") with { RecordHash = "h1" });
        await storage.AppendAsync(SampleRecord("r2") with { RecordHash = "h2" });

        var found = await storage.GetByRequestIDAsync("r1");
        Assert.Equal("r1", found.RequestID);

        await Assert.ThrowsAsync<AuditNotFoundException>(() => storage.GetByRequestIDAsync("unknown"));
    }

    [Fact]
    public async Task Storage_LatestNotFound_Throws()
    {
        var storage = new InMemoryAuditStorage();
        await Assert.ThrowsAsync<AuditNotFoundException>(() => storage.LatestAsync());
    }

    [Fact]
    public async Task Replay_LoadsRecord()
    {
        var storage = new InMemoryAuditStorage();
        await storage.AppendAsync(SampleRecord("r1") with { RecordHash = "h1" });

        var replay = new AuditReplay(storage);
        var loaded = await replay.LoadAsync("r1");
        Assert.Equal("r1", loaded.RequestID);

        await Assert.ThrowsAsync<InvalidCommandException>(() => replay.LoadAsync(""));
    }

    [Fact]
    public async Task Writer_ConcurrentWrites_MaintainsChainIntegrity()
    {
        var inner = new InMemoryAuditStorage();
        var storage = new YieldingAuditStorage(inner);
        var writer = new AuditWriter(storage);
        var verifier = new AuditVerifier(inner);

        const int n = 100;
        var tasks = Enumerable.Range(0, n)
            .Select(i => writer.WriteAsync(SampleRecord($"r{i}")))
            .ToArray();
        await Task.WhenAll(tasks);

        await verifier.VerifyChainAsync();

        var records = new List<DecisionRecord>();
        for (var i = 0; i < n; i++)
        {
            records.Add(await inner.GetByRequestIDAsync($"r{i}"));
        }

        var prevCounts = records
            .Where(r => !string.IsNullOrEmpty(r.PrevRecordHash))
            .GroupBy(r => r.PrevRecordHash)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.All(prevCounts.Values, count => Assert.Equal(1, count));
    }

    /// <summary>
    /// Storage decorator that yields between operations to expose read-then-write races.
    /// </summary>
    private sealed class YieldingAuditStorage : IAuditStorage
    {
        private readonly IAuditStorage _inner;

        public YieldingAuditStorage(IAuditStorage inner) => _inner = inner;

        public async Task<DecisionRecord> LatestAsync(CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            return await _inner.LatestAsync(cancellationToken);
        }

        public async Task AppendAsync(DecisionRecord record, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            await _inner.AppendAsync(record, cancellationToken);
        }

        public async Task<IReadOnlyList<DecisionRecord>> AllAsync(CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            return await _inner.AllAsync(cancellationToken);
        }

        public async Task<DecisionRecord> GetByRequestIDAsync(string requestID, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            return await _inner.GetByRequestIDAsync(requestID, cancellationToken);
        }
    }

    [Fact]
    public async Task Writer_CanonicalizesInputToJsonElement()
    {
        var storage = new InMemoryAuditStorage();
        var writer = new AuditWriter(storage);

        await writer.WriteAsync(SampleRecord("r1"));
        var stored = await storage.LatestAsync();

        Assert.IsType<JsonElement>(stored.Input);
        Assert.IsType<JsonElement>(stored.Output);
    }
}
