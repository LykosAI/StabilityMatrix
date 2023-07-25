namespace StabilityMatrix.Core.Processes;

[Flags]
public enum AnsiCommand
{
    /// <summary>
    /// Default value
    /// </summary>
    None = 0,
    
    // Erase commands
    
    /// <summary>
    /// Erase from cursor to end of line
    /// ESC[K or ESC[0K
    /// </summary>
    EraseToEndOfLine = 1 << 0,
    
    /// <summary>
    /// Erase from start of line to cursor
    /// ESC[1K
    /// </summary>
    EraseFromStartOfLine = 1 << 1,
   
    /// <summary>
    /// Erase entire line
    /// ESC[2K
    /// </summary>
    EraseLine = 1 << 2,
}
