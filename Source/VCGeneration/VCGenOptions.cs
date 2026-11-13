namespace Microsoft.Boogie;

public interface VCGenOptions : SMTLibOptions
{
  // Separate from proof limits: a nonresponsive idle handshake must not hold a pool slot forever.
  uint SolverIdleTimeout => 300;
  int KInductionDepth { get; }
  bool AlwaysAssumeFreeLoopInvariants { get; }
  int LiveVariableAnalysis { get; }
  bool RemoveEmptyBlocks { get; }
}