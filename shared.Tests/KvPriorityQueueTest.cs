using System;
using System.Collections.Generic;

namespace shared.Tests;

using TVal = Tuple<String, int>; // (content, score)

public class KvPriorityQueueTest {
    [Fact]
    public void TestPutPopSteps() {
        KvPriorityQueue<String, Tuple<String, int>>.ValScore scoringFunc = (x) => {
            return x.Item2;
        };
        var minHeap = new KvPriorityQueue<String, Tuple<String, int>>(1024, scoringFunc); // the minHeap should never be full in our use case

        minHeap.Put("bear#1", new TVal("Bayley", 1));
        minHeap.Put("bear#2", new TVal("Black", 10));
        minHeap.Put("bear#3", new TVal("Blue", 3));

        var t = minHeap.Pop();
        Assert.NotNull(t);
        Assert.Equal("Bayley", t.Item1);
        Assert.Equal(2, minHeap.Cnt());

        t = minHeap.Pop();
        Assert.NotNull(t);
        Assert.Equal("Blue", t.Item1);

        // Put back the two bears
        minHeap.Put("bear#1", new TVal("Bayley", 1));
        minHeap.Put("bear#3", new TVal("Blue", 3));
        // Put in more others
        minHeap.Put("rabbit#1", new TVal("Rebecca", 2));
        minHeap.Put("rabbit#2", new TVal("Racheal", 999));
        minHeap.Put("rabbit#3", new TVal("Rein", 4));

        // By now, the minHeap have these scores in order: 1, 2, 3, 4, 10, 999
        Assert.Equal(6, minHeap.Cnt());

        // Trigger a heapifyDown
        minHeap.Put("rabbit#1", new TVal("Rebecca", 5));
        // By now, the minHeap have these scores in order: 1, 3, 4, 5, 10, 999
        Assert.Equal(6, minHeap.Cnt());

        // Trigger a heapifyUp
        minHeap.Put("rabbit#3", new TVal("Rein", 1));
        // By now, the minHeap have these scores in order: 1, 1, 3, 5, 10, 999
        Assert.Equal(6, minHeap.Cnt());

        t = minHeap.Pop();
        Assert.NotNull(t);
        Assert.Equal("Bayley", t.Item1); // The top one should be the oldest with "score=1"
        Assert.Equal(5, minHeap.Cnt());

        t = minHeap.Pop();
        Assert.NotNull(t);
        Assert.Equal("Rein", t.Item1); // Then the later heapified up one with "score=1"

        // By now, the minHeap have these scores in order: 3, 5, 10, 999
        Assert.Equal(4, minHeap.Cnt());
        t = minHeap.PopAny("bear#2");
        Assert.NotNull(t);
        Assert.Equal(10, t.Item2);
        Assert.Equal("Black", t.Item1); 
        Assert.Equal(3, minHeap.Cnt());

        // By now, the minHeap have these scores in order: 3, 5, 999
        minHeap.Put("rabbit#4", new TVal("Rihana", 1));
        // By now, the minHeap have these scores in order: 1, 3, 5, 999
        minHeap.Put("rabbit#2", new TVal("Racheal", 2));
        // By now, the minHeap have these scores in order: 1, 2, 3, 5

        t = minHeap.Pop();
        Assert.NotNull(t);
        Assert.Equal("Rihana", t.Item1);
        Assert.Equal(3, minHeap.Cnt());

        // By now, the minHeap have these scores in order: 2, 3, 5
        t = minHeap.PopAny("bear#3");
        Assert.NotNull(t);
        Assert.Equal("Blue", t.Item1);
        Assert.Equal(2, minHeap.Cnt());

        // By now, the minHeap have these scores in order: 2, 5
        minHeap.Put("rabbit#1", new TVal("Rebecca", 1));

        // By now, the minHeap have these scores in order: 1, 2
        t = minHeap.Top();
        Assert.NotNull(t);
        Assert.Equal("Rebecca", t.Item1);
        Assert.Equal(2, minHeap.Cnt());

        t = minHeap.PopAny("rabbit#2");
        Assert.NotNull(t);
        Assert.Equal("Racheal", t.Item1);
        Assert.Equal(1, minHeap.Cnt());

        t = minHeap.Pop();
        Assert.NotNull(t);
        Assert.Equal("Rebecca", t.Item1);
        Assert.Equal(0, minHeap.Cnt());
    }
}

