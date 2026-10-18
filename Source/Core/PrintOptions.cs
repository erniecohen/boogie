namespace Microsoft.Boogie;

public interface PrintOptions {
  public static readonly PrintOptions Default = new PrintOptionsRec(
    false,
    false,
    false,
    0,
    false);

  bool PrintWithUniqueASTIds { get; }
  bool PrintInstrumented { get; }
  bool PrintInlined { get; }
  int StratifiedInlining { get; }
  bool PrintDesugarings { get; set; }
  string PrintPassiveFile { get; set; }
  int PrintUnstructured { get; set; }
  bool ReflectAdd { get; }

  /// <summary>
  /// Print each axiom once, at its own position among the top-level declarations, and name the
  /// functions and constants that own it as a definition axiom in attributes that resolution
  /// reattaches, instead of printing it in the uses clause of each owner.
  /// </summary>
  bool PrintDefinitionAxiomsInPlace { get; }
}

record PrintOptionsRec(bool PrintWithUniqueASTIds, bool PrintInstrumented, bool PrintInlined, int StratifiedInlining, bool ReflectAdd) : PrintOptions {
  public bool PrintDesugarings { get; set; }
  public string PrintPassiveFile { get; set; }
  public int PrintUnstructured { get; set; }
  public bool PrintDefinitionAxiomsInPlace { get; set; }
}