using AutoSeller.Core.AutoRetainer.Model;

namespace AutoSeller.Tests.Run;

public sealed class PostprocessHandshakeTests
{
    private const string Retainer = "Alpha";
    private const string OtherRetainer = "Beta";
    private const ulong RetainerId = 0x0030_0000_0000_0001;
    private const int PlannedWrites = 4;
    private const long StartMs = 10_000;
    private const long BudgetMs = 180_000;

    private static readonly Guid job = new("5a1e7c2b-3d4f-4e6a-8b9c-0d1e2f3a4b5c");

    [Fact]
    public void ARequestedRetainerStartsAJobWhenItIsHandedOver()
    {
        var handshake = Requested();

        Assert.Equal(ReadyStep.Start, handshake.Ready(Retainer, StartMs));
        handshake.JobStarted(job);

        Assert.Equal(HandshakePhase.Working, handshake.Phase);
        Assert.True(handshake.OwesFinish);
        Assert.Equal(job, handshake.JobId);
        Assert.Equal(TimeSpan.FromMilliseconds(BudgetMs), handshake.Allowance);
    }

    [Fact]
    public void ARetainerHandedOverWithoutARequestIsHandedStraightBack()
    {
        var handshake = new PostprocessHandshake();

        Assert.Equal(ReadyStep.HandBack, handshake.Ready(Retainer, StartMs));
        Assert.Equal(HandshakePhase.Idle, handshake.Phase);
        Assert.False(handshake.OwesFinish);
    }

    [Fact]
    public void AnotherRetainerThanTheOneAskedForIsHandedStraightBack()
    {
        var handshake = Requested();

        Assert.Equal(ReadyStep.HandBack, handshake.Ready(OtherRetainer, StartMs));
        Assert.Equal(HandshakePhase.Idle, handshake.Phase);
    }

    [Fact]
    public void NoNewRequestIsMadeWhileARetainerIsOwed()
    {
        var handshake = Working();

        Assert.False(handshake.Request(OtherRetainer, RetainerId + 1, 1));
        Assert.Equal(Retainer, handshake.RetainerName);
    }

    [Fact]
    public void ARetainerWithoutAnIdIsNeverRequested()
    {
        Assert.False(new PostprocessHandshake().Request(Retainer, 0, PlannedWrites));
    }

    [Fact]
    public void ARefusedRequestLeavesNothingPending()
    {
        var handshake = Requested();

        handshake.RequestRefused();

        Assert.Equal(HandshakePhase.Idle, handshake.Phase);
        Assert.Equal(ReadyStep.HandBack, handshake.Ready(Retainer, StartMs));
    }

    [Fact]
    public void ARunningJobWithinItsBudgetIsLeftAlone()
    {
        var handshake = Working();

        Assert.Equal(HandshakeStep.Wait, handshake.Tick(Facts(StartMs + BudgetMs - 1)));
        Assert.Equal(HandshakePhase.Working, handshake.Phase);
    }

    [Fact]
    public void AFinishedJobHandsTheRetainerBackOnceTheMenuIsBack()
    {
        var handshake = Working();

        Assert.Equal(HandshakeStep.Wait, handshake.Tick(Facts(StartMs + 1_000, jobRunning: false, settling: true, atMenu: false)));
        Assert.Equal(HandshakePhase.Returning, handshake.Phase);
        Assert.Equal(HandshakeStep.Wait, handshake.Tick(Facts(StartMs + 2_000, jobRunning: false, settling: false, atMenu: false)));
        Assert.Equal(HandshakeStep.Wait, handshake.Tick(Facts(StartMs + 3_000, jobRunning: false, settling: true, atMenu: true)));
        Assert.Equal(HandshakeStep.Finish, handshake.Tick(Facts(StartMs + 4_000, jobRunning: false, settling: false, atMenu: true)));
    }

    [Fact]
    public void AJobThatEndsAtTheMenuIsHandedBackAtOnce()
    {
        var handshake = Working();

        Assert.Equal(HandshakeStep.Finish, handshake.Tick(Facts(StartMs + 1_000, jobRunning: false)));
    }

    [Fact]
    public void AMenuThatNeverComesBackIsHandedBackAfterTheReturnBound()
    {
        var handshake = Working();
        handshake.Tick(Facts(StartMs + 1_000, jobRunning: false, atMenu: false));

        Assert.Equal(HandshakeStep.Wait, handshake.Tick(Facts(StartMs + 1_000 + PostprocessHandshake.ReturnBoundMs - 1, jobRunning: false, settling: true, atMenu: false)));
        Assert.Equal(HandshakeStep.Finish, handshake.Tick(Facts(StartMs + 1_000 + PostprocessHandshake.ReturnBoundMs, jobRunning: false, settling: true, atMenu: false)));
    }

    [Fact]
    public void ARunOutBudgetStopsTheJobOnceThenEndsItAfterTheGrace()
    {
        var handshake = Working();
        var unwindAt = StartMs + BudgetMs;

        Assert.Equal(HandshakeStep.StopJob, handshake.Tick(Facts(unwindAt)));
        Assert.Equal(HandshakePhase.Unwinding, handshake.Phase);
        Assert.Equal(HandshakeStep.Wait, handshake.Tick(Facts(unwindAt + PostprocessHandshake.UnwindGraceMs - 1)));
        Assert.Equal(HandshakeStep.EndJob, handshake.Tick(Facts(unwindAt + PostprocessHandshake.UnwindGraceMs)));
        Assert.Equal(HandshakeStep.Wait, handshake.Tick(Facts(unwindAt + PostprocessHandshake.UnwindGraceMs + 1)));
        Assert.Equal(HandshakeStep.Finish, handshake.Tick(Facts(unwindAt + PostprocessHandshake.UnwindGraceMs + 2, jobRunning: false)));
    }

    [Fact]
    public void AnUnwoundJobThatStopsInTimeGoesBackToTheMenu()
    {
        var handshake = Working();
        handshake.Tick(Facts(StartMs + BudgetMs));

        Assert.Equal(HandshakeStep.Wait, handshake.Tick(Facts(StartMs + BudgetMs + 5_000, jobRunning: false, settling: true)));
        Assert.Equal(HandshakePhase.Returning, handshake.Phase);
        Assert.Equal(HandshakeStep.Finish, handshake.Tick(Facts(StartMs + BudgetMs + 9_000, jobRunning: false)));
    }

    [Fact]
    public void AnotherCharacterAbandonsTheJobAndHandsBackWithoutWaiting()
    {
        var handshake = Working();

        Assert.Equal(HandshakeStep.Abandon, handshake.Tick(Facts(StartMs + 1_000) with { SameCharacter = false }));
    }

    [Fact]
    public void TheRetainerIsHandedBackExactlyOnce()
    {
        var handshake = Working();

        Assert.True(handshake.Finish());
        Assert.False(handshake.Finish());
        Assert.Equal(HandshakePhase.Idle, handshake.Phase);
        Assert.Equal(Guid.Empty, handshake.JobId);
        Assert.Equal(HandshakeStep.Wait, handshake.Tick(Facts(StartMs + 1_000, jobRunning: false)));
    }

    [Fact]
    public void APendingRequestOwesNothing()
    {
        Assert.False(Requested().Finish());
    }

    [Fact]
    public void TheSellNowButtonAndAStopLastUntilTheBellIsLeft()
    {
        var handshake = Requested();
        handshake.ArmSellNow(StartMs);
        Assert.True(handshake.SellNowArmed);

        handshake.StandDown();
        Assert.True(handshake.StoodDown);
        Assert.False(handshake.SellNowArmed);

        handshake.LeftBell();
        Assert.False(handshake.StoodDown);
        Assert.False(handshake.SellNowArmed);
        Assert.Equal(HandshakePhase.Idle, handshake.Phase);
    }

    [Fact]
    public void PressingSellNowAgainLiftsAStop()
    {
        var handshake = new PostprocessHandshake();
        handshake.StandDown();

        handshake.ArmSellNow(StartMs);

        Assert.False(handshake.StoodDown);
        Assert.True(handshake.SellNowArmed);
    }

    [Fact]
    public void SellNowEndsOnceAutoRetainerIsIdleAfterItsWalk()
    {
        var handshake = new PostprocessHandshake();
        handshake.ArmSellNow(StartMs);

        Assert.False(handshake.SellNowWalkEnded(false, StartMs + 500));
        Assert.False(handshake.SellNowWalkEnded(true, StartMs + 1_000));
        Assert.False(handshake.SellNowWalkEnded(true, StartMs + 60_000));
        Assert.True(handshake.SellNowArmed);

        Assert.True(handshake.SellNowWalkEnded(false, StartMs + 61_000));
        Assert.False(handshake.SellNowArmed);
        Assert.False(handshake.SellNowWalkEnded(false, StartMs + 62_000));
    }

    [Fact]
    public void SellNowThatAutoRetainerNeverTakesUpEndsAfterTheTakeUpBound()
    {
        var handshake = new PostprocessHandshake();
        handshake.ArmSellNow(StartMs);

        Assert.False(handshake.SellNowWalkEnded(false, StartMs + PostprocessHandshake.SellNowTakeUpMs - 1));
        Assert.True(handshake.SellNowWalkEnded(false, StartMs + PostprocessHandshake.SellNowTakeUpMs));
        Assert.False(handshake.SellNowArmed);
    }

    [Fact]
    public void PressingSellNowAgainStartsANewWalk()
    {
        var handshake = new PostprocessHandshake();
        handshake.ArmSellNow(StartMs);
        handshake.SellNowWalkEnded(true, StartMs + 1_000);

        handshake.ArmSellNow(StartMs + 2_000);

        Assert.False(handshake.SellNowWalkEnded(false, StartMs + 2_500));
        Assert.True(handshake.SellNowArmed);
    }

    [Fact]
    public void NothingEndsWhenSellNowWasNeverPressed()
    {
        Assert.False(new PostprocessHandshake().SellNowWalkEnded(false, StartMs + PostprocessHandshake.SellNowTakeUpMs));
    }

    [Fact]
    public void LeavingTheBellKeepsAJobThatIsStillOwed()
    {
        var handshake = Working();

        handshake.LeftBell();

        Assert.Equal(HandshakePhase.Working, handshake.Phase);
        Assert.True(handshake.OwesFinish);
    }

    private static PostprocessHandshake Requested()
    {
        var handshake = new PostprocessHandshake();
        Assert.True(handshake.Request(Retainer, RetainerId, PlannedWrites));
        Assert.Equal(HandshakePhase.Requested, handshake.Phase);
        return handshake;
    }

    private static PostprocessHandshake Working()
    {
        var handshake = Requested();
        handshake.Ready(Retainer, StartMs);
        handshake.JobStarted(job);
        return handshake;
    }

    private static HandshakeFacts Facts(long nowMs, bool jobRunning = true, bool settling = false, bool atMenu = true)
    {
        return new HandshakeFacts(true, jobRunning, settling, atMenu, nowMs);
    }
}
