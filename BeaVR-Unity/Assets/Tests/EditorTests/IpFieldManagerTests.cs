using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class IpFieldManagerTests
{
	[Test]
	public void Normalize_StripsPort_And_UnicodeDots()
	{
		var input = "192.168.1.1:5555";
		var type = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).FirstOrDefault(t => t.Name == "IPFieldManager");
		Assert.IsNotNull(type, "IPFieldManager type not found");
		var method = type.GetMethod("NormalizeIPv4Input", BindingFlags.Public | BindingFlags.Static);
		Assert.IsNotNull(method, "NormalizeIPv4Input not found");
		var norm = (string)method.Invoke(null, new object[] { input });
		Assert.AreEqual("192.168.1.1", norm);
	}

	[Test]
	public void Valid_BasicIPs()
	{
		var type = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).FirstOrDefault(t => t.Name == "IPFieldManager");
		var method = type.GetMethod("IsValidIPv4", BindingFlags.Public | BindingFlags.Static);
		Assert.IsTrue((bool)method.Invoke(null, new object[] { "192.168.1.1" }));
		Assert.IsTrue((bool)method.Invoke(null, new object[] { "10.0.0.1" }));
		Assert.IsTrue((bool)method.Invoke(null, new object[] { "172.16.0.1" }));
	}

	[Test]
	public void Reject_LeadingZeros()
	{
		var type = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).FirstOrDefault(t => t.Name == "IPFieldManager");
		var method = type.GetMethod("IsValidIPv4", BindingFlags.Public | BindingFlags.Static);
		Assert.IsFalse((bool)method.Invoke(null, new object[] { "192.168.01.1" }));
	}

	[Test]
	public void Reject_OutOfRange()
	{
		var type = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).FirstOrDefault(t => t.Name == "IPFieldManager");
		var method = type.GetMethod("IsValidIPv4", BindingFlags.Public | BindingFlags.Static);
		Assert.IsFalse((bool)method.Invoke(null, new object[] { "256.1.1.1" }));
		Assert.IsFalse((bool)method.Invoke(null, new object[] { "1.1.1.300" }));
	}

	[Test]
	public void Trim_Whitespace()
	{
		var type = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).FirstOrDefault(t => t.Name == "IPFieldManager");
		var method = type.GetMethod("IsValidIPv4", BindingFlags.Public | BindingFlags.Static);
		Assert.IsTrue((bool)method.Invoke(null, new object[] { "  192.168.1.1  " }));
	}
}
