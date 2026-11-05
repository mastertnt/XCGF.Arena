namespace XCGF.Arena.Data;

/// <summary>
/// Raised when a library is malformed or inconsistent, or when a value breaks a rule of the library.
/// </summary>
public sealed class XcgfDataException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="XcgfDataException"/> class.
    /// </summary>
    /// <param name="pMessage">What is wrong.</param>
    public XcgfDataException(string pMessage)
        : base(pMessage)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XcgfDataException"/> class.
    /// </summary>
    /// <param name="pMessage">What is wrong.</param>
    /// <param name="pInnerException">The error that revealed it.</param>
    public XcgfDataException(string pMessage, Exception pInnerException)
        : base(pMessage, pInnerException)
    {
    }
}
