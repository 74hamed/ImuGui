using System.Collections;

namespace ImuGui.Core.Collections;

/// <summary>
/// A fixed-capacity circular buffer: once full, adding overwrites the oldest element.
/// Index 0 is always the oldest retained element. Not thread-safe; callers serialize access.
/// This is the primitive that keeps chart history bounded.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public sealed class RingBuffer<T> : IReadOnlyList<T>
{
    private readonly T[] _items;
    private int _start;

    /// <summary>Creates a buffer with the given fixed capacity.</summary>
    /// <param name="capacity">The maximum number of retained elements; must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is not positive.</exception>
    public RingBuffer(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be positive.");
        }

        _items = new T[capacity];
    }

    /// <summary>The maximum number of retained elements.</summary>
    public int Capacity => _items.Length;

    /// <summary>The number of elements currently retained.</summary>
    public int Count { get; private set; }

    /// <summary>Gets the element at <paramref name="index"/>, where 0 is the oldest.</summary>
    /// <param name="index">The logical index.</param>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside [0, Count).</exception>
    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Index must be in [0, {Count}).");
            }

            return _items[(_start + index) % _items.Length];
        }
    }

    /// <summary>Appends an element, overwriting the oldest one when full.</summary>
    /// <param name="item">The element to append.</param>
    public void Add(T item)
    {
        if (Count < _items.Length)
        {
            _items[(_start + Count) % _items.Length] = item;
            Count++;
        }
        else
        {
            _items[_start] = item;
            _start = (_start + 1) % _items.Length;
        }
    }

    /// <summary>Removes all elements.</summary>
    public void Clear()
    {
        Array.Clear(_items);
        _start = 0;
        Count = 0;
    }

    /// <summary>Copies the retained elements, oldest first, into a new array.</summary>
    public T[] ToArray()
    {
        var result = new T[Count];
        for (int i = 0; i < Count; i++)
        {
            result[i] = this[i];
        }

        return result;
    }

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
        {
            yield return this[i];
        }
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
