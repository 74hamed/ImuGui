using FluentAssertions;
using ImuGui.Core.Collections;
using Xunit;

namespace ImuGui.Core.Tests.Collections;

public class RingBufferTests
{
    [Fact]
    public void Fills_up_to_capacity_then_overwrites_oldest()
    {
        var buffer = new RingBuffer<int>(3);

        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);
        buffer.Count.Should().Be(3);
        buffer.ToArray().Should().Equal(1, 2, 3);

        buffer.Add(4);
        buffer.Count.Should().Be(3, "capacity is a hard bound — no unbounded growth");
        buffer.ToArray().Should().Equal(2, 3, 4);

        buffer.Add(5);
        buffer.Add(6);
        buffer.Add(7);
        buffer.ToArray().Should().Equal(5, 6, 7);
    }

    [Fact]
    public void Indexer_is_oldest_first_and_bounds_checked()
    {
        var buffer = new RingBuffer<string>(2);
        buffer.Add("a");
        buffer.Add("b");
        buffer.Add("c");

        buffer[0].Should().Be("b");
        buffer[1].Should().Be("c");

        Action act = () => _ = buffer[2];
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Clear_empties_the_buffer_for_reuse()
    {
        var buffer = new RingBuffer<int>(2);
        buffer.Add(1);
        buffer.Add(2);

        buffer.Clear();

        buffer.Count.Should().Be(0);
        buffer.ToArray().Should().BeEmpty();
        buffer.Add(9);
        buffer.ToArray().Should().Equal(9);
    }

    [Fact]
    public void Enumerator_yields_in_logical_order_after_wrapping()
    {
        var buffer = new RingBuffer<int>(4);
        for (int i = 1; i <= 6; i++)
        {
            buffer.Add(i);
        }

        buffer.Should().Equal(3, 4, 5, 6);
    }

    [Fact]
    public void Non_positive_capacity_is_rejected()
    {
        Action act = () => _ = new RingBuffer<int>(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
