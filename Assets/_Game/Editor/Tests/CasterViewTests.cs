using Com.IsartDigital.GoobyrFPS.Player;
using NUnit.Framework;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Tests
{
    /// <summary>EditMode tests of the caster view: the screen splits and the auto-director.</summary>
    public class CasterViewTests
    {
        [Test]
        public void Layouts_FeedsStayOnScreenAndNeverOverlap()
        {
            foreach (CasterLayout lLayout in System.Enum.GetValues(typeof(CasterLayout)))
            {
                int lCount = CasterLayoutLogic.FeedCount(lLayout);
                for (int i = 0; i < lCount; i++)
                {
                    Rect lA = CasterLayoutLogic.Viewport(lLayout, i);
                    Assert.IsTrue(lA.xMin >= 0f && lA.yMin >= 0f && lA.xMax <= 1f && lA.yMax <= 1f, lLayout + " feed " + i + " off screen");
                    for (int j = i + 1; j < lCount; j++)
                    {
                        Rect lB = CasterLayoutLogic.Viewport(lLayout, j);
                        bool lOverlap = lA.xMin < lB.xMax - 1e-4f && lB.xMin < lA.xMax - 1e-4f && lA.yMin < lB.yMax - 1e-4f && lB.yMin < lA.yMax - 1e-4f;
                        Assert.IsFalse(lOverlap, lLayout + " feeds " + i + " and " + j + " overlap");
                    }
                }
            }
        }

        [Test]
        public void Layouts_FeedsKeepTheScreenAspect()
        {
            foreach (CasterLayout lLayout in new[] { CasterLayout.Duo, CasterLayout.Quad, CasterLayout.MainPlus3 })
                for (int i = 0; i < CasterLayoutLogic.FeedCount(lLayout); i++)
                {
                    Rect lFeed = CasterLayoutLogic.Viewport(lLayout, i);
                    Assert.AreEqual(lFeed.width, lFeed.height, 1e-4f, lLayout + " feed " + i + " is stretched");
                }
        }

        [Test]
        public void FeedAt_FindsTheFeedUnderThePointAndNothingInTheBands()
        {
            Assert.AreEqual(0, CasterLayoutLogic.FeedAt(CasterLayout.MainPlus3, new Vector2(0.3f, 0.6f)));
            Assert.AreEqual(1, CasterLayoutLogic.FeedAt(CasterLayout.MainPlus3, new Vector2(0.9f, 0.9f)));
            Assert.AreEqual(3, CasterLayoutLogic.FeedAt(CasterLayout.MainPlus3, new Vector2(0.9f, 0.3f)));
            Assert.AreEqual(-1, CasterLayoutLogic.FeedAt(CasterLayout.MainPlus3, new Vector2(0.5f, 0.1f)), "the agent cards band");
            Assert.AreEqual(-1, CasterLayoutLogic.FeedAt(CasterLayout.Duo, new Vector2(0.5f, 0.9f)));
            Assert.AreEqual(3, CasterLayoutLogic.FeedAt(CasterLayout.Quad, new Vector2(0.75f, 0.25f)));
        }

        [Test]
        public void Interest_ProofAndCombatFirstAndTheDeadNever()
        {
            FeedInterest lCalm = new FeedInterest { alive = true, objectiveDistance = 300f };
            FeedInterest lFight = new FeedInterest { alive = true, combat = true, objectiveDistance = 300f };
            FeedInterest lProof = new FeedInterest { alive = true, carriesProof = true, objectiveDistance = 0f };
            Assert.Greater(CasterLayoutLogic.Interest(lFight), CasterLayoutLogic.Interest(lCalm));
            Assert.Greater(CasterLayoutLogic.Interest(lProof), CasterLayoutLogic.Interest(lFight));
            Assert.Less(CasterLayoutLogic.Interest(new FeedInterest { alive = false, combat = true }), 0f);
        }

        [Test]
        public void PickMain_HoldsThenSwitchesOnlyForClearlyBetter()
        {
            float[] lScores = { 1f, 2f, 7f };
            Assert.AreEqual(0, CasterLayoutLogic.PickMain(lScores, 0, 2f), "held too briefly");
            Assert.AreEqual(2, CasterLayoutLogic.PickMain(lScores, 0, 6f));
            Assert.AreEqual(0, CasterLayoutLogic.PickMain(new[] { 5f, 6f }, 0, 30f), "not better by the margin");
            Assert.AreEqual(1, CasterLayoutLogic.PickMain(new[] { -1f, 0.5f }, 0, 0f), "the watched agent died");
            Assert.AreEqual(-1, CasterLayoutLogic.PickMain(new[] { -1f, -1f }, 0, 10f));
        }
    }
}
