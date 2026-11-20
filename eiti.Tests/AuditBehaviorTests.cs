using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Behaviors;
using eiti.Domain.Companies;
using FluentAssertions;
using MediatR;
using Moq;

namespace eiti.Tests;

/// <summary>
/// Un pedido que el navegador abandona llega con el token cancelado. Su auditoría se escribía con ese token y
/// se perdía: la venta MAT-4194 (2026-11-19) quedó guardada sin ningún registro de quién la creó.
/// </summary>
public sealed class AuditBehaviorTests
{
    public sealed record PingCommand : IRequest<Result>;

    private readonly Mock<IAuditLogWriter> _writer = new();
    private readonly List<CancellationToken> _writeTokens = [];
    private readonly List<AuditLogEntry> _entries = [];

    public AuditBehaviorTests()
    {
        _writer.Setup(x => x.WriteAsync(It.IsAny<AuditLogEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLogEntry, CancellationToken>((entry, token) =>
            {
                _entries.Add(entry);
                _writeTokens.Add(token);
            })
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task A_command_from_an_abandoned_request_is_still_audited()
    {
        var result = await Behavior().Handle(new PingCommand(), () => Task.FromResult(Result.Success()), CancelledToken());

        result.IsSuccess.Should().BeTrue();
        _entries.Should().ContainSingle().Which.Succeeded.Should().BeTrue();
        _writeTokens.Single().IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public async Task A_command_cut_by_the_abandoned_request_is_audited_as_failed()
    {
        var handle = () => Behavior().Handle(
            new PingCommand(),
            () => Task.FromException<Result>(new OperationCanceledException()),
            CancelledToken());

        await handle.Should().ThrowAsync<OperationCanceledException>();
        _entries.Should().ContainSingle().Which.ErrorCode.Should().Be("Exception.OperationCanceledException");
        _writeTokens.Single().IsCancellationRequested.Should().BeFalse();
    }

    private static CancellationToken CancelledToken()
    {
        var source = new CancellationTokenSource();
        source.Cancel();
        return source.Token;
    }

    private AuditBehavior<PingCommand, Result> Behavior()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.CompanyId).Returns(CompanyId.New());
        return new AuditBehavior<PingCommand, Result>(currentUser.Object, _writer.Object, new Mock<IAuditSnapshotService>().Object);
    }
}
