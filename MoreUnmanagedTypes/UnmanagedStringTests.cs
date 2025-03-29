using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public unsafe class UnmanagedStringTests
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
        }
        finally
        {
            us.Free();
        }
    }
}
