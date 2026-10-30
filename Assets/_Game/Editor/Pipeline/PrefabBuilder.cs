using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Audio;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Match;
using Com.IsartDigital.GoobyrFPS.Net;
using Com.IsartDigital.GoobyrFPS.Player;
using Com.IsartDigital.GoobyrFPS.Systems;
using Com.IsartDigital.GoobyrFPS.Weapons;
using Netcode.Transports.Facepunch;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// The network prefabs, the NetworkManager root and the lists that tie them together:
    /// <c>Prefabs/Net/*</c>, <c>Data/NetworkPrefabs</c> and <c>Resources/NetPrefabs</c>.
    /// </summary>
    public static class PrefabBuilder
    {
        private const string DIR = EditorAssets.PREFABS + "/Net";

        [MenuItem("Goobyr/Build/Prefabs")]
        public static void Build()
        {
            EditorAssets.EnsureFolder(DIR);

            GameObject lLobbyPlayer = Save(MakeLobbyPlayer(), "P_LobbyPlayer");
            GameObject lLobbyState = Save(MakeSimple("LobbyState", typeof(LobbyState)), "P_LobbyState");
            GameObject lMapSession = Save(MakeSimple("MapSession", typeof(MapSession), typeof(MapEffects), typeof(MatchDirector)), "P_MapSession");
            GameObject lAvatar = Save(MakeAvatar(), "P_PlayerAvatar");
            GameObject lNpc = Save(MakeNpc(), "P_Npc");
            GameObject lPickup = Save(MakePickup(), "P_ItemPickup");
            GameObject lProof = Save(MakeMoving("Proof", false, typeof(ProofPickup)), "P_Proof");
            GameObject lRocket = Save(MakeMoving("Rocket", false, typeof(Projectile)), "P_Rocket");
            GameObject lThrown = Save(MakeThrown(), "P_Thrown");

            NetworkPrefabsList lList = EditorAssets.LoadOrCreate<NetworkPrefabsList>(EditorAssets.DATA + "/NetworkPrefabs.asset");
            foreach (NetworkPrefab lExisting in new System.Collections.Generic.List<NetworkPrefab>(lList.PrefabList)) lList.Remove(lExisting);
            foreach (GameObject lPrefab in new[] { lLobbyPlayer, lLobbyState, lMapSession, lAvatar, lNpc, lPickup, lProof, lRocket, lThrown })
                lList.Add(new NetworkPrefab { Prefab = lPrefab });
            EditorUtility.SetDirty(lList);

            NetPrefabs lRefs = EditorAssets.LoadOrCreate<NetPrefabs>(EditorAssets.RESOURCES + "/NetPrefabs.asset");
            lRefs.mapSession = lMapSession.GetComponent<NetworkObject>();
            lRefs.playerAvatar = lAvatar.GetComponent<NetworkObject>();
            lRefs.npc = lNpc.GetComponent<NetworkObject>();
            lRefs.itemPickup = lPickup.GetComponent<NetworkObject>();
            lRefs.proof = lProof.GetComponent<NetworkObject>();
            lRefs.rocket = lRocket.GetComponent<NetworkObject>();
            lRefs.thrown = lThrown.GetComponent<NetworkObject>();
            EditorUtility.SetDirty(lRefs);

            Save(MakeGameRoot(lLobbyPlayer, lLobbyState, lList), "P_GameRoot");
            AssetDatabase.SaveAssets();
            Debug.Log("[Build] prefabs");
        }

        public static GameObject GameRoot => AssetDatabase.LoadAssetAtPath<GameObject>(DIR + "/P_GameRoot.prefab");

        private static GameObject Save(GameObject pObject, string pName)
        {
            string lPath = DIR + "/" + pName + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(pObject, lPath);
            Object.DestroyImmediate(pObject);
            AssetDatabase.ImportAsset(lPath, ImportAssetOptions.ForceUpdate);

            // The hash NGO matches prefabs by comes from the asset's GlobalObjectId, which only
            // exists once the asset is saved: stamp it on the asset itself.
            GameObject lAsset = AssetDatabase.LoadAssetAtPath<GameObject>(lPath);
            NetworkObject lNetworkObject = lAsset.GetComponent<NetworkObject>();
            if (lNetworkObject != null)
            {
                typeof(NetworkObject).GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.Invoke(lNetworkObject, null);
                EditorUtility.SetDirty(lAsset);
                AssetDatabase.SaveAssetIfDirty(lAsset);
            }
            return lAsset;
        }

        private static GameObject MakeSimple(string pName, params System.Type[] pComponents)
        {
            GameObject lObject = new GameObject(pName);
            lObject.AddComponent<NetworkObject>();
            foreach (System.Type lType in pComponents) lObject.AddComponent(lType);
            return lObject;
        }

        private static GameObject MakeLobbyPlayer()
        {
            GameObject lObject = MakeSimple("LobbyPlayer", typeof(LobbyPlayer), typeof(VoiceChat));
            return lObject;
        }

        private static NetworkTransform AddTransform(GameObject pObject, bool pOwner)
        {
            NetworkTransform lTransform = pObject.AddComponent<NetworkTransform>();
            lTransform.AuthorityMode = pOwner ? NetworkTransform.AuthorityModes.Owner : NetworkTransform.AuthorityModes.Server;
            lTransform.SyncRotAngleX = false;
            lTransform.SyncRotAngleZ = false;
            lTransform.SyncScaleX = lTransform.SyncScaleY = lTransform.SyncScaleZ = false;
            lTransform.Interpolate = true;
            return lTransform;
        }

        private static void AddGoob(GameObject pParent, int pLayer)
        {
            GameObject lGoob = new GameObject("Goob");
            lGoob.transform.SetParent(pParent.transform, false);
            lGoob.AddComponent<GoobBody>();
            lGoob.AddComponent<GoobAnimator>();
            lGoob.layer = pLayer;
        }

        private static GameObject MakeAvatar()
        {
            GameObject lObject = new GameObject("PlayerAvatar") { layer = GameLayers.PLAYER };
            lObject.AddComponent<NetworkObject>();
            AddTransform(lObject, true);
            CharacterController lController = lObject.AddComponent<CharacterController>();
            lController.height = 1.8f;
            lController.radius = 0.35f;
            lController.center = new Vector3(0, 0.9f, 0);
            lController.stepOffset = 0.45f;
            lController.slopeLimit = 50f;
            lController.skinWidth = 0.04f;
            lObject.AddComponent<Damageable>();
            lObject.AddComponent<FpsMotor>();
            // PlayerAvatar before WeaponHolder: WeaponHolder's RequireComponent would add a second one.
            lObject.AddComponent<PlayerAvatar>();
            lObject.AddComponent<WeaponHolder>();
            lObject.AddComponent<PlayerInteractor>();
            AddGoob(lObject, GameLayers.PLAYER);
            return lObject;
        }

        private static GameObject MakeNpc()
        {
            GameObject lObject = new GameObject("Npc") { layer = GameLayers.NPC };
            lObject.AddComponent<NetworkObject>();
            AddTransform(lObject, false);
            CapsuleCollider lCapsule = lObject.AddComponent<CapsuleCollider>();
            lCapsule.height = 1.8f;
            lCapsule.radius = 0.38f;
            lCapsule.center = new Vector3(0, 0.9f, 0);
            NavMeshAgent lAgent = lObject.AddComponent<NavMeshAgent>();
            lAgent.radius = 0.35f;
            lAgent.height = 1.8f;
            lAgent.stoppingDistance = 0.3f;
            lAgent.autoBraking = true;
            lAgent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            lAgent.enabled = false;
            lObject.AddComponent<Damageable>();
            lObject.AddComponent<Npc>();
            lObject.AddComponent<NpcVisuals>();
            AddGoob(lObject, GameLayers.NPC);
            return lObject;
        }

        private static GameObject MakeMoving(string pName, bool pOwner, params System.Type[] pComponents)
        {
            GameObject lObject = MakeSimple(pName, pComponents);
            AddTransform(lObject, pOwner);
            return lObject;
        }

        private static GameObject MakePickup()
        {
            GameObject lObject = MakeMoving("ItemPickup", false, typeof(ItemPickup));
            lObject.layer = GameLayers.PICKUP;
            Rigidbody lBody = lObject.AddComponent<Rigidbody>();
            lBody.isKinematic = true;
            lBody.mass = 2f;
            lBody.interpolation = RigidbodyInterpolation.Interpolate;
            BoxCollider lBox = lObject.AddComponent<BoxCollider>();
            lBox.size = new Vector3(0.6f, 0.4f, 0.8f);
            lBox.center = new Vector3(0, 0.2f, 0);
            return lObject;
        }

        private static GameObject MakeThrown()
        {
            GameObject lObject = MakeMoving("Thrown", false, typeof(ThrownItem));
            lObject.layer = GameLayers.PICKUP;
            Rigidbody lBody = lObject.GetComponent<Rigidbody>() ?? lObject.AddComponent<Rigidbody>();
            lBody.mass = 0.5f;
            lBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            lBody.interpolation = RigidbodyInterpolation.Interpolate;
            lObject.AddComponent<SphereCollider>().radius = 0.12f;
            return lObject;
        }

        private static GameObject MakeGameRoot(GameObject pPlayerPrefab, GameObject pLobbyState, NetworkPrefabsList pList)
        {
            GameObject lRoot = new GameObject("GameRoot");
            NetworkManager lManager = lRoot.AddComponent<NetworkManager>();
            UnityTransport lUnity = lRoot.AddComponent<UnityTransport>();
            lManager.NetworkConfig = new NetworkConfig
            {
                PlayerPrefab = pPlayerPrefab,
                ConnectionApproval = true,
                EnableSceneManagement = true,
                TickRate = 30,
                NetworkTransport = lUnity
            };
            lManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(pList);

            GameObject lSteam = new GameObject("SteamTransport");
            lSteam.transform.SetParent(lRoot.transform, false);
            lSteam.AddComponent<FacepunchTransport>();
            lSteam.SetActive(false);

            lRoot.AddComponent<SteamLobby>();
            NetSession lSession = lRoot.AddComponent<NetSession>();
            SerializedObject lSerialized = new SerializedObject(lSession);
            lSerialized.FindProperty("_networkManager").objectReferenceValue = lManager;
            lSerialized.FindProperty("_unityTransport").objectReferenceValue = lUnity;
            lSerialized.FindProperty("_steamTransportObject").objectReferenceValue = lSteam;
            lSerialized.FindProperty("_lobbyStatePrefab").objectReferenceValue = pLobbyState.GetComponent<NetworkObject>();
            lSerialized.ApplyModifiedPropertiesWithoutUndo();
            return lRoot;
        }
    }
}
