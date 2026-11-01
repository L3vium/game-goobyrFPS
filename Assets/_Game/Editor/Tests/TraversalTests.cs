using System.Collections.Generic;
using System.Reflection;
using Com.IsartDigital.GoobyrFPS.Player;
using NUnit.Framework;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Tests
{
    /// <summary>
    /// EditMode tests of the parkour probes on real colliders: thin walls and window sills (the case
    /// that used to fail), crates, high walls and ledges to hang from.
    /// </summary>
    public class TraversalTests
    {
        private const BindingFlags PRIVATE = BindingFlags.NonPublic | BindingFlags.Instance;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private FpsMotor _motor;

        [SetUp]
        public void SetUp()
        {
            Box(new Vector3(0, -0.5f, 0), new Vector3(60, 1, 20));
            GameObject lPlayer = new GameObject("Player");
            _spawned.Add(lPlayer);
            CharacterController lController = lPlayer.AddComponent<CharacterController>();
            lController.height = 1.86f;
            lController.radius = 0.35f;
            lController.center = new Vector3(0, 0.93f, 0);
            _motor = lPlayer.AddComponent<FpsMotor>();
            typeof(FpsMotor).GetMethod("Awake", PRIVATE).Invoke(_motor, null);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject lObject in _spawned) Object.DestroyImmediate(lObject);
            _spawned.Clear();
        }

        private void Box(Vector3 pPosition, Vector3 pSize)
        {
            GameObject lBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lBox.transform.position = pPosition;
            lBox.transform.localScale = pSize;
            _spawned.Add(lBox);
        }

        private bool Traverse(float pX, float pY, bool pInAir, out bool pVault, out Vector3 pTo, out MotorMode pAfter)
        {
            _motor.transform.SetPositionAndRotation(new Vector3(pX, pY, 0.9f), Quaternion.identity);
            Physics.SyncTransforms();
            bool lOk = pInAir
                ? (bool)typeof(FpsMotor).GetMethod("TryAirGrab", PRIVATE).Invoke(_motor, new object[] { Vector3.forward })
                : (bool)typeof(FpsMotor).GetMethod("TryTraverse", PRIVATE).Invoke(_motor, new object[] { Vector3.forward, true });
            pVault = (bool)typeof(FpsMotor).GetField("_vaulting", PRIVATE).GetValue(_motor);
            pTo = (Vector3)typeof(FpsMotor).GetField("_moveTo", PRIVATE).GetValue(_motor);
            pAfter = (MotorMode)typeof(FpsMotor).GetField("_afterMove", PRIVATE).GetValue(_motor);
            return lOk;
        }

        [Test]
        public void ThinLowWall_IsVaultedOver()
        {
            Box(new Vector3(0, 0.5f, 2f), new Vector3(3, 1f, 0.3f));
            Assert.IsTrue(Traverse(0, 0, false, out bool lVault, out Vector3 lTo, out _));
            Assert.IsTrue(lVault);
            Assert.Greater(lTo.z, 2.2f, "lands on the far side");
        }

        [Test]
        public void Crate_IsMantledOnto()
        {
            Box(new Vector3(10, 0.4f, 2f), new Vector3(1, 0.8f, 1));
            Assert.IsTrue(Traverse(10, 0, false, out bool lVault, out Vector3 lTo, out _));
            Assert.IsFalse(lVault);
            Assert.AreEqual(0.83f, lTo.y, 0.05f, "stands on top");
        }

        [Test]
        public void OpenWindow_IsVaultedThrough()
        {
            Box(new Vector3(20, 0.45f, 2f), new Vector3(4, 0.9f, 0.3f));
            Box(new Vector3(20, 3.0f, 2f), new Vector3(4, 1.2f, 0.3f));
            Box(new Vector3(18.6f, 1.65f, 2f), new Vector3(1.6f, 1.5f, 0.3f));
            Box(new Vector3(21.4f, 1.65f, 2f), new Vector3(1.6f, 1.5f, 0.3f));
            Assert.IsTrue(Traverse(20, 0, false, out bool lVault, out Vector3 lTo, out _));
            Assert.IsTrue(lVault);
            Assert.Greater(lTo.z, 2.2f, "ends up inside");
        }

        [Test]
        public void TallWall_IsHungFromInTheAirButNotReachedFromTheGround()
        {
            Box(new Vector3(40, 1.6f, 2.5f), new Vector3(4, 3.2f, 1f));
            Assert.IsFalse(Traverse(40, 0, false, out _, out _, out _));
            Assert.IsTrue(Traverse(40, 1.1f, true, out _, out _, out MotorMode lAfter));
            Assert.AreEqual(MotorMode.LedgeHang, lAfter);
        }
    }
}
