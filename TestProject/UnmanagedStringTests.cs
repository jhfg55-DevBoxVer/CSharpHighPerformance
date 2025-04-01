using MoreUnmanagedTypes;

[TestClass]
public class UnmanagedStringTests
{
    [TestMethod]
    public void ConstructorAndToStringTest()
    {
        string test = "Hello, 世界!";
        UnmanagedString us = new UnmanagedString(test);
        try
        {
            Assert.AreEqual(test, us.ToString());
        }
        finally
        {
            us.Free();
        }
    }

    [TestMethod]
    public void CharEnumerableTest()
    {
        string test = "abc";
        UnmanagedString us = new UnmanagedString(test);
        try
        {
            int count = 0;
            foreach (int cp in us.CharValues)
            {
                count++;
            }
            // 对于纯 ASCII 字符，CharCount 和字符串长度相同
            Assert.AreEqual(test.Length, count);
        }
        finally
        {
            us.Free();
        }
    }

    [TestMethod]
    public void CharCountTest()
    {
        string test = "Hello, 世界!";
        UnmanagedString us = new UnmanagedString(test);
        try
        {
            int count = us.CharCount();
            // 对于此测试字符串，每个字符占1个 UTF-8 字节（假设没有代理对），因此字符数与 Length 相等
            Assert.AreEqual(test.Length, count);
        }
        finally
        {
            us.Free();
        }
    }

    [TestMethod]
    public void RemoveAtTest()
    {
        string test = "Hello";
        UnmanagedString us = new UnmanagedString(test);
        try
        {
            // 删除索引1处字符 'e'
            int removed = us.RemoveAt(1);
            Assert.AreEqual('e', removed);
            Assert.AreEqual("Hllo", us.ToString());
        }
        finally
        {
            us.Free();
        }
    }

    [TestMethod]
    public void InsertAtCharTest()
    {
        string test = "Hllo";
        UnmanagedString us = new UnmanagedString(test);
        try
        {
            // 在索引1处插入字符 'e'
            us.InsertAt(1, 'e');
            Assert.AreEqual("Hello", us.ToString());
        }
        finally
        {
            us.Free();
        }
    }

    [TestMethod]
    public void InsertAtStringTest()
    {
        string test = "Hell!";
        UnmanagedString us = new UnmanagedString(test);
        try
        {
            // 在索引4处插入字符串 "o" 完成 "Hello!"
            us.InsertAt(4, "o");
            Assert.AreEqual("Hello!", us.ToString());
        }
        finally
        {
            us.Free();
        }
    }

    [TestMethod]
    public void WithCapacityTest()
    {
        // 使用 WithCapacity 方法创建具备预分配空间的 UnmanagedString
        UnmanagedString us = UnmanagedString.WithCapacity(100);
        try
        {
            us.InsertAt(0, "Test");
            Assert.AreEqual("Test", us.ToString());
            // 容量应不小于 100
            Assert.IsTrue(us.Capacity >= 100);
        }
        finally
        {
            us.Free();
        }
    }

    [TestMethod]
    public void ReserveAndShrinkToFitTest()
    {
        string test = "Data";
        UnmanagedString us = new UnmanagedString(test);
        try
        {
            // 初始容量即为字节数，可能较接近于 Length
            int initialCapacity = us.Capacity;
            // Reserve 额外空间
            us.Reserve(50);
            Assert.IsTrue(us.Capacity >= us.Length + 50);
            // 调用 ShrinkToFit 之后，容量应等于当前 Length
            us.ShrinkToFit();
            Assert.AreEqual(us.Length, us.Capacity);
        }
        finally
        {
            us.Free();
        }
    }

    [TestMethod]
    public void PushAndPushStrTest()
    {
        UnmanagedString us = UnmanagedString.New();
        try
        {
            // 使用 Push 和 PushStr 追加字符和字符串
            us.Push('a');
            us.PushStr("bc");
            Assert.AreEqual("abc", us.ToString());
        }
        finally
        {
            us.Free();
        }
    }
}
