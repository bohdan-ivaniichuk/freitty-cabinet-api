namespace Freitty.Application.Dashboard;

// An order that is invoiced while an Alert is open needs two things and is counted in both.
public sealed record AttentionCounts(int AwaitingAction, int Alerts)
{
    public int Total => AwaitingAction + Alerts;
}
