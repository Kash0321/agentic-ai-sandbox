using Microsoft.Extensions.Logging;
using Sandbox.Agents.Tests.Fakes;
using Sandbox.Application.Auditing;

namespace Sandbox.Agents.Tests.Auditing;

public sealed class AuditLogTests
{
    [Fact]
    public void ToolInvoked_AnyInvocation_WritesAuditEventWithItsFields()
    {
        var (logs, audit) = Create();

        audit.ToolInvoked("place_order", ToolDecision.Blocked, "BLOCKED_BY_POLICY", 7);

        var entry = AssertAuditEvent(logs, 5001, "ToolInvoked");
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("place_order", entry.Properties["Tool"]);
        Assert.Equal(ToolDecision.Blocked, entry.Properties["Decision"]);
        Assert.Equal("BLOCKED_BY_POLICY", entry.Properties["Outcome"]);
        Assert.Equal(7L, entry.Properties["ElapsedMs"]);
    }

    [Fact]
    public void UnknownToolRequested_AnyName_WritesAuditEventWithTheName()
    {
        var (logs, audit) = Create();

        audit.UnknownToolRequested("ship_order");

        var entry = AssertAuditEvent(logs, 5002, "UnknownToolRequested");
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("ship_order", entry.Properties["Tool"]);
    }

    [Fact]
    public void RunStopped_AnyReason_WritesAuditEventWithTheReason()
    {
        var (logs, audit) = Create();

        audit.RunStopped("TokenBudget");

        var entry = AssertAuditEvent(logs, 5003, "RunStopped");
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("TokenBudget", entry.Properties["Reason"]);
    }

    [Fact]
    public void AccessDenied_ForeignOrder_WritesAuditEventWithOperationOrderCustomerAndReason()
    {
        var (logs, audit) = Create();

        audit.AccessDenied("GetOrder", "ORD-0001", "CUST-002", AuditLog.ForeignOrderReason);

        var entry = AssertAuditEvent(logs, 5004, "AccessDenied");
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("GetOrder", entry.Properties["Operation"]);
        Assert.Equal("ORD-0001", entry.Properties["OrderId"]);
        Assert.Equal("CUST-002", entry.Properties["CustomerId"]);
        Assert.Equal("pedido de otro cliente", entry.Properties["Reason"]);
    }

    [Fact]
    public void ApprovalDecided_AnyDecision_WritesAuditEventWithToolOutcomeApproverAndReason()
    {
        var (logs, audit) = Create();

        audit.ApprovalDecided("cancel_order", ApprovalOutcome.Denied, "Console", "sin consola interactiva");

        var entry = AssertAuditEvent(logs, 5005, "ApprovalDecided");
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("cancel_order", entry.Properties["Tool"]);
        Assert.Equal(ApprovalOutcome.Denied, entry.Properties["Outcome"]);
        Assert.Equal("Console", entry.Properties["Approver"]);
        Assert.Equal("sin consola interactiva", entry.Properties["Reason"]);
        Assert.Equal(
            ["Approver", "Outcome", "Reason", "Tool", "{OriginalFormat}"],
            entry.Properties.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void TrimToolName_LongerThanLimit_KeepsTheFirst64Characters()
    {
        var name = new string('x', 200);

        var trimmed = AuditLog.TrimToolName(name);

        Assert.Equal(64, trimmed.Length);
    }

    [Fact]
    public void TrimToolName_WithinLimit_KeepsTheName()
    {
        Assert.Equal("ship_order", AuditLog.TrimToolName("ship_order"));
    }

    private static (RecordingLoggerProvider Logs, AuditLog Audit) Create()
    {
        var logs = new RecordingLoggerProvider();
        return (logs, AuditLog.Create(logs));
    }

    private static LogEntry AssertAuditEvent(RecordingLoggerProvider logs, int id, string name)
    {
        var entry = Assert.Single(logs.Entries);
        Assert.Equal("Sandbox.Audit", entry.Category);
        Assert.Equal(id, entry.EventId.Id);
        Assert.Equal(name, entry.EventId.Name);
        return entry;
    }
}
