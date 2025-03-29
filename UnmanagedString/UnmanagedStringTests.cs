using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public unsafe class UnmanagedStringTests
{
    [TestMethod]
    public void ConstructorAndToStringTest()
    {
        string test = "Hello, ����!";
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
        string test = "Hello, ����!";
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
            // ɾ������1���ַ� 'e'
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
            // ������1�������ַ� 'e'
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
            // ������4�������ַ��� "o" ��� "Hello!"
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
        // ʹ�� WithCapacity ���������߱�Ԥ����ռ�� UnmanagedString
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
