﻿using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mute.Moe.Extensions;

namespace Mute.Tests.Extensions
{
    [TestClass]
    public class StackExtensionsTests
    {
        [TestMethod]
        public void PopOrDefaultPops()
        {
            var s = new Stack<int>();
            s.Push(1);
            Assert.AreEqual(1, s.PopOrDefault());
        }

        [TestMethod]
        public void PopOrDefaultDefault()
        {
            var s = new Stack<int>();
            Assert.AreEqual(0, s.PopOrDefault());
        }
    }
}
