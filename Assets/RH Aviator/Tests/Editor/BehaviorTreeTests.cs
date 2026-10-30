using System.Collections.Generic;
using NUnit.Framework;
using DeepWater.Aviator.Core.Tree;

namespace DeepWater.Aviator.Tests
{
    /// <summary>The behavior tree's nodes (RH Aviator A27, slice 4a): what each says, and what each runs and skips.</summary>
    public class BehaviorTreeTests
    {
        [Test]
        public void ASequence_RunsInTurn_AndStopsAtTheFirstThatDoesNotSucceed()
        {
            var ran = new List<string>();
            var tree = new Sequence("seq",
                new Act("a", () => ran.Add("a")),
                new Condition("no", () => { ran.Add("no"); return false; }),
                new Act("c", () => ran.Add("c")));
            Assert.AreEqual(NodeStatus.Failure, tree.Tick(1));
            CollectionAssert.AreEqual(new[] { "a", "no" }, ran);
            Assert.AreEqual(long.MinValue, tree.Children[2].LastTick, "the node after the failure is never reached");
        }

        [Test]
        public void ASelector_StopsAtTheFirstThatDoesNotFail()
        {
            var ran = new List<string>();
            var tree = new Selector("sel",
                new Condition("no", () => { ran.Add("no"); return false; }),
                new Act("yes", () => ran.Add("yes")),
                new Act("never", () => ran.Add("never")));
            Assert.AreEqual(NodeStatus.Success, tree.Tick(1));
            CollectionAssert.AreEqual(new[] { "no", "yes" }, ran);
            Assert.AreEqual(NodeStatus.Failure, new Selector("none", new Condition("no", () => false)).Tick(1));
        }

        [Test]
        public void AStep_ActsAndThenSaysWhetherTheBranchGoesOn_AsAnEarlyReturnDoes()
        {
            int acted = 0;
            bool goOn = false;
            var tree = new Sequence("seq", new Step("step", () => { acted++; return goOn; }), new Act("after", () => acted += 10));
            Assert.AreEqual(NodeStatus.Failure, tree.Tick(1));
            Assert.AreEqual(1, acted, "it acted, and the branch stopped");
            goOn = true;
            Assert.AreEqual(NodeStatus.Success, tree.Tick(2));
            Assert.AreEqual(12, acted);
        }

        [Test]
        public void EveryNode_KeepsWhatItSaid_AndTheTickItRan_SoTheWindowCanLightTheBranch()
        {
            bool first = true;
            var a = new Condition("a", () => first);
            var b = new Act("b", () => { });
            var tree = new Selector("sel", a, b);
            tree.Tick(10);
            Assert.AreEqual(10, a.LastTick);
            Assert.AreEqual(NodeStatus.Success, a.Status);
            Assert.AreEqual(long.MinValue, b.LastTick, "b was skipped");
            first = false;
            tree.Tick(15);
            Assert.AreEqual(15, a.LastTick);
            Assert.AreEqual(NodeStatus.Failure, a.Status);
            Assert.AreEqual(15, b.LastTick);
            Assert.AreEqual(15, tree.LastTick);
        }

        [Test]
        public void ANode_NeedsANameAndNoNullChild()
        {
            Assert.Throws<System.ArgumentException>(() => new Act("", () => { }));
            Assert.Throws<System.ArgumentNullException>(() => new Sequence("seq", new Act("a", () => { }), null));
            Assert.Throws<System.ArgumentNullException>(() => new Condition("c", null));
        }
    }
}
