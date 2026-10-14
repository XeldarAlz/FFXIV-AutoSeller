namespace AutoSeller.Core.Tasks;

internal enum RunPhase : byte { Idle, Preparing, Paused, Travelling, Selling, Finishing, Stopping }
