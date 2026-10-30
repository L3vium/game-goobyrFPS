using System.IO;
using Com.IsartDigital.GoobyrFPS.Net;
using Com.IsartDigital.GoobyrFPS.Player;
using Steamworks;
using Unity.Netcode;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Audio
{
    /// <summary>
    /// Proximity voice chat on Steam Voice. The speaker's machine records compressed voice, the
    /// host relays it only to players within range, and each listener decompresses it into a 3D
    /// audio source on the speaker's Goob - voices come from where people stand. The host switches
    /// it on or off for the room (lobby option); push-to-talk (B) or open mic is a local setting.
    /// Needs Steam: on a LAN session without Steam it stays silent.
    /// </summary>
    [RequireComponent(typeof(LobbyPlayer))]
    public class VoiceChat : NetworkBehaviour
    {
        public static bool PushToTalk
        {
            get => PlayerPrefs.GetInt("goobyr.ptt", 1) == 1;
            set => PlayerPrefs.SetInt("goobyr.ptt", value ? 1 : 0);
        }

        private LobbyPlayer _player;
        private AudioSource _source;
        private float[] _ring;
        private int _write, _read;
        private int _sampleRate = 24000;
        private readonly MemoryStream _compressed = new MemoryStream();
        private readonly MemoryStream _decompressed = new MemoryStream();
        private bool _recording;

        private static bool Enabled => LobbyState.Instance != null && LobbyState.Instance.ProximityChat.Value && SteamClient.IsValid;

        private void Awake() => _player = GetComponent<LobbyPlayer>();

        public override void OnNetworkSpawn()
        {
            if (SteamClient.IsValid) _sampleRate = (int)SteamUser.OptimalSampleRate;
            _ring = new float[_sampleRate * 2];
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner && SteamClient.IsValid) SteamUser.VoiceRecord = false;
            if (_source != null) Destroy(_source.gameObject);
        }

        private void Update()
        {
            if (!IsSpawned) return;
            if (IsOwner) UpdateRecording();
            UpdatePlaybackAnchor();
        }

        private void UpdateRecording()
        {
            bool lWant = Enabled && PlayerAvatar.Local != null && PlayerAvatar.Local.Health.IsAlive && (!PushToTalk || GameInput.PushToTalk);
            if (lWant != _recording)
            {
                _recording = lWant;
                SteamUser.VoiceRecord = lWant;
                _player.TalkingNow.Value = lWant;
            }
            if (!_recording || !SteamUser.HasVoiceData) return;

            _compressed.SetLength(0);
            int lBytes = SteamUser.ReadVoiceData(_compressed);
            if (lBytes <= 0) return;
            byte[] lData = new byte[lBytes];
            System.Array.Copy(_compressed.GetBuffer(), lData, lBytes);
            VoiceToServerRpc(lData);
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        private void VoiceToServerRpc(byte[] pData)
        {
            if (!LobbyState.Instance || !LobbyState.Instance.ProximityChat.Value) return;
            PlayerAvatar lSpeaker = PlayerAvatar.ForClient(OwnerClientId);
            if (lSpeaker == null) return;
            float lRange = Data.GameDatabase.Rules.proximityChatRange;
            System.Collections.Generic.List<ulong> lTargets = new System.Collections.Generic.List<ulong>();
            foreach (PlayerAvatar lListener in PlayerAvatar.All)
            {
                if (lListener.OwnerClientId == OwnerClientId) continue;
                if (Vector3.Distance(lListener.transform.position, lSpeaker.transform.position) <= lRange) lTargets.Add(lListener.OwnerClientId);
            }
            if (lTargets.Count > 0) VoiceToClientsRpc(pData, RpcTarget.Group(lTargets.ToArray(), RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Unreliable)]
        private void VoiceToClientsRpc(byte[] pData, RpcParams pParams)
        {
            if (!SteamClient.IsValid) return;
            _decompressed.SetLength(0);
            int lBytes = SteamUser.DecompressVoice(pData, _decompressed);
            if (lBytes <= 0) return;
            EnsureSource();
            byte[] lPcm = _decompressed.GetBuffer();
            for (int i = 0; i + 1 < lBytes; i += 2)
            {
                short lSample = (short)(lPcm[i] | (lPcm[i + 1] << 8));
                _ring[_write] = lSample / 32768f;
                _write = (_write + 1) % _ring.Length;
            }
        }

        private void EnsureSource()
        {
            if (_source != null) return;
            GameObject lObject = new GameObject("Voice_" + OwnerClientId);
            DontDestroyOnLoad(lObject);
            _source = lObject.AddComponent<AudioSource>();
            _source.spatialBlend = 1f;
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.minDistance = 3f;
            _source.maxDistance = Data.GameDatabase.Rules.proximityChatRange;
            _source.loop = true;
            _source.clip = AudioClip.Create("voice", _sampleRate, 1, _sampleRate, true, ReadPcm);
            _source.Play();
        }

        private void ReadPcm(float[] pData)
        {
            for (int i = 0; i < pData.Length; i++)
            {
                if (_read == _write) { pData[i] = 0f; continue; }
                pData[i] = _ring[_read];
                _read = (_read + 1) % _ring.Length;
            }
        }

        private void UpdatePlaybackAnchor()
        {
            if (_source == null) return;
            PlayerAvatar lAvatar = PlayerAvatar.ForClient(OwnerClientId);
            if (lAvatar != null) _source.transform.position = lAvatar.Eye;
        }
    }
}
