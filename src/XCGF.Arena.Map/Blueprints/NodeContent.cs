using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace XCGF.Arena.Map.Blueprints;

/// <summary>
/// Base of the view models shown by the node template.
/// </summary>
public abstract class Observable : INotifyPropertyChanged
{
    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Sets a field and raises <see cref="PropertyChanged"/> when it changes.
    /// </summary>
    /// <typeparam name="T">Type of the field.</typeparam>
    /// <param name="pField">The field.</param>
    /// <param name="pValue">The new value.</param>
    /// <param name="pName">The name of the property.</param>
    /// <returns>True when the value changed.</returns>
    protected bool Set<T>(ref T pField, T pValue, [CallerMemberName] string? pName = null)
    {
        if (EqualityComparer<T>.Default.Equals(pField, pValue))
        {
            return false;
        }

        pField = pValue;
        Raise(pName!);
        return true;
    }

    /// <summary>
    /// Raises <see cref="PropertyChanged"/>.
    /// </summary>
    /// <param name="pName">The name of the property.</param>
    protected void Raise(string pName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(pName));
}

/// <summary>
/// One row of pins of a node: the label of the left pin, and of the right one.
/// </summary>
public sealed class PinRowContent : Observable
{
    private string mLeft = string.Empty;
    private string mRight = string.Empty;

    /// <summary>
    /// Gets or sets the label of the input pin.
    /// </summary>
    public string Left
    {
        get => mLeft;
        set => Set(ref mLeft, value);
    }

    /// <summary>
    /// Gets or sets the label of the output pin.
    /// </summary>
    public string Right
    {
        get => mRight;
        set => Set(ref mRight, value);
    }
}

/// <summary>
/// Content of a node: coloured header, rows of pins, badge of the fibers waiting on it, halo of its last execution.
/// </summary>
public sealed class NodeContent : Observable
{
    private static readonly IBrush sIdle = new SolidColorBrush(Color.Parse("#45464E"));
    private static readonly IBrush sWaiting = new SolidColorBrush(Color.Parse("#F2C14E"));

    private string? mBadge;
    private double mFlash;

    /// <summary>
    /// Initializes a new instance of the <see cref="NodeContent"/> class.
    /// </summary>
    /// <param name="pTitle">The title.</param>
    /// <param name="pHeader">The colour of the header.</param>
    /// <param name="pRows">The number of pin rows.</param>
    public NodeContent(string pTitle, Color pHeader, int pRows)
    {
        Title = pTitle;
        HeaderBrush = new SolidColorBrush(pHeader);
        Rows = Enumerable.Range(0, pRows).Select(pIndex => new PinRowContent()).ToList();
    }

    /// <summary>
    /// Gets the title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the brush of the header.
    /// </summary>
    public IBrush HeaderBrush { get; }

    /// <summary>
    /// Gets the rows of pins.
    /// </summary>
    public IReadOnlyList<PinRowContent> Rows { get; }

    /// <summary>
    /// Gets or sets the text of the badge when fibers wait on the node, else null.
    /// </summary>
    public string? Badge
    {
        get => mBadge;
        set
        {
            if (Set(ref mBadge, value))
            {
                Raise(nameof(IsWaiting));
                Raise(nameof(BorderBrush));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether fibers wait on the node.
    /// </summary>
    public bool IsWaiting => mBadge is not null;

    /// <summary>
    /// Gets the brush of the border: highlighted while fibers wait on the node.
    /// </summary>
    public IBrush BorderBrush => IsWaiting ? sWaiting : sIdle;

    /// <summary>
    /// Gets or sets the opacity of the execution halo, from 1 (just run) to 0.
    /// </summary>
    public double Flash
    {
        get => mFlash;
        set => Set(ref mFlash, Math.Round(value, 2));
    }
}
