using Com.IsartDigital.GoobyrFPS.AI;
using NUnit.Framework;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Tests
{
    /// <summary>EditMode tests of the NPC brain's pure rules (<see cref="NpcRules"/>): the attack chain, civilians, combat, patrols.</summary>
    public class NpcRulesTests
    {
        [Test]
        public void Glimpse_NeverCalmsASearcherOrAResponder()
        {
            Assert.IsFalse(NpcRules.CanBecomeSuspicious(NpcState.Search, false), "a searcher who glimpses you hunts you");
            Assert.IsFalse(NpcRules.CanBecomeSuspicious(NpcState.Investigate, true), "a responder running to a shot");
            Assert.IsTrue(NpcRules.CanBecomeSuspicious(NpcState.Investigate, false), "a calm look at a noise can still turn into a stare");
            Assert.IsTrue(NpcRules.CanBecomeSuspicious(NpcState.Patrol, false));
            Assert.IsTrue(NpcRules.CanBecomeSuspicious(NpcState.Idle, false));
            foreach (NpcState lState in new[] { NpcState.Combat, NpcState.Calling, NpcState.Engaged, NpcState.Flee, NpcState.Sick, NpcState.KnockedOut, NpcState.Dead })
                Assert.IsFalse(NpcRules.CanBecomeSuspicious(lState, false), lState.ToString());
            Assert.IsTrue(NpcRules.OnEdge(NpcState.Search, false));
            Assert.IsTrue(NpcRules.OnEdge(NpcState.Investigate, true));
            Assert.IsFalse(NpcRules.OnEdge(NpcState.Investigate, false));
            Assert.IsFalse(NpcRules.OnEdge(NpcState.Patrol, false));
        }
    }
}
